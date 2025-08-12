using BizConnect.Services.Models.KBank;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for KBank Online Direct Debit operations
/// </summary>
public interface IKbankOddService
{
    /// <summary>
    /// Starts the registration process and returns the redirect URL to KBank's registration page
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>URL to redirect user to KBank's registration page</returns>
    Task<string> StartRegistrationRedirectUrlAsync(CancellationToken cancellationToken = default, string language = "en");

    /// <summary>
    /// Starts the registration process with user contact information and returns the redirect URL to KBank's registration page
    /// </summary>
    /// <param name="request">Registration request containing user contact information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>URL to redirect user to KBank's registration page</returns>
    Task<string> StartRegistrationAsync(OddRegistrationRequest request, CancellationToken cancellationToken = default, string language = "en");

    /// <summary>
    /// Starts the registration process using an existing registration record and returns the redirect URL to KBank's registration page
    /// </summary>
    /// <param name="request">Registration request containing user contact information</param>
    /// <param name="existingExternalReference">External reference of the existing registration record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>URL to redirect user to KBank's registration page</returns>
    Task<string> StartRegistrationWithExistingReferenceAsync(OddRegistrationRequest request, string existingExternalReference, CancellationToken cancellationToken = default, string language = "en");

    /// <summary>
    /// Processes status update callback from KBank
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>Processing result</returns>
    Task<StatusProcessResult> ProcessStatusUpdateAsync(StatusUpdateDto dto, CancellationToken cancellationToken = default, string language = "en");

    // Phase 1: Pure API methods without database operations

    /// <summary>
    /// Pure API method: Initializes registration with KBank without database operations
    /// </summary>
    /// <param name="request">Registration request containing user contact information</param>
    /// <param name="externalReference">External reference to use (if null, will generate new)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>Registration result with KBank response data and redirect URL</returns>
    Task<KBankRegistrationResult> InitializeRegistrationAsync(OddRegistrationRequest request, 
        string? externalReference = null, CancellationToken cancellationToken = default, string language = "en");

    /// <summary>
    /// Pure validation method: Validates status update webhook without database operations
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<StatusValidationResult> ValidateStatusUpdateAsync(StatusUpdateDto dto, CancellationToken cancellationToken = default);
}
