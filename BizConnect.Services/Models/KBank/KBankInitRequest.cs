using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Request model for KBank Online Direct Debit registration initialization
/// Based on KBank API specification section 2.1
/// </summary>
public class KBankInitRequest
{
    /// <summary>
    /// Transaction Service Type - Fixed value "0620" for Online Register Initialization Request
    /// </summary>
    [Required]
    [StringLength(4)]
    [JsonPropertyName("transaction_type")]
    public string TransactionType { get; set; } = "0620";

    /// <summary>
    /// Encoding - Possible values: UTF8, TIS620
    /// </summary>
    [Required]
    [StringLength(15)]
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "UTF8";

    /// <summary>
    /// External System Short Name
    /// </summary>
    [Required]
    [StringLength(12)]
    [JsonPropertyName("external_system")]
    public string ExternalSystem { get; set; } = null!;

    /// <summary>
    /// Payee Short Name - Required for authentication hash calculation (max 12 chars)
    /// </summary>
    [Required]
    [StringLength(12)]
    [JsonPropertyName("payee_short_name")]
    public string PayeeShortName { get; set; } = null!;


    /// <summary>
    /// Registrant's user mobile no - Mandatory if service name matched and field has been set up as mandatory in service setup
    /// </summary>
    [StringLength(30)]
    [JsonPropertyName("user_mobile_no")]
    public string? UserMobileNo { get; set; }

    /// <summary>
    /// ID used by registrant to register with the Bank
    /// Possible Values: National ID, Passport, Tax ID, etc.
    /// </summary>
    [StringLength(20)]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// External System Reference Number - Must be unique among the external system
    /// </summary>
    [Required]
    [StringLength(20)]
    [JsonPropertyName("external_reference")]
    public string ExternalReference { get; set; } = null!;

    /// <summary>
    /// Type of service to be registered with
    /// </summary>
    [Required]
    [StringLength(80)]
    [JsonPropertyName("service_name")]
    public string ServiceName { get; set; } = null!;

    /// <summary>
    /// Reference - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    /// <summary>
    /// Reference1 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    [JsonPropertyName("reference1")]
    public string? Reference1 { get; set; }

    /// <summary>
    /// Reference2 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    [JsonPropertyName("reference2")]
    public string? Reference2 { get; set; }

    /// <summary>
    /// Reference3 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    [JsonPropertyName("reference3")]
    public string? Reference3 { get; set; }

    /// <summary>
    /// Reference4 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    [JsonPropertyName("reference4")]
    public string? Reference4 { get; set; }

    /// <summary>
    /// URL for call back after register has successful
    /// </summary>
    [StringLength(2048)]
    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Logo for show landing page on K+
    /// </summary>
    [StringLength(2048)]
    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Authentication Hash Parameter
    /// SHA-256 (pass phrase ++ external_system ++ payee_short_name ++ external_reference)
    /// </summary>
    [Required]
    [JsonPropertyName("auth_parameter")]
    public string AuthParameter { get; set; } = null!;
}
