using BizConnect.Services.Models.KBank;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Interface for KBank Online Direct Debit API client
/// </summary>
public interface IKBankOddClient
{
    /// <summary>
    /// Calls KBank's registration initialization endpoint
    /// </summary>
    /// <param name="request">Initialization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Initialization response</returns>
    Task<KBankInitResponse> InitAsync(KBankInitRequest request, CancellationToken cancellationToken = default);
}
