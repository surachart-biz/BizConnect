using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Requests;
using BizConnect.Services.Models.Results;
using BizConnect.Services.Models.KBank;
using BizConnect.Services.Utils;

namespace BizConnect.Services
{
    /// <summary>
    /// Service for managing KBank ODD registration lifecycle
    /// </summary>
    public class RegistrationManagementService : IRegistrationManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IKbankOddService _kbankOddService;
        private readonly IOtacCodeGenerator _otacCodeGenerator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<RegistrationManagementService> _logger;

        public RegistrationManagementService(
            IUnitOfWork unitOfWork,
            IKbankOddService kbankOddService,
            IOtacCodeGenerator otacCodeGenerator,
            IDateTimeProvider dateTimeProvider,
            ILogger<RegistrationManagementService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _kbankOddService = kbankOddService ?? throw new ArgumentNullException(nameof(kbankOddService));
            _otacCodeGenerator = otacCodeGenerator ?? throw new ArgumentNullException(nameof(otacCodeGenerator));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initiates a new KBank ODD registration after validating the provided data
        /// </summary>
        /// <param name="request">Registration request containing user data and OTAC code</param>
        /// <returns>RegistrationResult containing redirect URL and registration details</returns>
        public async Task<RegistrationResult> StartAsync(RegistrationRequest request)
        {
            try
            {
                _logger.LogInformation("Starting registration process for OTAC {OtacCode}", request.OtacCode);

                // Validate the registration request
                var validationResult = await ValidateRegistrationRequestAsync(request);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.GetAllErrors();
                    var errorMessage = errors.FirstOrDefault() ?? "Validation failed";
                    return RegistrationResult.Failure(errorMessage);
                }

                // Find and validate the OTAC code
                var normalizedOtac = _otacCodeGenerator.NormalizeCode(request.OtacCode);
                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .Include(r => r.Branch)
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedOtac && r.OtacState == "Validated");

                if (registration == null)
                {
                    _logger.LogWarning("OTAC code {OtacCode} not found or not validated", normalizedOtac);
                    return RegistrationResult.Failure("Invalid or unvalidated OTAC code");
                }

                // Check if OTAC has expired
                var now = _dateTimeProvider.UtcNow;
                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    _logger.LogWarning("OTAC code {OtacCode} has expired", normalizedOtac);
                    return RegistrationResult.Failure("OTAC code has expired");
                }

                // Use transaction for atomic operations
                return await _unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
                {
                    // Generate external reference
                    var externalReference = OddUtils.GenerateExternalReference();

                    // Update registration with form data
                    registration.ExternalReference = externalReference;
                    registration.FullName = request.FullName;
                    registration.IdType = request.IdType;
                    registration.IdValue = request.IdValue;
                    registration.MobileNo = request.MobileNo;
                    registration.AccountNo = request.AccountNo;
                    registration.BranchId = request.BranchId;
                    registration.OtacState = "Used";
                    registration.UpdatedAt = now;

                    // Prepare KBank registration request
                    var kbankRequest = new OddRegistrationRequest
                    {
                        FullName = request.FullName,
                        MobileNo = request.MobileNo,
                        IdType = request.IdType,
                        IdValue = request.IdValue,
                        AccountNo = request.AccountNo,
                        BranchId = request.BranchId
                    };

                    // Call KBank API with existing external reference
                    string redirectUrl;
                    try
                    {
                        redirectUrl = await _kbankOddService.StartRegistrationWithExistingReferenceAsync(
                            kbankRequest, externalReference, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "KBank API call failed for external reference {ExternalReference}", externalReference);
                        return RegistrationResult.ExternalServiceError("KBank", ex.Message);
                    }

                    // Extract RegId from redirect URL
                    var regId = ExtractRegIdFromRedirectUrl(redirectUrl);
                    if (string.IsNullOrEmpty(regId))
                    {
                        _logger.LogWarning("Failed to extract RegId from redirect URL {RedirectUrl}", redirectUrl);
                        return RegistrationResult.Failure("Failed to extract registration ID from KBank response");
                    }

                    // Update registration with KBank response
                    registration.RegId = regId;
                    registration.Status = "Pending";

                    await uow.SaveChangesAsync(ct);

                    var registrationInfo = new RegistrationInfo
                    {
                        RedirectUrl = redirectUrl,
                        ExternalReference = externalReference,
                        RegId = regId,
                        RegistrationId = Guid.NewGuid(), // Using a placeholder GUID
                        Status = "Pending",
                        CreatedAt = registration.CreatedAt,
                        ContactPhone = request.MobileNo
                    };

                    _logger.LogInformation("Registration started successfully with external reference {ExternalReference} and RegId {RegId}", 
                        externalReference, regId);

                    return RegistrationResult.Success(registrationInfo);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting registration for OTAC {OtacCode}", request.OtacCode);
                return RegistrationResult.Failure(ex);
            }
        }

        /// <summary>
        /// Updates the status of an existing registration (typically called by KBank webhook)
        /// </summary>
        /// <param name="regId">KBank registration ID</param>
        /// <param name="status">New status (Success, Fail, etc.)</param>
        /// <param name="returnCode">KBank return code (optional)</param>
        /// <param name="espaId">ESPA ID for successful registrations (optional)</param>
        /// <returns>Result indicating success or failure of the status update</returns>
        public async Task<Result> UpdateStatusAsync(string regId, string status, string? returnCode = null, string? espaId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(regId))
                {
                    return Result.Failure("RegId is required");
                }

                if (string.IsNullOrWhiteSpace(status))
                {
                    return Result.Failure("Status is required");
                }

                _logger.LogInformation("Updating registration status for RegId {RegId} to {Status}", regId, status);

                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .FirstOrDefaultAsync(r => r.RegId == regId);

                if (registration == null)
                {
                    _logger.LogWarning("Registration with RegId {RegId} not found", regId);
                    return Result.Failure($"Registration with RegId {regId} not found");
                }

                // Update registration status
                registration.Status = status;
                registration.ReturnCode = returnCode;
                registration.EspaId = espaId;
                registration.UpdatedAt = _dateTimeProvider.UtcNow;

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Registration status updated successfully for RegId {RegId}: {Status}", regId, status);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating registration status for RegId {RegId}", regId);
                return Result.Failure($"Failed to update registration status: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a registration by external reference
        /// </summary>
        /// <param name="externalRef">BizConnect external reference (BIZyyyyMMddHHmmssfff format)</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        public async Task<Result<KbankOddRegistration>> GetByExternalRefAsync(string externalRef)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(externalRef))
                {
                    return Result<KbankOddRegistration>.Failure("External reference is required");
                }

                if (!OddUtils.IsValidExternalReference(externalRef))
                {
                    return Result<KbankOddRegistration>.Failure("Invalid external reference format");
                }

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .FirstOrDefaultAsync(r => r.ExternalReference == externalRef);

                if (registration == null)
                {
                    _logger.LogWarning("Registration with external reference {ExternalRef} not found", externalRef);
                    return Result<KbankOddRegistration>.Failure($"Registration with external reference {externalRef} not found");
                }

                return Result<KbankOddRegistration>.Success(registration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration by external reference {ExternalRef}", externalRef);
                return Result<KbankOddRegistration>.Failure($"Failed to retrieve registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a registration by KBank registration ID
        /// </summary>
        /// <param name="regId">KBank registration ID</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        public async Task<Result<KbankOddRegistration>> GetByRegIdAsync(string regId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(regId))
                {
                    return Result<KbankOddRegistration>.Failure("RegId is required");
                }

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .FirstOrDefaultAsync(r => r.RegId == regId);

                if (registration == null)
                {
                    _logger.LogWarning("Registration with RegId {RegId} not found", regId);
                    return Result<KbankOddRegistration>.Failure($"Registration with RegId {regId} not found");
                }

                return Result<KbankOddRegistration>.Success(registration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration by RegId {RegId}", regId);
                return Result<KbankOddRegistration>.Failure($"Failed to retrieve registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the registration request data
        /// </summary>
        private async Task<ValidationResult> ValidateRegistrationRequestAsync(RegistrationRequest request)
        {
            var validationErrors = new List<string>();

            // Validate OTAC format
            if (!_otacCodeGenerator.IsValidFormat(request.OtacCode))
            {
                validationErrors.Add("Invalid OTAC code format");
            }

            // Validate branch exists
            if (request.BranchId > 0)
            {
                var branch = await _unitOfWork.Branches.GetByIdAsync(request.BranchId);
                if (branch == null)
                {
                    validationErrors.Add("Selected branch does not exist");
                }
            }

            // Additional business rule validations can be added here

            return validationErrors.Any() 
                ? ValidationResult.Invalid(string.Join("; ", validationErrors))
                : ValidationResult.Valid();
        }

        /// <summary>
        /// Extracts RegId from KBank redirect URL
        /// </summary>
        private string? ExtractRegIdFromRedirectUrl(string redirectUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(redirectUrl))
                    return null;

                // Parse URL and extract RegId parameter
                var uri = new Uri(redirectUrl);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return queryParams["RegId"];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting RegId from redirect URL {RedirectUrl}", redirectUrl);
                return null;
            }
        }
    }
}