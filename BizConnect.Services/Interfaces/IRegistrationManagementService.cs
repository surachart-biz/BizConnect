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

        /// <summary>
        /// Updates registration status (API compatible method)
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <param name="status">New status</param>
        /// <returns>Result indicating success/failure</returns>
        Task<Result> UpdateRegistrationStatusAsync(int id, string status);

        /// <summary>
        /// Processes a registration with business logic
        /// </summary>
        /// <param name="registration">Registration to process</param>
        /// <returns>Result with processing outcome</returns>
        Task<Result<KbankOddRegistration>> ProcessRegistrationAsync(KbankOddRegistration registration);

        /// <summary>
        /// Gets registration trends over time
        /// </summary>
        /// <param name="days">Number of days to analyze</param>
        /// <returns>Result with trend data</returns>
        Task<Result<RegistrationTrends>> GetRegistrationTrendsAsync(int days = 30);

        /// <summary>
        /// Submits a validated OTAC registration to KBank (Phase 3 of OTAC flow)
        /// This method specifically handles the transition from validated OTAC to KBank submission
        /// </summary>
        /// <param name="validatedOtacCode">The OTAC code that has been validated in Phase 2</param>
        /// <param name="registrationData">Guest registration form data</param>
        /// <returns>RegistrationResult containing redirect URL and external reference</returns>
        Task<RegistrationResult> SubmitAsync(string validatedOtacCode, RegistrationRequest registrationData);
    }
}