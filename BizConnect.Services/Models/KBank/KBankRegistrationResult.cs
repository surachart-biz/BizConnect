namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Result model for pure KBank ODD registration API operations
/// Contains all data needed by the calling service to handle database operations
/// </summary>
public class KBankRegistrationResult
{
    /// <summary>
    /// Indicates if the KBank API call was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Registration Token ID returned by KBank (when successful)
    /// </summary>
    public string? RegId { get; set; }

    /// <summary>
    /// Generated external reference for the registration
    /// </summary>
    public string ExternalReference { get; set; } = string.Empty;

    /// <summary>
    /// Return status from KBank API
    /// Possible values: 0 - Success, 1 - Fail
    /// </summary>
    public string? ReturnStatus { get; set; }

    /// <summary>
    /// Return code from KBank API
    /// </summary>
    public string? ReturnCode { get; set; }

    /// <summary>
    /// Return message from KBank API
    /// </summary>
    public string? ReturnMessage { get; set; }

    /// <summary>
    /// Complete redirect URL to KBank's registration page (when successful)
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// Error message for failed operations
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static KBankRegistrationResult Success(string externalReference, string regId, string redirectUrl)
    {
        return new KBankRegistrationResult
        {
            IsSuccess = true,
            ExternalReference = externalReference,
            RegId = regId,
            RedirectUrl = redirectUrl,
            ReturnStatus = "0"
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static KBankRegistrationResult Failure(string externalReference, string errorMessage, 
        string? returnStatus = null, string? returnCode = null, string? returnMessage = null)
    {
        return new KBankRegistrationResult
        {
            IsSuccess = false,
            ExternalReference = externalReference,
            ErrorMessage = errorMessage,
            ReturnStatus = returnStatus,
            ReturnCode = returnCode,
            ReturnMessage = returnMessage
        };
    }
}