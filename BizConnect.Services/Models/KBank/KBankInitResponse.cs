using System.Text.Json.Serialization;

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
    [JsonPropertyName("reg_id")]
    public string? RegId { get; set; }

    /// <summary>
    /// Return status from KBank
    /// Possible values: 0 - Success, 1 - Fail
    /// </summary>
    [JsonPropertyName("return_status")]
    public string ReturnStatus { get; set; } = null!;

    /// <summary>
    /// Return code from KBank
    /// </summary>
    [JsonPropertyName("return_code")]
    public string ReturnCode { get; set; } = null!;

    /// <summary>
    /// Return message from KBank
    /// </summary>
    [JsonPropertyName("return_message")]
    public string ReturnMessage { get; set; } = null!;
}
