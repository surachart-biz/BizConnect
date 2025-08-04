using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service for managing KBank ODD registrations with integrated OTAC functionality
/// </summary>
public interface IOddRegistrationService
{
    // OTAC Management Methods
    
    /// <summary>
    /// Generates a new OTAC code and creates a KBank ODD registration record
    /// </summary>
    /// <param name="employeeUserId">ID of the employee generating the OTAC</param>
    /// <returns>Generated OTAC code</returns>
    Task<string> GenerateOtacAsync(int employeeUserId);

    /// <summary>
    /// Validates an OTAC code and updates its state
    /// </summary>
    /// <param name="otacCode">The OTAC code to validate</param>
    /// <param name="clientIp">Client IP address for security logging</param>
    /// <returns>Validation result with success status and error message</returns>
    Task<(bool IsValid, string ErrorMessage)> ValidateOtacAsync(string otacCode, string clientIp);

    /// <summary>
    /// Checks if an OTAC code is valid (not expired, not locked, in validated state)
    /// </summary>
    /// <param name="otacCode">The OTAC code to check</param>
    /// <returns>True if valid and can be used for registration</returns>
    Task<bool> IsOtacValidAsync(string otacCode);

    /// <summary>
    /// Gets a specific registration by OTAC code
    /// </summary>
    /// <param name="otacCode">OTAC code</param>
    /// <returns>Registration record or null if not found</returns>
    Task<KbankOddRegistration?> GetRegistrationByOtacAsync(string otacCode);

    // Registration Management Methods

    /// <summary>
    /// Starts the registration process by submitting form data to KBank
    /// </summary>
    /// <param name="otacCode">Validated OTAC code</param>
    /// <param name="formData">Registration form data</param>
    /// <returns>Result with success status and error message</returns>
    Task<(bool IsSuccess, string ErrorMessage)> StartRegistrationAsync(string otacCode, RegistrationFormData formData);

    /// <summary>
    /// Updates registration status from KBank callback
    /// </summary>
    /// <param name="regId">Registration ID from KBank</param>
    /// <param name="status">New status</param>
    /// <param name="returnCode">Return code from KBank</param>
    /// <param name="espaId">ESPA ID if successful</param>
    Task UpdateRegistrationStatusAsync(string regId, string status, string? returnCode = null, string? espaId = null);

    /// <summary>
    /// Gets a specific registration by registration ID
    /// </summary>
    /// <param name="regId">KBank registration ID</param>
    /// <returns>Registration record or null if not found</returns>
    Task<KbankOddRegistration?> GetRegistrationByRegIdAsync(string regId);

    // Admin Management Methods

    /// <summary>
    /// Gets paginated list of ODD registrations with optional filtering
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of records per page</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="search">Optional search term (email, mobile, external reference)</param>
    /// <returns>Paginated list of registrations with metadata</returns>
    Task<OddRegistrationPagedResult> GetRegistrationsAsync(int page = 1, int pageSize = 10, string? status = null, string? search = null);

    /// <summary>
    /// Gets a specific ODD registration by ID
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <returns>Registration details or null if not found</returns>
    Task<KbankOddRegistration?> GetRegistrationByIdAsync(int id);

    /// <summary>
    /// Updates the status of an ODD registration
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <param name="status">New status</param>
    /// <returns>True if update was successful, false if registration not found</returns>
    Task<bool> UpdateRegistrationStatusAsync(int id, string status);

    /// <summary>
    /// Deletes an ODD registration
    /// </summary>
    /// <param name="id">Registration ID</param>
    /// <returns>True if deletion was successful, false if registration not found</returns>
    Task<bool> DeleteRegistrationAsync(int id);

    /// <summary>
    /// Gets all ODD registrations for export purposes
    /// </summary>
    /// <returns>List of all registrations ordered by creation date (descending)</returns>
    Task<List<KbankOddRegistration>> GetAllRegistrationsForExportAsync();

    // Background Job Methods

    /// <summary>
    /// Purges expired OTAC codes (background job)
    /// </summary>
    /// <returns>Number of records purged</returns>
    Task<int> PurgeExpiredOtacCodesAsync();
}

/// <summary>
/// Model containing paginated ODD registration results with metadata
/// </summary>
public class OddRegistrationPagedResult
{
    public List<KbankOddRegistration> Registrations { get; set; } = new List<KbankOddRegistration>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public string StatusFilter { get; set; } = "";
    public string SearchQuery { get; set; } = "";
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    
    public int StartRecord => (CurrentPage - 1) * PageSize + 1;
    public int EndRecord => Math.Min(CurrentPage * PageSize, TotalRecords);
}

/// <summary>
/// Data transfer object for registration form data
/// </summary>
public class RegistrationFormData
{
    public string FullName { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public string IdValue { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public int BranchId { get; set; }
}