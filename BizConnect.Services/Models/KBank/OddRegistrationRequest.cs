namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Request model for KBank ODD registration V1.9.7 - email removed as per specification
/// </summary>
public class OddRegistrationRequest
{
    /// <summary>
    /// User's full name for registration
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// User's mobile number (format: 08xxxxxxxx or +66xxxxxxxx)
    /// </summary>
    public string MobileNo { get; set; } = string.Empty;

    /// <summary>
    /// Type of identification document
    /// </summary>
    public string IdType { get; set; } = string.Empty;

    /// <summary>
    /// Identification document number/value
    /// </summary>
    public string IdValue { get; set; } = string.Empty;

    /// <summary>
    /// Bank account number for ODD registration
    /// </summary>
    public string AccountNo { get; set; } = string.Empty;

    /// <summary>
    /// Selected branch ID for the registration
    /// </summary>
    public int? BranchId { get; set; }
}
