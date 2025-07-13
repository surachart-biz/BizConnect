namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Request model for KBank ODD registration with user contact information
/// </summary>
public class OddRegistrationRequest
{
    /// <summary>
    /// User's email address for ODD registration
    /// </summary>
    public string Email { get; set; } = string.Empty;

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
}
