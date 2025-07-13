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
    /// <returns>URL to redirect user to KBank's registration page</returns>
    Task<string> StartRegistrationRedirectUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes status update callback from KBank
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    Task<StatusProcessResult> ProcessStatusUpdateAsync(StatusUpdateDto dto, CancellationToken cancellationToken = default);
}
