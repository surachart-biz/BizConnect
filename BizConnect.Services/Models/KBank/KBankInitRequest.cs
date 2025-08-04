using System.ComponentModel.DataAnnotations;

namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Request model for KBank Online Direct Debit registration initialization
/// Based on KBank API specification section 2.1
/// </summary>
public class KBankInitRequest
{
    /// <summary>
    /// Transaction Service Type - Fixed value "0600" for Online Register Initialization Request
    /// </summary>
    [Required]
    [StringLength(4)]
    public string TransactionType { get; set; } = "0600";

    /// <summary>
    /// Encoding - Possible values: UTF8, TIS620
    /// </summary>
    [Required]
    [StringLength(15)]
    public string Encoding { get; set; } = "UTF8";

    /// <summary>
    /// External System Short Name
    /// </summary>
    [Required]
    [StringLength(12)]
    public string ExternalSystem { get; set; } = null!;

    /// <summary>
    /// Payee Short Name - Optional consideration for supporting company short name registration
    /// </summary>
    [StringLength(12)]
    public string? PayeeShortName { get; set; }


    /// <summary>
    /// Registrant's user mobile no - Mandatory if service name matched and field has been set up as mandatory in service setup
    /// </summary>
    [StringLength(30)]
    public string? UserMobileNo { get; set; }

    /// <summary>
    /// ID used by registrant to register with the Bank
    /// Possible Values: National ID, Passport, Tax ID, etc.
    /// </summary>
    [StringLength(20)]
    public string? Id { get; set; }

    /// <summary>
    /// External System Reference Number - Must be unique among the external system
    /// </summary>
    [Required]
    [StringLength(20)]
    public string ExternalReference { get; set; } = null!;

    /// <summary>
    /// Type of service to be registered with
    /// </summary>
    [Required]
    [StringLength(80)]
    public string ServiceName { get; set; } = null!;

    /// <summary>
    /// Reference - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    public string? Reference { get; set; }

    /// <summary>
    /// Reference1 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    public string? Reference1 { get; set; }

    /// <summary>
    /// Reference2 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    public string? Reference2 { get; set; }

    /// <summary>
    /// Reference3 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    public string? Reference3 { get; set; }

    /// <summary>
    /// Reference4 - Reserve field (Not use)
    /// </summary>
    [StringLength(50)]
    public string? Reference4 { get; set; }

    /// <summary>
    /// URL for call back after register has successful
    /// </summary>
    [StringLength(2048)]
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Logo for show landing page on K+
    /// </summary>
    [StringLength(2048)]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Authentication Hash Parameter
    /// SHA-256 (pass phrase ++ external_system ++ external_reference)
    /// </summary>
    [Required]
    public string AuthParameter { get; set; } = null!;
}
