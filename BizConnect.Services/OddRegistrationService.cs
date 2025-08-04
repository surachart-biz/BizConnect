using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.Services.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service for managing KBank ODD registrations with integrated OTAC functionality
/// </summary>
public class OddRegistrationService : IOddRegistrationService
{
    private readonly BizConnectContext _context;
    private readonly IKbankOddService _kbankOddService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OddRegistrationService> _logger;

    private const int OtacExpiryMinutes = 30;
    private const int MaxAttempts = 5;

    public OddRegistrationService(
        BizConnectContext context,
        IKbankOddService kbankOddService,
        IConfiguration configuration,
        ILogger<OddRegistrationService> logger)
    {
        _context = context;
        _kbankOddService = kbankOddService;
        _configuration = configuration;
        _logger = logger;
    }

    #region OTAC Management Methods

    /// <summary>
    /// Generates a new OTAC code and creates a KBank ODD registration record
    /// </summary>
    /// <param name="employeeUserId">ID of the employee generating the OTAC</param>
    /// <returns>Generated KBank ODD registration record with OTAC</returns>
    public async Task<KbankOddRegistration> GenerateOtacAsync(int employeeUserId)
    {
        try
        {
            _logger.LogInformation("Generating OTAC for employee user ID: {UserId}", employeeUserId);

            // Generate unique OTAC code
            string otacCode;
            int attempts = 0;
            const int maxGenerationAttempts = 10;

            do
            {
                otacCode = OtacUtils.GenerateOtacCode();
                attempts++;

                if (attempts > maxGenerationAttempts)
                {
                    _logger.LogError("Failed to generate unique OTAC after {Attempts} attempts", maxGenerationAttempts);
                    throw new InvalidOperationException("Unable to generate unique OTAC code");
                }
            }
            while (await _context.KbankOddRegistrations.AnyAsync(r => r.OtacCode == otacCode));

            // Create registration record with OTAC (Status = null initially, ExternalReference = null until form submission)
            var registration = new KbankOddRegistration
            {
                ExternalReference = string.Empty, // Will be set when form is submitted
                RegId = string.Empty, // Will be set when KBank API is called
                Status = null, // No status initially - will be set to "Pending" after form submission
                CreatedAt = DateTime.UtcNow,
                OtacCode = otacCode,
                OtacState = "Generated",
                GeneratedByUserId = employeeUserId,
                AttemptCount = 0,
                IsLocked = false,
                OtacExpiresAt = DateTime.UtcNow.AddMinutes(OtacExpiryMinutes)
            };

            _context.KbankOddRegistrations.Add(registration);
            await _context.SaveChangesAsync();

            _logger.LogInformation("OTAC generated successfully: {OtacCode} for user {UserId}, expires at {ExpiresAt}",
                otacCode, employeeUserId, registration.OtacExpiresAt);

            return registration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate OTAC for employee user ID: {UserId}", employeeUserId);
            throw;
        }
    }

    /// <summary>
    /// Validates an OTAC code and updates its state
    /// </summary>
    /// <param name="otacCode">The OTAC code to validate</param>
    /// <param name="clientIp">Client IP address for security logging</param>
    /// <returns>Validation result with success status and error message</returns>
    public async Task<(bool IsValid, string ErrorMessage)> ValidateOtacAsync(string otacCode, string clientIp)
    {
        try
        {
            if (!OtacUtils.IsValidOtacFormat(otacCode))
            {
                _logger.LogWarning("Invalid OTAC format attempted: {OtacCode} from IP: {ClientIp}", otacCode, clientIp);
                return (false, "รหัส OTAC ไม่ถูกต้อง");
            }

            var normalizedCode = OtacUtils.NormalizeOtacCode(otacCode);
            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

            if (registration == null)
            {
                _logger.LogWarning("OTAC not found: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
                return (false, "ไม่พบรหัส OTAC นี้");
            }

            // Update attempt tracking
            registration.AttemptCount++;
            registration.LastAttemptAt = DateTime.UtcNow;
            registration.LastAttemptIp = clientIp;

            // Check if locked
            if (registration.IsLocked)
            {
                await _context.SaveChangesAsync();
                _logger.LogWarning("OTAC validation attempted on locked code: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
                return (false, "รหัส OTAC นี้ถูกล็อกเนื่องจากใส่ผิดหลายครั้ง");
            }

            // Check expiry
            if (registration.OtacExpiresAt.HasValue && DateTime.UtcNow > registration.OtacExpiresAt.Value)
            {
                await _context.SaveChangesAsync();
                _logger.LogWarning("Expired OTAC validation attempted: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
                return (false, "รหัส OTAC หมดอายุแล้ว");
            }

            // Check if already used
            if (registration.OtacState == "Used")
            {
                await _context.SaveChangesAsync();
                _logger.LogWarning("Already used OTAC validation attempted: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
                return (false, "รหัส OTAC นี้ถูกใช้งานแล้ว");
            }

            // Check max attempts and lock if exceeded
            if (registration.AttemptCount > MaxAttempts)
            {
                registration.IsLocked = true;
                await _context.SaveChangesAsync();
                _logger.LogWarning("OTAC locked due to too many attempts: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
                return (false, "รหัส OTAC ถูกล็อกเนื่องจากใส่ผิดหลายครั้ง");
            }

            // Validation successful - update state
            registration.OtacState = "Validated";
            await _context.SaveChangesAsync();

            _logger.LogInformation("OTAC validated successfully: {OtacCode} from IP: {ClientIp}", normalizedCode, clientIp);
            return (true, "รหัส OTAC ถูกต้อง");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate OTAC: {OtacCode} from IP: {ClientIp}", otacCode, clientIp);
            return (false, "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง");
        }
    }

    /// <summary>
    /// Checks if an OTAC code is valid (not expired, not locked, in validated state)
    /// </summary>
    /// <param name="otacCode">The OTAC code to check</param>
    /// <returns>True if valid and can be used for registration</returns>
    public async Task<bool> IsOtacValidAsync(string otacCode)
    {
        try
        {
            if (!OtacUtils.IsValidOtacFormat(otacCode))
                return false;

            var normalizedCode = OtacUtils.NormalizeOtacCode(otacCode);
            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

            if (registration == null || registration.IsLocked || registration.OtacState != "Validated")
                return false;

            // Check expiry
            if (registration.OtacExpiresAt.HasValue && DateTime.UtcNow > registration.OtacExpiresAt.Value)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check OTAC validity: {OtacCode}", otacCode);
            return false;
        }
    }

    /// <summary>
    /// Gets a specific registration by OTAC code
    /// </summary>
    /// <param name="otacCode">OTAC code</param>
    /// <returns>Registration record or null if not found</returns>
    public async Task<KbankOddRegistration?> GetRegistrationByOtacAsync(string otacCode)
    {
        try
        {
            if (!OtacUtils.IsValidOtacFormat(otacCode))
                return null;

            var normalizedCode = OtacUtils.NormalizeOtacCode(otacCode);
            return await _context.KbankOddRegistrations
                .Include(r => r.Branch)
                .Include(r => r.GeneratedByUser)
                .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get registration by OTAC: {OtacCode}", otacCode);
            return null;
        }
    }

    #endregion

    #region Registration Management Methods

    /// <summary>
    /// Starts the registration process by submitting form data to KBank
    /// </summary>
    /// <param name="otacCode">Validated OTAC code</param>
    /// <param name="formData">Registration form data</param>
    /// <returns>Result with success status and error message</returns>
    public async Task<(bool IsSuccess, string ErrorMessage)> StartRegistrationAsync(string otacCode, RegistrationFormData formData)
    {
        try
        {
            _logger.LogInformation("Starting registration process for OTAC: {OtacCode}", otacCode);

            // Validate OTAC first
            if (!await IsOtacValidAsync(otacCode))
            {
                _logger.LogWarning("Registration attempt with invalid OTAC: {OtacCode}", otacCode);
                return (false, "รหัส OTAC ไม่ถูกต้องหรือหมดอายุแล้ว");
            }

            var normalizedCode = OtacUtils.NormalizeOtacCode(otacCode);
            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.OtacCode == normalizedCode);

            if (registration == null)
            {
                return (false, "ไม่พบข้อมูลการลงทะเบียน");
            }

            // Update registration with form data and set ExternalReference
            registration.ExternalReference = OddUtils.GenerateExternalReference();
            registration.FullName = formData.FullName;
            registration.IdType = formData.IdType;
            registration.IdValue = formData.IdValue;
            registration.MobileNo = formData.MobileNo;
            registration.AccountNo = formData.AccountNo;
            registration.BranchId = formData.BranchId;
            registration.OtacState = "Used";
            registration.UpdatedAt = DateTime.UtcNow;

            // Create KBank request
            var kbankRequest = new OddRegistrationRequest
            {
                FullName = formData.FullName,
                MobileNo = formData.MobileNo,
                IdType = formData.IdType,
                IdValue = formData.IdValue,
                AccountNo = formData.AccountNo,
                BranchId = formData.BranchId
            };

            // Call KBank service
            var redirectUrl = await _kbankOddService.StartRegistrationWithExistingReferenceAsync(
                kbankRequest, registration.ExternalReference);

            // Extract RegId from redirect URL if needed
            if (redirectUrl.Contains("reg_id="))
            {
                var regIdStart = redirectUrl.IndexOf("reg_id=") + 7;
                var regIdEnd = redirectUrl.IndexOf("&", regIdStart);
                if (regIdEnd == -1) regIdEnd = redirectUrl.Length;
                registration.RegId = redirectUrl.Substring(regIdStart, regIdEnd - regIdStart);
            }

            registration.Status = "Pending";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Registration started successfully for OTAC: {OtacCode}, RegId: {RegId}",
                otacCode, registration.RegId);

            return (true, redirectUrl); // Return redirect URL as success message
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start registration for OTAC: {OtacCode}", otacCode);
            return (false, "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง");
        }
    }

    /// <summary>
    /// Updates registration status from KBank callback
    /// </summary>
    /// <param name="regId">Registration ID from KBank</param>
    /// <param name="status">New status</param>
    /// <param name="returnCode">Return code from KBank</param>
    /// <param name="espaId">ESPA ID if successful</param>
    public async Task UpdateRegistrationStatusAsync(string regId, string status, string? returnCode = null, string? espaId = null)
    {
        try
        {
            _logger.LogInformation("Updating registration status - RegId: {RegId}, Status: {Status}, ReturnCode: {ReturnCode}",
                regId, status, returnCode);

            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.RegId == regId);

            if (registration == null)
            {
                _logger.LogWarning("Registration not found for RegId: {RegId}", regId);
                return;
            }

            var oldStatus = registration.Status;
            registration.Status = status;
            registration.ReturnCode = returnCode;
            registration.EspaId = espaId;
            registration.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Registration status updated successfully - RegId: {RegId}, Old Status: {OldStatus}, New Status: {NewStatus}",
                regId, oldStatus, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update registration status - RegId: {RegId}, Status: {Status}", regId, status);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific registration by registration ID
    /// </summary>
    /// <param name="regId">KBank registration ID</param>
    /// <returns>Registration record or null if not found</returns>
    public async Task<KbankOddRegistration?> GetRegistrationByRegIdAsync(string regId)
    {
        try
        {
            return await _context.KbankOddRegistrations
                .Include(r => r.Branch)
                .Include(r => r.GeneratedByUser)
                .FirstOrDefaultAsync(r => r.RegId == regId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get registration by RegId: {RegId}", regId);
            return null;
        }
    }

    /// <summary>
    /// Gets a specific registration by external reference
    /// </summary>
    /// <param name="externalRef">External reference</param>
    /// <returns>Registration record or null if not found</returns>
    public async Task<KbankOddRegistration?> GetRegistrationByExternalRefAsync(string externalRef)
    {
        try
        {
            return await _context.KbankOddRegistrations
                .Include(r => r.Branch)
                .Include(r => r.GeneratedByUser)
                .FirstOrDefaultAsync(r => r.ExternalReference == externalRef);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get registration by ExternalRef: {ExternalRef}", externalRef);
            return null;
        }
    }

    #endregion

    #region Background Job Methods

    /// <summary>
    /// Purges expired OTAC codes (background job)
    /// </summary>
    /// <returns>Number of records purged</returns>
    public async Task<int> PurgeExpiredOtacCodesAsync()
    {
        try
        {
            _logger.LogInformation("Starting OTAC purge job");

            var cutoffDate = DateTime.UtcNow.AddMinutes(-OtacExpiryMinutes);

            // Find expired OTAC codes that haven't been used
            var expiredRegistrations = await _context.KbankOddRegistrations
                .Where(r => r.OtacExpiresAt.HasValue && 
                           r.OtacExpiresAt.Value < cutoffDate &&
                           r.OtacState != "Used" &&
                           (r.Status == null || r.Status == "Pending"))
                .ToListAsync();

            if (expiredRegistrations.Any())
            {
                // Soft delete by updating status
                foreach (var registration in expiredRegistrations)
                {
                    registration.Status = "Expired";
                    registration.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("OTAC purge job completed - Purged {Count} expired codes", expiredRegistrations.Count);
                return expiredRegistrations.Count;
            }

            _logger.LogInformation("OTAC purge job completed - No expired codes found");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OTAC purge job failed");
            throw;
        }
    }

    #endregion

    #region Admin Management Methods

    /// <summary>
    /// Gets paginated list of ODD registrations with optional filtering
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="search">Optional search term (full name, mobile, ID value, external reference)</param>
    /// <returns>Paginated list of registrations with metadata</returns>
    public async Task<OddRegistrationPagedResult> GetRegistrationsAsync(int page = 1, int pageSize = 10, string? status = null, string? search = null)
    {
        try
        {
            _logger.LogInformation("Fetching ODD registrations - Page: {Page}, PageSize: {PageSize}, Status: {Status}, Search: {Search}",
                page, pageSize, status, search);

            var query = _context.KbankOddRegistrations.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            // Apply search filter (V1.9.7 - search by FullName, mobile, external reference, ID value)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => 
                    (r.FullName != null && r.FullName.Contains(search)) ||
                    (r.MobileNo != null && r.MobileNo.Contains(search)) ||
                    (r.IdValue != null && r.IdValue.Contains(search)) ||
                    (r.ExternalReference != null && r.ExternalReference.Contains(search)));
            }

            // Get total count for pagination
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            // Apply pagination and ordering
            var registrations = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new OddRegistrationPagedResult
            {
                Registrations = registrations,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalRecords = totalRecords,
                StatusFilter = status ?? "",
                SearchQuery = search ?? "",
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            _logger.LogInformation("Successfully fetched {Count} ODD registrations (Page {Page} of {TotalPages})",
                registrations.Count, page, totalPages);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ODD registrations - Page: {Page}, PageSize: {PageSize}, Status: {Status}, Search: {Search}",
                page, pageSize, status, search);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific ODD registration by ID
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <returns>Registration details or null if not found</returns>
    public async Task<KbankOddRegistration?> GetRegistrationByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation("Fetching ODD registration with ID: {Id}", id);

            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                _logger.LogWarning("ODD registration not found with ID: {Id}", id);
            }
            else
            {
                _logger.LogInformation("Successfully fetched ODD registration with ID: {Id}", id);
            }

            return registration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ODD registration with ID: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Updates the status of an ODD registration
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if update was successful, false if registration not found</returns>
    public async Task<bool> UpdateRegistrationStatusAsync(int id, string status)
    {
        try
        {
            _logger.LogInformation("Updating ODD registration status - ID: {Id}, Status: {Status}", id, status);

            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                _logger.LogWarning("Cannot update status - ODD registration not found with ID: {Id}", id);
                return false;
            }

            var oldStatus = registration.Status;
            registration.Status = status;
            // Note: UpdatedAt is handled by database trigger

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated ODD registration status - ID: {Id}, Old Status: {OldStatus}, New Status: {NewStatus}",
                id, oldStatus, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ODD registration status - ID: {Id}, Status: {Status}", id, status);
            throw;
        }
    }

    /// <summary>
    /// Deletes an ODD registration
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <returns>True if deletion was successful, false if registration not found</returns>
    public async Task<bool> DeleteRegistrationAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting ODD registration with ID: {Id}", id);

            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                _logger.LogWarning("Cannot delete - ODD registration not found with ID: {Id}", id);
                return false;
            }

            _context.KbankOddRegistrations.Remove(registration);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted ODD registration with ID: {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete ODD registration with ID: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets all ODD registrations for export purposes
    /// </summary>
    /// <returns>List of all registrations ordered by creation date (descending)</returns>
    public async Task<List<KbankOddRegistration>> GetAllRegistrationsForExportAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all ODD registrations for export");

            var registrations = await _context.KbankOddRegistrations
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Successfully fetched {Count} ODD registrations for export", registrations.Count);

            return registrations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all ODD registrations for export");
            throw;
        }
    }

    #endregion
}