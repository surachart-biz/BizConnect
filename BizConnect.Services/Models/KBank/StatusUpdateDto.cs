using System.ComponentModel.DataAnnotations;

namespace BizConnect.Services.Models.KBank;

/// <summary>
/// DTO for KBank status update callback
/// Based on KBank API specification section 4
/// </summary>
public class StatusUpdateDto
{
    /// <summary>
    /// External System Reference Number
    /// </summary>
    [Required]
    [StringLength(20)]
    public string ExternalReference { get; set; } = null!;

    /// <summary>
    /// Payer Short Name - Only applicable for newly registered Payer, otherwise blank
    /// </summary>
    [StringLength(30)]
    public string? PayerShortName { get; set; }

    /// <summary>
    /// ESPA ID - Only returned if registration successful
    /// </summary>
    [StringLength(100)]
    public string? EspaId { get; set; }

    /// <summary>
    /// Payer account - Conditional depending on setup
    /// </summary>
    [StringLength(20)]
    public string? PayerAccount { get; set; }

    /// <summary>
    /// Customer ID - Conditional depending on setup
    /// </summary>
    [StringLength(64)]
    public string? CustomerId { get; set; }

    /// <summary>
    /// Timestamp when response generated from PG
    /// Format: YYYYMMDDHHmmss
    /// </summary>
    [Required]
    [StringLength(14)]
    public string Timestamp { get; set; } = null!;

    /// <summary>
    /// PG return status
    /// Possible values: 0 - Success, 1 - Fail
    /// </summary>
    [Required]
    [StringLength(1)]
    public string ReturnStatus { get; set; } = null!;

    /// <summary>
    /// Return Code from KBank
    /// </summary>
    [Required]
    [StringLength(5)]
    public string ReturnCode { get; set; } = null!;

    /// <summary>
    /// Return Message from KBank
    /// </summary>
    [Required]
    [StringLength(256)]
    public string ReturnMessage { get; set; } = null!;

    /// <summary>
    /// Authentication parameter SHA-256 (pass phrase ++ external_reference ++ timestamp ++ return status ++ return_code)
    /// No space and comma
    /// </summary>
    [Required]
    public string AuthParameter { get; set; } = null!;
}

/// <summary>
/// Enum for status processing results
/// </summary>
public enum StatusProcessResult
{
    Success,
    Fail,
    Unauthorized,
    NotFound
}
