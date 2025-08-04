using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Models.Requests;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service interface for managing KBank ODD registration lifecycle
    /// Handles registration initiation, status updates, and state management
    /// </summary>
    public interface IRegistrationManagementService
    {
        /// <summary>
        /// Initiates a new KBank ODD registration after validating the provided data
        /// </summary>
        /// <param name="request">Registration request containing user data and OTAC code</param>
        /// <returns>RegistrationResult containing redirect URL and registration details</returns>
        Task<RegistrationResult> StartAsync(RegistrationRequest request);

        /// <summary>
        /// Updates the status of an existing registration (typically called by KBank webhook)
        /// </summary>
        /// <param name="regId">KBank registration ID</param>
        /// <param name="status">New status (Success, Fail, etc.)</param>
        /// <param name="returnCode">KBank return code (optional)</param>
        /// <param name="espaId">ESPA ID for successful registrations (optional)</param>
        /// <returns>Result indicating success or failure of the status update</returns>
        Task<Result> UpdateStatusAsync(string regId, string status, string? returnCode = null, string? espaId = null);

        /// <summary>
        /// Retrieves a registration by external reference
        /// </summary>
        /// <param name="externalRef">BizConnect external reference (BIZyyyyMMddHHmmssfff format)</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        Task<Result<KbankOddRegistration>> GetByExternalRefAsync(string externalRef);

        /// <summary>
        /// Retrieves a registration by KBank registration ID
        /// </summary>
        /// <param name="regId">KBank registration ID</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        Task<Result<KbankOddRegistration>> GetByRegIdAsync(string regId);
    }
}