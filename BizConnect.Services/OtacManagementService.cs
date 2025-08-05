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
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult containing the generated code and expiry information</returns>
        public async Task<OtacResult> GenerateAsync(int userId, string purpose = "Registration", string language = "en")
        {
            try
            {
                _logger.LogInformation("Generating OTAC for user {UserId} with purpose {Purpose} in language {Language}", userId, purpose, language);

                // Verify user exists
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("OTAC generation failed: User {UserId} not found", userId);
                    var userNotFoundMessage = language.ToLower() == "th" 
                        ? "ไม่พบผู้ใช้งาน" 
                        : "User not found";
                    return OtacResult.Failure(userNotFoundMessage);
                }

                var now = _dateTimeProvider.UtcNow;
                var expiresAt = now.AddMinutes(OtacExpiryMinutes);
                var otacCode = _otacCodeGenerator.GenerateCode();

                // Create new registration record
                var registration = new KbankOddRegistration
                {
                    ExternalReference = null, // Will be set later when form is submitted
                    RegId = null, // Will be set by KBank
                    Status = null, // Will be set when KBank call is made
                    CreatedAt = now,
                    GeneratedByUserId = userId,
                    OtacCode = otacCode,
                    OtacState = "Generated",
                    OtacExpiresAt = expiresAt,
                    AttemptCount = 0,
                    IsLocked = false,
                    StatusMessageTh = "รหัส OTAC ถูกสร้างแล้ว",
                    StatusMessageEn = "OTAC code generated"
                };

                await _unitOfWork.KbankOddRegistrations.AddAsync(registration);
                await _unitOfWork.SaveChangesAsync();

                var otacInfo = new OtacInfo
                {
                    Code = otacCode,
                    ExpiresAt = expiresAt,
                    RegistrationId = userId, // Using the user ID as registration ID
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
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult indicating validation success/failure with remaining attempts</returns>
        public async Task<OtacResult> ValidateAsync(string code, string clientIp, string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("OTAC validation failed: Code is null or empty");
                    var requiredMessage = language.ToLower() == "th" 
                        ? "จำเป็นต้องระบุรหัส OTAC" 
                        : "OTAC code is required";
                    return OtacResult.Failure(requiredMessage);
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    _logger.LogWarning("OTAC validation failed: Invalid format for code {Code}", code);
                    var formatMessage = language.ToLower() == "th" 
                        ? "รูปแบบรหัส OTAC ไม่ถูกต้อง" 
                        : "Invalid OTAC code format";
                    return OtacResult.Failure(formatMessage);
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);
                var now = _dateTimeProvider.UtcNow;

                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} not found", normalizedCode);
                    return OtacResult.NotFound(language);
                }

                // Check if locked
                if (registration.IsLocked)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} is locked", normalizedCode);
                    return OtacResult.LockedCode(language);
                }

                // Check if expired
                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    _logger.LogWarning("OTAC validation failed: Code {Code} has expired at {ExpiresAt}", 
                        normalizedCode, registration.OtacExpiresAt.Value);
                    return OtacResult.ExpiredCode(language);
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
                    return OtacResult.LockedCode(language);
                }

                // Update state to validated
                registration.OtacState = "Validated";
                registration.StatusMessageTh = "รหัส OTAC ผ่านการตรวจสอบแล้ว";
                registration.StatusMessageEn = "OTAC code validated";
                await _unitOfWork.SaveChangesAsync();

                var remainingAttempts = MaxValidationAttempts - registration.AttemptCount;
                var otacInfo = new OtacInfo
                {
                    Code = normalizedCode,
                    ExpiresAt = registration.OtacExpiresAt ?? now.AddMinutes(OtacExpiryMinutes),
                    RegistrationId = registration.Id,
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
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>ValidationResult indicating whether the code is valid</returns>
        public async Task<ValidationResult> IsValidAsync(string code, string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    var requiredMessage = language.ToLower() == "th" 
                        ? "จำเป็นต้องระบุรหัส OTAC" 
                        : "OTAC code is required";
                    return ValidationResult.Invalid(requiredMessage);
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    var formatMessage = language.ToLower() == "th" 
                        ? "รูปแบบรหัส OTAC ไม่ถูกต้อง" 
                        : "Invalid OTAC code format";
                    return ValidationResult.Invalid(formatMessage);
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);
                var now = _dateTimeProvider.UtcNow;

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    var notFoundMessage = language.ToLower() == "th" 
                        ? "ไม่พบรหัส OTAC" 
                        : "OTAC code not found";
                    return ValidationResult.Invalid(notFoundMessage);
                }

                if (registration.IsLocked)
                {
                    var lockedMessage = language.ToLower() == "th" 
                        ? "รหัส OTAC ถูกล็อก" 
                        : "OTAC code is locked";
                    return ValidationResult.Invalid(lockedMessage);
                }

                if (registration.OtacExpiresAt.HasValue && registration.OtacExpiresAt.Value <= now)
                {
                    var expiredMessage = language.ToLower() == "th" 
                        ? "รหัส OTAC หมดอายุแล้ว" 
                        : "OTAC code has expired";
                    return ValidationResult.Invalid(expiredMessage);
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
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult containing code information or failure if not found</returns>
        public async Task<OtacResult> GetInfoAsync(string code, string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    var requiredMessage = language.ToLower() == "th" 
                        ? "จำเป็นต้องระบุรหัส OTAC" 
                        : "OTAC code is required";
                    return OtacResult.Failure(requiredMessage);
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    var formatMessage = language.ToLower() == "th" 
                        ? "รูปแบบรหัส OTAC ไม่ถูกต้อง" 
                        : "Invalid OTAC code format";
                    return OtacResult.Failure(formatMessage);
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    return OtacResult.NotFound(language);
                }

                var remainingAttempts = Math.Max(0, MaxValidationAttempts - registration.AttemptCount);
                var otacInfo = new OtacInfo
                {
                    Code = normalizedCode,
                    ExpiresAt = registration.OtacExpiresAt ?? _dateTimeProvider.UtcNow.AddMinutes(OtacExpiryMinutes),
                    RegistrationId = registration.Id,
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
                        registration.StatusMessageTh = "รหัส OTAC หมดอายุแล้ว";
                        registration.StatusMessageEn = "OTAC code expired";
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

        /// <summary>
        /// Generates a new OTAC code for a registration (API compatible method)
        /// </summary>
        /// <param name="registrationId">Registration ID for generating OTAC</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with OTAC information</returns>
        public async Task<Result<OtacInfo>> GenerateOtacAsync(int registrationId, string language = "en")
        {
            try
            {
                var otacResult = await GenerateAsync(registrationId, "Registration", language);
                if (otacResult.IsSuccess)
                {
                    return Result<OtacInfo>.Success(otacResult.Data);
                }
                return Result<OtacInfo>.Failure(otacResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateOtacAsync for registration {RegistrationId}", registrationId);
                return Result<OtacInfo>.Failure($"Failed to generate OTAC: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates an OTAC code (API compatible method)
        /// </summary>
        /// <param name="code">The OTAC code to validate</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with validation information</returns>
        public async Task<Result<OtacInfo>> ValidateOtacAsync(string code, string language = "en")
        {
            try
            {
                var otacResult = await ValidateAsync(code, "API", language);
                if (otacResult.IsSuccess)
                {
                    return Result<OtacInfo>.Success(otacResult.Data);
                }
                return Result<OtacInfo>.Failure(otacResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ValidateOtacAsync for code {Code}", code);
                return Result<OtacInfo>.Failure($"Failed to validate OTAC: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves OTAC information (API compatible method)
        /// </summary>
        /// <param name="code">The OTAC code</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with OTAC information</returns>
        public async Task<Result<OtacInfo>> GetOtacInfoAsync(string code, string language = "en")
        {
            try
            {
                var otacResult = await GetInfoAsync(code, language);
                if (otacResult.IsSuccess)
                {
                    return Result<OtacInfo>.Success(otacResult.Data);
                }
                return Result<OtacInfo>.Failure(otacResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOtacInfoAsync for code {Code}", code);
                return Result<OtacInfo>.Failure($"Failed to get OTAC info: {ex.Message}");
            }
        }

        /// <summary>
        /// Invalidates an OTAC code
        /// </summary>
        /// <param name="code">The OTAC code to invalidate</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result indicating success/failure</returns>
        public async Task<Result> InvalidateOtacAsync(string code, string language = "en")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    var requiredMessage = language.ToLower() == "th" 
                        ? "จำเป็นต้องระบุรหัส OTAC" 
                        : "OTAC code is required";
                    return Result.Failure(requiredMessage);
                }

                if (!_otacCodeGenerator.IsValidFormat(code))
                {
                    var formatMessage = language.ToLower() == "th" 
                        ? "รูปแบบรหัส OTAC ไม่ถูกต้อง" 
                        : "Invalid OTAC code format";
                    return Result.Failure(formatMessage);
                }

                var normalizedCode = _otacCodeGenerator.NormalizeCode(code);
                var now = _dateTimeProvider.UtcNow;

                var registration = await _unitOfWork.KbankOddRegistrations
                    .QueryWithTracking()
                    .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

                if (registration == null)
                {
                    var notFoundMessage = language.ToLower() == "th" 
                        ? "ไม่พบรหัส OTAC" 
                        : "OTAC code not found";
                    return Result.Failure(notFoundMessage);
                }

                if (registration.OtacState == "Invalidated")
                {
                    var alreadyInvalidatedMessage = language.ToLower() == "th" 
                        ? "รหัส OTAC ถูกยกเลิกแล้ว" 
                        : "OTAC code is already invalidated";
                    return Result.Failure(alreadyInvalidatedMessage);
                }

                registration.OtacState = "Invalidated";
                registration.UpdatedAt = now;
                registration.IsLocked = true;
                registration.StatusMessageTh = "รหัส OTAC ถูกยกเลิก";
                registration.StatusMessageEn = "OTAC code invalidated";

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("OTAC {Code} invalidated successfully", normalizedCode);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating OTAC code {Code}", code);
                return Result.Failure($"Failed to invalidate OTAC: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets OTAC statistics for a given time period
        /// </summary>
        /// <param name="period">Time period for statistics</param>
        /// <returns>Result with OTAC statistics</returns>
        public async Task<Result<OtacStatistics>> GetOtacStatisticsAsync(TimeSpan period)
        {
            try
            {
                var now = _dateTimeProvider.UtcNow;
                var periodStart = now.Subtract(period);

                var registrations = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= now)
                    .ToListAsync();

                var totalGenerated = registrations.Count;
                var totalValidated = registrations.Count(r => r.OtacState == "Validated" || r.OtacState == "Used");
                var totalExpired = registrations.Count(r => r.OtacState == "Expired");
                var totalLocked = registrations.Count(r => r.IsLocked);
                var totalInvalidated = registrations.Count(r => r.OtacState == "Invalidated");

                var averageAttempts = registrations.Any() 
                    ? (decimal)registrations.Sum(r => r.AttemptCount) / registrations.Count 
                    : 0;

                var statistics = new OtacStatistics
                {
                    TotalGenerated = totalGenerated,
                    TotalValidated = totalValidated,
                    TotalExpired = totalExpired,
                    TotalLocked = totalLocked,
                    TotalInvalidated = totalInvalidated,
                    AverageAttempts = averageAttempts,
                    Period = period,
                    PeriodStart = periodStart,
                    PeriodEnd = now
                };

                _logger.LogInformation("Generated OTAC statistics for period {Period}: {TotalGenerated} generated, {TotalValidated} validated", 
                    period, totalGenerated, totalValidated);

                return Result<OtacStatistics>.Success(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating OTAC statistics for period {Period}", period);
                return Result<OtacStatistics>.Failure($"Failed to generate OTAC statistics: {ex.Message}");
            }
        }
    }
}