using System.Threading.Tasks;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing One-Time Access Codes (OTAC) lifecycle
    /// Handles code generation, validation, expiry, and security tracking
    /// </summary>
    public interface IOtacManagementService
    {
        /// <summary>
        /// Generates a new 8-character OTAC code for the specified user and purpose
        /// </summary>
        /// <param name="userId">ID of the user generating the OTAC</param>
        /// <param name="purpose">Purpose of the OTAC (e.g., "Registration", "Verification")</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult containing the generated code and expiry information</returns>
        Task<OtacResult> GenerateAsync(int userId, string purpose = "Registration", string language = "en");

        /// <summary>
        /// Validates an OTAC code and tracks the validation attempt
        /// </summary>
        /// <param name="code">The OTAC code to validate (case-insensitive)</param>
        /// <param name="clientIp">IP address of the client making the validation request</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult indicating validation success/failure with remaining attempts</returns>
        Task<OtacResult> ValidateAsync(string code, string clientIp, string language = "en");

        /// <summary>
        /// Checks if an OTAC code is valid without incrementing attempt counter
        /// </summary>
        /// <param name="code">The OTAC code to check</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>ValidationResult indicating whether the code is valid</returns>
        Task<ValidationResult> IsValidAsync(string code, string language = "en");

        /// <summary>
        /// Retrieves information about an OTAC code without affecting its state
        /// </summary>
        /// <param name="code">The OTAC code to retrieve information for</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>OtacResult containing code information or failure if not found</returns>
        Task<OtacResult> GetInfoAsync(string code, string language = "en");

        /// <summary>
        /// Removes expired OTAC codes from the system
        /// </summary>
        /// <returns>Result indicating success with error details if purge fails</returns>
        Task<Result> PurgeExpiredAsync();

        /// <summary>
        /// Generates a new OTAC code for a registration (API compatible method)
        /// </summary>
        /// <param name="registrationId">Registration ID for generating OTAC</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with OTAC information</returns>
        Task<Result<OtacInfo>> GenerateOtacAsync(int registrationId, string language = "en");

        /// <summary>
        /// Validates an OTAC code (API compatible method)
        /// </summary>
        /// <param name="code">The OTAC code to validate</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with validation information</returns>
        Task<Result<OtacInfo>> ValidateOtacAsync(string code, string language = "en");

        /// <summary>
        /// Retrieves OTAC information (API compatible method)
        /// </summary>
        /// <param name="code">The OTAC code</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result with OTAC information</returns>
        Task<Result<OtacInfo>> GetOtacInfoAsync(string code, string language = "en");

        /// <summary>
        /// Invalidates an OTAC code
        /// </summary>
        /// <param name="code">The OTAC code to invalidate</param>
        /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
        /// <returns>Result indicating success/failure</returns>
        Task<Result> InvalidateOtacAsync(string code, string language = "en");

        /// <summary>
        /// Gets OTAC statistics for a given time period
        /// </summary>
        /// <param name="period">Time period for statistics</param>
        /// <returns>Result with OTAC statistics</returns>
        Task<Result<OtacStatistics>> GetOtacStatisticsAsync(TimeSpan period);
    }
}