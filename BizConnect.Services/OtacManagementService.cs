using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services
{
    /// <summary>
    /// Service for managing One-Time Access Codes (OTAC) lifecycle
    /// </summary>
    public class OtacManagementService : IOtacManagementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IOtacCodeGenerator _otacCodeGenerator;
        private readonly ILogger<OtacManagementService> _logger;

        private const int OtacExpiryMinutes = 30;
        private const int MaxValidationAttempts = 5;

        public OtacManagementService(
            IUnitOfWork unitOfWork,
            IDateTimeProvider dateTimeProvider,
            IOtacCodeGenerator otacCodeGenerator,
            ILogger<OtacManagementService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _otacCodeGenerator = otacCodeGenerator ?? throw new ArgumentNullException(nameof(otacCodeGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates a new 8-character OTAC code for the specified user and purpose
        /// </summary>
        /// <param name="userId">ID of the user generating the OTAC</param>
        /// <param name="purpose">Purpose of the OTAC (e.g., "Registration", "Verification")</param>
        /// <returns>OtacResult containing the generated code and expiry information</returns>
        public async Task<OtacResult> GenerateAsync(int userId, string purpose = "Registration")
        {
            try
            {
                _logger.LogInformation("Generating OTAC for user {UserId} with purpose {Purpose}", userId, purpose);

                // Verify user exists
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("OTAC generation failed: User {UserId} not found", userId);
                    return OtacResult.Failure("User not found");
                }

                var now = _dateTimeProvider.UtcNow;
                var expiresAt = now.AddMinutes(OtacExpiryMinutes);
                var otacCode = _otacCodeGenerator.GenerateCode();

                // Create new registration record
                var registration = new KbankOddRegistration
                {
                    ExternalReference = string.Empty, // Will be set later when form is submitted
                    RegId = string.Empty, // Will be set by KBank
                    Status = string.Empty, // Will be set when KBank call is made
                    CreatedAt = now,
                    GeneratedByUserId = userId,
                    OtacCode = otacCode,
                    OtacState = "Generated",
                    OtacExpiresAt = expiresAt,
                    AttemptCount = 0,
                    IsLocked = false
                };

                await _unitOfWork.KbankOddRegistrations.AddAsync(registration);
                await _unitOfWork.SaveChangesAsync();

                var otacInfo = new OtacInfo
                {
                    Code = otacCode,
                    ExpiresAt = expiresAt,
                    RegistrationId = Guid.NewGuid(), // Using registration.Id would require conversion logic
                    Purpose = purpose,
                    RemainingAttempts = MaxValidationAttempts,
                    DeliveryMethod = "Display",
                    DeliveryDestination = "Screen"
                };

                _logger.LogInformation("OTAC {OtacCode} generated successfully for user {UserId}, expires at {ExpiresAt}", 
                    otacCode, userId, expiresAt);

                return OtacResult.Success(otacInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OTAC for user {UserId}", userId);
                return OtacResult.Failure(ex);
            }
        }

        /// <summary>
        /// Validates an OTAC code and tracks the validation attempt
        /// </summary>
        /// <param name="code">The OTAC code to validate (case-insensitive)</param>
        /// <param name="clientIp">IP address of the client making the validation request</param>
        /// <returns>OtacResult indicating validation success/failure with remaining attempts</returns>
        public async Task<OtacResult> ValidateAsync(string code, string clientIp)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("OTAC validation failed: Code is null or empty");
                    return OtacResult.Failure("OTAC code is required");
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    _logger.LogWarning("OTAC validation failed: Invalid format for code {Code}", code);
                    return OtacResult.Failure("Invalid OTAC code format");
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);
                var now = _dateTimeProvider.UtcNow;

                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} not found", normalizedCode);
                    return OtacResult.NotFound();
                }

                // Check if locked
                if (registration.IsLocked)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} is locked", normalizedCode);
                    return OtacResult.LockedCode();
                }

                // Check if expired
                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} has expired at {ExpiresAt}", 
                        normalizedCode, registration.OtacExpiresAt.Value);
                    return OtacResult.ExpiredCode();
                }

                // Increment attempt count and update tracking
                registration.AttemptCount++;
                registration.LastAttemptAt = now;
                registration.LastAttemptIp = clientIp;

                // Check if this attempt puts us over the limit
                if (registration.AttemptCount >= MaxValidationAttempts)
                {
                    registration.IsLocked = true;
                    await _unitOfWork.SaveChangesAsync();
                    
                    _logger.LogWarning("OTAC {Code} locked after {AttemptCount} failed attempts", 
                        normalizedCode, registration.AttemptCount);
                    return OtacResult.LockedCode();
                }

                // Update state to validated
                registration.OtacState = "Validated";
                await _unitOfWork.SaveChangesAsync();

                var remainingAttempts = MaxValidationAttempts - registration.AttemptCount;
                var otacInfo = new OtacInfo
                {
                    Code = normalizedCode,
                    ExpiresAt = registration.OtacExpiresAt ?? now.AddMinutes(OtacExpiryMinutes),
                    RegistrationId = Guid.NewGuid(),
                    Purpose = "Registration",
                    RemainingAttempts = remainingAttempts
                };

                _logger.LogInformation("OTAC {Code} validated successfully with {RemainingAttempts} attempts remaining", 
                    normalizedCode, remainingAttempts);

                return OtacResult.Success(otacInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating OTAC code {Code}", code);
                return OtacResult.Failure(ex);
            }
        }

        /// <summary>
        /// Checks if an OTAC code is valid without incrementing attempt counter
        /// </summary>
        /// <param name="code">The OTAC code to check</param>
        /// <returns>ValidationResult indicating whether the code is valid</returns>
        public async Task<ValidationResult> IsValidAsync(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return ValidationResult.Invalid("OTAC code is required");
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    return ValidationResult.Invalid("Invalid OTAC code format");
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);
                var now = _dateTimeProvider.UtcNow;

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    return ValidationResult.Invalid("OTAC code not found");
                }

                if (registration.IsLocked)
                {
                    return ValidationResult.Invalid("OTAC code is locked");
                }

                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    return ValidationResult.Invalid("OTAC code has expired");
                }

                return ValidationResult.Valid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OTAC validity for code {Code}", code);
                return ValidationResult.Invalid($"System error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves information about an OTAC code without affecting its state
        /// </summary>
        /// <param name="code">The OTAC code to retrieve information for</param>
        /// <returns>OtacResult containing code information or failure if not found</returns>
        public async Task<OtacResult> GetInfoAsync(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return OtacResult.Failure("OTAC code is required");
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    return OtacResult.Failure("Invalid OTAC code format");
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    return OtacResult.NotFound();
                }

                var remainingAttempts = Math.Max(0, MaxValidationAttempts - registration.AttemptCount);
                var otacInfo = new OtacInfo
                {
                    Code = normalizedCode,
                    ExpiresAt = registration.OtacExpiresAt ?? _dateTimeProvider.UtcNow.AddMinutes(OtacExpiryMinutes),
                    RegistrationId = Guid.NewGuid(),
                    Purpose = "Registration",
                    RemainingAttempts = remainingAttempts
                };

                return OtacResult.Success(otacInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OTAC info for code {Code}", code);
                return OtacResult.Failure(ex);
            }
        }

        /// <summary>
        /// Removes expired OTAC codes from the system
        /// </summary>
        /// <returns>Result indicating success with error details if purge fails</returns>
        public async Task<Result> PurgeExpiredAsync()
        {
            try
            {
                var now = _dateTimeProvider.UtcNow;
                
                // Find expired registrations that haven't been used
                var expiredRegistrations = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .Where(r => r.OtacExpiresAt.HasValue && 
                               r.OtacExpiresAt.Value <= now &&
                               r.OtacState != "Used" &&
                               string.IsNullOrEmpty(r.Status)) // Not yet submitted to KBank
                    .ToListAsync();

                if (expiredRegistrations.Any())
                {
                    // Soft delete by updating state rather than hard delete for audit purposes
                    foreach (var registration in expiredRegistrations)
                    {
                        registration.OtacState = "Expired";
                        registration.UpdatedAt = now;
                    }

                    await _unitOfWork.SaveChangesAsync();
                    
                    _logger.LogInformation("Purged {Count} expired OTAC codes", expiredRegistrations.Count);
                }
                else
                {
                    _logger.LogDebug("No expired OTAC codes found for purging");
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging expired OTAC codes");
                return Result.Failure($"Failed to purge expired OTAC codes: {ex.Message}");
            }
        }
    }
}