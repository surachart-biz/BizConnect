using System.Text.Json.Serialization;

namespace BizConnect.Models.Api;

/// <summary>
/// Data Transfer Object for OTAC (One-Time Access Code) operations in the API.
/// Contains all necessary information for OTAC generation, validation, and status tracking.
/// </summary>
public class OtacDto
{
    /// <summary>
    /// The generated OTAC code (8 characters, alphanumeric, excluding confusing characters)
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The registration ID associated with this OTAC
    /// </summary>
    [JsonPropertyName("registrationId")]
    public int RegistrationId { get; set; }

    /// <summary>
    /// When the OTAC was generated (UTC)
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// When the OTAC expires (UTC)
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Current status of the OTAC (GENERATED, VALIDATED, EXPIRED, LOCKED)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of validation attempts made
    /// </summary>
    [JsonPropertyName("attemptCount")]
    public int AttemptCount { get; set; }

    /// <summary>
    /// Maximum number of validation attempts allowed
    /// </summary>
    [JsonPropertyName("maxAttempts")]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Time remaining until expiration (in seconds)
    /// </summary>
    [JsonPropertyName("timeRemainingSeconds")]
    public int TimeRemainingSeconds => Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds);

    /// <summary>
    /// Indicates if the OTAC has expired
    /// </summary>
    [JsonPropertyName("isExpired")]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Indicates if the OTAC is locked due to too many failed attempts
    /// </summary>
    [JsonPropertyName("isLocked")]
    public bool IsLocked => AttemptCount >= MaxAttempts;

    /// <summary>
    /// Indicates if the OTAC is still valid and can be used
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid => !IsExpired && !IsLocked && Status == "GENERATED";

    /// <summary>
    /// Creates an OTAC DTO from domain model data
    /// </summary>
    public static OtacDto FromRegistration(int registrationId, string code, DateTime generatedAt, DateTime expiresAt, int attemptCount = 0)
    {
        return new OtacDto
        {
            RegistrationId = registrationId,
            Code = code,
            GeneratedAt = generatedAt,
            ExpiresAt = expiresAt,
            Status = "GENERATED",
            AttemptCount = attemptCount
        };
    }
}

/// <summary>
/// Request model for generating a new OTAC
/// </summary>
public class GenerateOtacRequest
{
    /// <summary>
    /// Optional existing registration ID to generate OTAC for (if resuming a registration)
    /// </summary>
    [JsonPropertyName("registrationId")]
    public int? RegistrationId { get; set; }

    /// <summary>
    /// Client information for rate limiting and audit purposes
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// Request model for validating an OTAC
/// </summary>
public class ValidateOtacRequest
{
    /// <summary>
    /// The OTAC code to validate (required, 8 characters)
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Client information for security tracking
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// Response model for OTAC validation
/// </summary>
public class ValidateOtacResponse
{
    /// <summary>
    /// Indicates if the validation was successful
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// The registration ID associated with the validated OTAC
    /// </summary>
    [JsonPropertyName("registrationId")]
    public int? RegistrationId { get; set; }

    /// <summary>
    /// Detailed reason for validation failure (if applicable)
    /// </summary>
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }

    /// <summary>
    /// Number of remaining validation attempts
    /// </summary>
    [JsonPropertyName("remainingAttempts")]
    public int RemainingAttempts { get; set; }

    /// <summary>
    /// Time until the OTAC expires (in seconds)
    /// </summary>
    [JsonPropertyName("timeRemainingSeconds")]
    public int TimeRemainingSeconds { get; set; }

    /// <summary>
    /// Creates a successful validation response
    /// </summary>
    public static ValidateOtacResponse Success(int registrationId, int timeRemainingSeconds)
    {
        return new ValidateOtacResponse
        {
            IsValid = true,
            RegistrationId = registrationId,
            TimeRemainingSeconds = timeRemainingSeconds
        };
    }

    /// <summary>
    /// Creates a failed validation response
    /// </summary>
    public static ValidateOtacResponse Failure(string reason, int remainingAttempts, int timeRemainingSeconds = 0)
    {
        return new ValidateOtacResponse
        {
            IsValid = false,
            FailureReason = reason,
            RemainingAttempts = remainingAttempts,
            TimeRemainingSeconds = timeRemainingSeconds
        };
    }
}

/// <summary>
/// Client information for security and audit purposes
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// Client IP address
    /// </summary>
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Client user agent string
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Session identifier (if available)
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Additional client metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}