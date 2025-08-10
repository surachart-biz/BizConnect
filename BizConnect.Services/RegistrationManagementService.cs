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
                    // Generate unique external reference with retry logic
                    string externalReference;
                    var maxRetries = 3;
                    var retryCount = 0;
                    
                    do
                    {
                        externalReference = OddUtils.GenerateExternalReference();
                        retryCount++;
                        
                        // Check if this external reference already exists
                        var existingRegistration = await uow.KbankOddRegistrations
                            .Query()
                            .FirstOrDefaultAsync(r => r.ExternalReference == externalReference, ct);
                            
                        if (existingRegistration == null)
                        {
                            break; // Unique reference found
                        }
                        
                        if (retryCount >= maxRetries)
                        {
                            _logger.LogError("Failed to generate unique external reference after {MaxRetries} attempts", maxRetries);
                            throw new InvalidOperationException("Unable to generate unique external reference");
                        }
                        
                        // Add small delay before retry to reduce collision probability
                        await Task.Delay(10 * retryCount, ct);
                        
                    } while (retryCount < maxRetries);

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
                    registration.StatusMessageTh = "รหัส OTAC ถูกใช้งานแล้ว";
                    registration.StatusMessageEn = "OTAC code used";

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
                    registration.StatusMessageTh = "กำลังดำเนินการลงทะเบียน";
                    registration.StatusMessageEn = "Registration in progress";

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

                // Update registration status and set appropriate messages
                registration.Status = status;
                registration.ReturnCode = returnCode;
                registration.EspaId = espaId;
                registration.UpdatedAt = _dateTimeProvider.UtcNow;
                
                // Set status messages based on status
                switch (status.ToLower())
                {
                    case "success":
                        registration.StatusMessageTh = "ลงทะเบียนสำเร็จ";
                        registration.StatusMessageEn = "Registration successful";
                        break;
                    case "fail":
                        registration.StatusMessageTh = "ลงทะเบียนไม่สำเร็จ";
                        registration.StatusMessageEn = "Registration failed";
                        if (!string.IsNullOrEmpty(returnCode))
                        {
                            registration.ErrorMessageTh = $"รหัสข้อผิดพลาด: {returnCode}";
                            registration.ErrorMessageEn = $"Error code: {returnCode}";
                        }
                        break;
                    case "pending":
                        registration.StatusMessageTh = "กำลังดำเนินการลงทะเบียน";
                        registration.StatusMessageEn = "Registration in progress";
                        break;
                    default:
                        registration.StatusMessageTh = $"สถานะ: {status}";
                        registration.StatusMessageEn = $"Status: {status}";
                        break;
                }

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
        private async Task<Models.Results.ValidationResult> ValidateRegistrationRequestAsync(RegistrationRequest request)
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
                ? Models.Results.ValidationResult.Invalid(string.Join("; ", validationErrors))
                : Models.Results.ValidationResult.Valid();
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

        /// <summary>
        /// Updates registration status (API compatible method)
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <param name="status">New status</param>
        /// <returns>Result indicating success/failure</returns>
        public async Task<Result> UpdateRegistrationStatusAsync(int id, string status)
        {
            try
            {
                if (id <= 0)
                {
                    return Result.Failure("Invalid registration ID");
                }

                if (string.IsNullOrWhiteSpace(status))
                {
                    return Result.Failure("Status is required");
                }

                _logger.LogInformation("Updating registration status for ID {Id} to {Status}", id, status);

                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (registration == null)
                {
                    _logger.LogWarning("Registration with ID {Id} not found", id);
                    return Result.Failure($"Registration with ID {id} not found");
                }

                // Update registration status and set appropriate messages
                registration.Status = status;
                registration.UpdatedAt = _dateTimeProvider.UtcNow;
                
                // Set status messages based on status
                switch (status.ToLower())
                {
                    case "success":
                        registration.StatusMessageTh = "ลงทะเบียนสำเร็จ";
                        registration.StatusMessageEn = "Registration successful";
                        break;
                    case "fail":
                        registration.StatusMessageTh = "ลงทะเบียนไม่สำเร็จ";
                        registration.StatusMessageEn = "Registration failed";
                        break;
                    case "pending":
                        registration.StatusMessageTh = "กำลังดำเนินการลงทะเบียน";
                        registration.StatusMessageEn = "Registration in progress";
                        break;
                    default:
                        registration.StatusMessageTh = $"สถานะ: {status}";
                        registration.StatusMessageEn = $"Status: {status}";
                        break;
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Registration status updated successfully for ID {Id}: {Status}", id, status);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating registration status for ID {Id}", id);
                return Result.Failure($"Failed to update registration status: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a registration with business logic
        /// </summary>
        /// <param name="registration">Registration to process</param>
        /// <returns>Result with processing outcome</returns>
        public async Task<Result<KbankOddRegistration>> ProcessRegistrationAsync(KbankOddRegistration registration)
        {
            try
            {
                if (registration == null)
                {
                    return Result<KbankOddRegistration>.Failure("Registration is required");
                }

                _logger.LogInformation("Processing registration {Id} with status {Status}", registration.Id, registration.Status);

                // Apply business rules based on current status
                switch (registration.Status)
                {
                    case "Pending":
                        // No additional processing needed for pending registrations
                        break;

                    case "Success":
                        // Mark OTAC as used if not already
                        if (registration.OtacState != "Used")
                        {
                            registration.OtacState = "Used";
                            registration.StatusMessageTh = "ลงทะเบียนสำเร็จ";
                            registration.StatusMessageEn = "Registration successful";
                        }
                        break;

                    case "Fail":
                        // Keep OTAC as validated for potential retry
                        if (registration.OtacState == "Used")
                        {
                            registration.OtacState = "Validated";
                            registration.StatusMessageTh = "ลงทะเบียนไม่สำเร็จ - สามารถลองใหม่ได้";
                            registration.StatusMessageEn = "Registration failed - can retry";
                        }
                        break;

                    default:
                        _logger.LogWarning("Unknown status {Status} for registration {Id}", registration.Status, registration.Id);
                        break;
                }

                registration.UpdatedAt = _dateTimeProvider.UtcNow;
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Registration {Id} processed successfully", registration.Id);
                return Result<KbankOddRegistration>.Success(registration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing registration {Id}", registration?.Id);
                return Result<KbankOddRegistration>.Failure($"Failed to process registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets registration trends over time
        /// </summary>
        /// <param name="days">Number of days to analyze</param>
        /// <returns>Result with trend data</returns>
        public async Task<Result<RegistrationTrends>> GetRegistrationTrendsAsync(int days = 30)
        {
            try
            {
                if (days <= 0 || days > 365) days = 30;

                var now = _dateTimeProvider.UtcNow;
                var startDate = now.AddDays(-days).Date;
                var endDate = now.Date;

                _logger.LogDebug("Calculating registration trends for {Days} days ({StartDate} to {EndDate})", 
                    days, startDate, endDate);

                var registrations = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .Where(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate)
                    .ToListAsync();

                // Calculate daily counts
                var dailyCounts = new Dictionary<DateTime, int>();
                var dailySuccessCounts = new Dictionary<DateTime, int>();
                var dailyFailureCounts = new Dictionary<DateTime, int>();

                // Initialize all dates with zero counts
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dailyCounts[date] = 0;
                    dailySuccessCounts[date] = 0;
                    dailyFailureCounts[date] = 0;
                }

                // Populate actual counts
                foreach (var reg in registrations)
                {
                    var date = reg.CreatedAt.Date;
                    dailyCounts[date] = dailyCounts.GetValueOrDefault(date, 0) + 1;

                    if (reg.Status == "Success")
                    {
                        dailySuccessCounts[date] = dailySuccessCounts.GetValueOrDefault(date, 0) + 1;
                    }
                    else if (reg.Status == "Fail")
                    {
                        dailyFailureCounts[date] = dailyFailureCounts.GetValueOrDefault(date, 0) + 1;
                    }
                }

                // Calculate metrics
                var totalRegistrations = registrations.Count();
                var successfulRegistrations = registrations.Count(r => r.Status == "Success");
                var failedRegistrations = registrations.Count(r => r.Status == "Fail");
                
                var overallSuccessRate = totalRegistrations > 0 
                    ? (decimal)successfulRegistrations / totalRegistrations * 100 
                    : 0;

                var averageDailyCount = days > 0 ? (decimal)totalRegistrations / days : 0;

                // Find peak day
                var peakEntry = dailyCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                var peakDay = peakEntry.Key != default ? peakEntry.Key : (DateTime?)null;
                var peakCount = peakEntry.Value;

                // Calculate trend direction (simple linear trend)
                var trendDirection = CalculateTrendDirection(dailyCounts);

                var trends = new RegistrationTrends
                {
                    DailyCounts = dailyCounts,
                    DailySuccessCounts = dailySuccessCounts,
                    DailyFailureCounts = dailyFailureCounts,
                    OverallSuccessRate = overallSuccessRate,
                    TrendDirection = trendDirection,
                    PeakDay = peakDay,
                    PeakCount = peakCount,
                    AverageDailyCount = averageDailyCount,
                    PeriodStart = startDate,
                    PeriodEnd = endDate,
                    DaysAnalyzed = days,
                    GeneratedAt = now
                };

                _logger.LogInformation("Generated registration trends: {TotalRegistrations} total, {SuccessRate}% success rate, trend {TrendDirection}", 
                    totalRegistrations, overallSuccessRate, trendDirection);

                return Result<RegistrationTrends>.Success(trends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating registration trends for {Days} days", days);
                return Result<RegistrationTrends>.Failure($"Failed to calculate trends: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates trend direction based on daily counts
        /// </summary>
        private string CalculateTrendDirection(Dictionary<DateTime, int> dailyCounts)
        {
            if (dailyCounts.Count < 2) return "stable";

            var sortedData = dailyCounts.OrderBy(kvp => kvp.Key).ToList();
            var firstHalf = sortedData.Take(sortedData.Count / 2).Sum(kvp => kvp.Value);
            var secondHalf = sortedData.Skip(sortedData.Count / 2).Sum(kvp => kvp.Value);

            var difference = secondHalf - firstHalf;
            var threshold = Math.Max(1, sortedData.Sum(kvp => kvp.Value) * 0.1); // 10% threshold

            if (difference > threshold) return "increasing";
            if (difference < -threshold) return "decreasing";
            return "stable";
        }

        /// <summary>
        /// Submits a validated OTAC registration to KBank (Phase 3 of OTAC flow)
        /// This method specifically handles the transition from validated OTAC to KBank submission
        /// </summary>
        /// <param name="validatedOtacCode">The OTAC code that has been validated in Phase 2</param>
        /// <param name="registrationData">Guest registration form data</param>
        /// <returns>RegistrationResult containing redirect URL and external reference</returns>
        public async Task<RegistrationResult> SubmitAsync(string validatedOtacCode, RegistrationRequest registrationData)
        {
            try
            {
                _logger.LogInformation("Submitting validated OTAC {OtacCode} for KBank registration", validatedOtacCode);

                // Validate input parameters
                if (string.IsNullOrWhiteSpace(validatedOtacCode))
                {
                    return RegistrationResult.Failure("OTAC code is required");
                }

                if (registrationData == null)
                {
                    return RegistrationResult.Failure("Registration data is required");
                }

                // Find and validate the OTAC record (must be in "Validated" state)
                var normalizedOtac = _otacCodeGenerator.NormalizeCode(validatedOtacCode);
                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .Include(r => r.Branch)
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedOtac && r.OtacState == "Validated");

                if (registration == null)
                {
                    _logger.LogWarning("OTAC code {OtacCode} not found or not in validated state", normalizedOtac);
                    return RegistrationResult.Failure("Invalid or unvalidated OTAC code");
                }

                // Check if OTAC has expired
                var now = _dateTimeProvider.UtcNow;
                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    _logger.LogWarning("OTAC code {OtacCode} has expired", normalizedOtac);
                    return RegistrationResult.Failure("OTAC code has expired");
                }

                // Ensure this OTAC hasn't already been used
                if (!string.IsNullOrEmpty(registration.ExternalReference))
                {
                    _logger.LogWarning("OTAC code {OtacCode} has already been used (ExternalReference: {ExternalReference})", 
                        normalizedOtac, registration.ExternalReference);
                    return RegistrationResult.Failure("OTAC code has already been used");
                }

                // Phase 3: Generate ExternalReference and submit to KBank
                return await _unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
                {
                    // Generate unique external reference with retry logic
                    string externalReference;
                    var maxRetries = 3;
                    var retryCount = 0;
                    
                    do
                    {
                        externalReference = OddUtils.GenerateExternalReference();
                        retryCount++;
                        
                        // Check if this external reference already exists
                        var existingRegistration = await uow.KbankOddRegistrations
                            .Query()
                            .FirstOrDefaultAsync(r => r.ExternalReference == externalReference, ct);
                            
                        if (existingRegistration == null)
                        {
                            break; // Unique reference found
                        }
                        
                        if (retryCount >= maxRetries)
                        {
                            _logger.LogError("Failed to generate unique external reference after {MaxRetries} attempts", maxRetries);
                            throw new InvalidOperationException("Unable to generate unique external reference");
                        }
                        
                        // Add small delay before retry to reduce collision probability
                        await Task.Delay(10 * retryCount, ct);
                        
                    } while (retryCount < maxRetries);

                    // Update registration with form data and external reference
                    registration.ExternalReference = externalReference;
                    registration.FullName = registrationData.FullName;
                    registration.IdType = registrationData.IdType;
                    registration.IdValue = registrationData.IdValue;
                    registration.MobileNo = registrationData.MobileNo;
                    registration.AccountNo = registrationData.AccountNo;
                    registration.BranchId = registrationData.BranchId;
                    registration.OtacState = "Used";
                    registration.UpdatedAt = now;
                    registration.StatusMessageTh = "กำลังส่งข้อมูลไปยัง KBank";
                    registration.StatusMessageEn = "Submitting data to KBank";

                    // Prepare KBank registration request
                    var kbankRequest = new OddRegistrationRequest
                    {
                        FullName = registrationData.FullName,
                        MobileNo = registrationData.MobileNo,
                        IdType = registrationData.IdType,
                        IdValue = registrationData.IdValue,
                        AccountNo = registrationData.AccountNo,
                        BranchId = registrationData.BranchId
                    };

                    // Call KBank API with the generated external reference
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
                    registration.StatusMessageTh = "กำลังดำเนินการลงทะเบียน";
                    registration.StatusMessageEn = "Registration in progress";

                    await uow.SaveChangesAsync(ct);

                    var registrationInfo = new RegistrationInfo
                    {
                        RedirectUrl = redirectUrl,
                        ExternalReference = externalReference,
                        RegId = regId,
                        RegistrationId = Guid.NewGuid(), // Using a placeholder GUID
                        Status = "Pending",
                        CreatedAt = registration.CreatedAt,
                        ContactPhone = registrationData.MobileNo
                    };

                    _logger.LogInformation("OTAC registration submitted successfully with external reference {ExternalReference} and RegId {RegId}", 
                        externalReference, regId);

                    return RegistrationResult.Success(registrationInfo);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting OTAC registration for code {OtacCode}", validatedOtacCode);
                return RegistrationResult.Failure(ex);
            }
        }
    }
}