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
        /// <returns>OtacResult containing the generated code and expiry information</returns>
        Task<OtacResult> GenerateAsync(int userId, string purpose = "Registration");

        /// <summary>
        /// Validates an OTAC code and tracks the validation attempt
        /// </summary>
        /// <param name="code">The OTAC code to validate (case-insensitive)</param>
        /// <param name="clientIp">IP address of the client making the validation request</param>
        /// <returns>OtacResult indicating validation success/failure with remaining attempts</returns>
        Task<OtacResult> ValidateAsync(string code, string clientIp);

        /// <summary>
        /// Checks if an OTAC code is valid without incrementing attempt counter
        /// </summary>
        /// <param name="code">The OTAC code to check</param>
        /// <returns>ValidationResult indicating whether the code is valid</returns>
        Task<ValidationResult> IsValidAsync(string code);

        /// <summary>
        /// Retrieves information about an OTAC code without affecting its state
        /// </summary>
        /// <param name="code">The OTAC code to retrieve information for</param>
        /// <returns>OtacResult containing code information or failure if not found</returns>
        Task<OtacResult> GetInfoAsync(string code);

        /// <summary>
        /// Removes expired OTAC codes from the system
        /// </summary>
        /// <returns>Result indicating success with error details if purge fails</returns>
        Task<Result> PurgeExpiredAsync();
    }
}