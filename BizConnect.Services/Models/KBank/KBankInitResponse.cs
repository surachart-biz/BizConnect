namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Response model for KBank Online Direct Debit registration initialization
/// Based on KBank API specification section 2.2
/// </summary>
public class KBankInitResponse
{
    /// <summary>
    /// Registration Token ID returned by KBank
    /// </summary>
    public string? RegId { get; set; }

    /// <summary>
    /// Return status from KBank
    /// Possible values: 0 - Success, 1 - Fail
    /// </summary>
    public string ReturnStatus { get; set; } = null!;

    /// <summary>
    /// Return code from KBank
    /// </summary>
    public string ReturnCode { get; set; } = null!;

    /// <summary>
    /// Return message from KBank
    /// </summary>
    public string ReturnMessage { get; set; } = null!;
}
