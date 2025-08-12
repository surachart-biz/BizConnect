namespace BizConnect.Services.Models.KBank;

/// <summary>
/// Result model for pure status validation operations
/// Contains validation result without database operations
/// </summary>
public class StatusValidationResult
{
    /// <summary>
    /// Indicates if the status update is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation result type
    /// </summary>
    public StatusValidationType ResultType { get; set; }

    /// <summary>
    /// External reference from the status update
    /// </summary>
    public string ExternalReference { get; set; } = string.Empty;

    /// <summary>
    /// Processed status update data (when valid)
    /// </summary>
    public StatusUpdateDto? StatusUpdate { get; set; }

    /// <summary>
    /// Error message for validation failures
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static StatusValidationResult Success(StatusUpdateDto statusUpdate)
    {
        return new StatusValidationResult
        {
            IsValid = true,
            ResultType = StatusValidationType.Valid,
            ExternalReference = statusUpdate.ExternalReference,
            StatusUpdate = statusUpdate
        };
    }

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    public static StatusValidationResult Failure(StatusValidationType resultType, string externalReference, string errorMessage)
    {
        return new StatusValidationResult
        {
            IsValid = false,
            ResultType = resultType,
            ExternalReference = externalReference,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Types of status validation results
/// </summary>
public enum StatusValidationType
{
    Valid,
    InvalidAuthentication,
    MissingPassPhrase,
    InvalidData
}