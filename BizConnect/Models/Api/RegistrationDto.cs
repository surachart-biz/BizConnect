using System.Text.Json.Serialization;

namespace BizConnect.Models.Api;

/// <summary>
/// Data Transfer Object for KBank ODD Registration operations in the API.
/// Contains comprehensive registration information for status tracking and management.
/// </summary>
public class RegistrationDto
{
    /// <summary>
    /// Unique registration identifier
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Full name of the registrant
    /// </summary>
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Type of identification document (National ID, Passport, Tax ID)
    /// </summary>
    [JsonPropertyName("identificationType")]
    public string IdentificationType { get; set; } = string.Empty;

    /// <summary>
    /// Identification number
    /// </summary>
    [JsonPropertyName("identificationNumber")]
    public string IdentificationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mobile phone number
    /// </summary>
    [JsonPropertyName("mobileNumber")]
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Bank account number
    /// </summary>
    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Bank branch identifier
    /// </summary>
    [JsonPropertyName("branchId")]
    public int BranchId { get; set; }

    /// <summary>
    /// Bank branch name
    /// </summary>
    [JsonPropertyName("branchName")]
    public string? BranchName { get; set; }

    /// <summary>
    /// Current registration status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When the registration was created (UTC)
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the registration was last updated (UTC)
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// KBank reference number (if available)
    /// </summary>
    [JsonPropertyName("kbankReference")]
    public string? KbankReference { get; set; }

    /// <summary>
    /// Error message if registration failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates if OTAC has been generated for this registration
    /// </summary>
    [JsonPropertyName("hasOtac")]
    public bool HasOtac { get; set; }

    /// <summary>
    /// OTAC expiration time (if applicable)
    /// </summary>
    [JsonPropertyName("otacExpiresAt")]
    public DateTime? OtacExpiresAt { get; set; }

    /// <summary>
    /// Number of OTAC validation attempts
    /// </summary>
    [JsonPropertyName("otacAttempts")]
    public int OtacAttempts { get; set; }

    /// <summary>
    /// Indicates if the registration is in a final state
    /// </summary>
    [JsonPropertyName("isFinal")]
    public bool IsFinal => Status is "SUCCESS" or "FAILED" or "EXPIRED" or "CANCELLED";

    /// <summary>
    /// Indicates if the registration is still pending
    /// </summary>
    [JsonPropertyName("isPending")]
    public bool IsPending => Status is "OTAC_GENERATED" or "FORM_SUBMITTED" or "PENDING_KBANK";

    /// <summary>
    /// User-friendly status description
    /// </summary>
    [JsonPropertyName("statusDescription")]
    public string StatusDescription => Status switch
    {
        "OTAC_GENERATED" => "OTAC code generated, awaiting validation",
        "FORM_SUBMITTED" => "Registration form submitted successfully",
        "PENDING_KBANK" => "Awaiting KBank processing",
        "SUCCESS" => "Registration completed successfully",
        "FAILED" => "Registration failed",
        "EXPIRED" => "Registration expired",
        "CANCELLED" => "Registration cancelled",
        _ => "Unknown status"
    };

    /// <summary>
    /// Creates a RegistrationDto from domain model
    /// </summary>
    public static RegistrationDto FromDomainModel(BizConnect.Dal.Models.KbankOddRegistration registration, string? branchName = null)
    {
        return new RegistrationDto
        {
            Id = registration.Id,
            FullName = registration.FullName ?? string.Empty,
            IdentificationType = registration.IdType ?? string.Empty,
            IdentificationNumber = registration.IdValue ?? string.Empty,
            MobileNumber = registration.MobileNo ?? string.Empty,
            AccountNumber = registration.AccountNo ?? string.Empty,
            BranchId = registration.BranchId ?? 0,
            BranchName = branchName,
            Status = registration.Status ?? string.Empty,
            CreatedAt = registration.CreatedAt,
            UpdatedAt = registration.UpdatedAt ?? registration.CreatedAt,
            KbankReference = registration.ExternalReference,
            ErrorMessage = registration.Status == "FAILED" ? registration.ReturnCode : null,
            HasOtac = !string.IsNullOrEmpty(registration.OtacCode),
            OtacExpiresAt = registration.OtacExpiresAt,
            OtacAttempts = registration.AttemptCount
        };
    }
}

/// <summary>
/// Request model for updating registration status
/// </summary>
public class UpdateRegistrationStatusRequest
{
    /// <summary>
    /// New status to set
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optional error message (required for FAILED status)
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Optional KBank reference number
    /// </summary>
    [JsonPropertyName("kbankReference")]
    public string? KbankReference { get; set; }

    /// <summary>
    /// Additional notes about the status change
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response model for registration statistics
/// </summary>
public class RegistrationStatsDto
{
    /// <summary>
    /// Total number of registrations
    /// </summary>
    [JsonPropertyName("totalRegistrations")]
    public int TotalRegistrations { get; set; }

    /// <summary>
    /// Number of successful registrations
    /// </summary>
    [JsonPropertyName("successfulRegistrations")]
    public int SuccessfulRegistrations { get; set; }

    /// <summary>
    /// Number of failed registrations
    /// </summary>
    [JsonPropertyName("failedRegistrations")]
    public int FailedRegistrations { get; set; }

    /// <summary>
    /// Number of pending registrations
    /// </summary>
    [JsonPropertyName("pendingRegistrations")]
    public int PendingRegistrations { get; set; }

    /// <summary>
    /// Success rate as a percentage
    /// </summary>
    [JsonPropertyName("successRate")]
    public double SuccessRate => TotalRegistrations > 0 ? (double)SuccessfulRegistrations / TotalRegistrations * 100 : 0;

    /// <summary>
    /// Statistics by status
    /// </summary>
    [JsonPropertyName("statusBreakdown")]
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();

    /// <summary>
    /// Statistics by time period
    /// </summary>
    [JsonPropertyName("timeBreakdown")]
    public Dictionary<string, int> TimeBreakdown { get; set; } = new();

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Request model for registration search and filtering
/// </summary>
public class RegistrationSearchRequest : PaginationRequest
{
    /// <summary>
    /// Filter by registration status
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Filter by date range - start date
    /// </summary>
    [JsonPropertyName("fromDate")]
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Filter by date range - end date
    /// </summary>
    [JsonPropertyName("toDate")]
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Filter by branch ID
    /// </summary>
    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    /// <summary>
    /// Filter by identification type
    /// </summary>
    [JsonPropertyName("identificationType")]
    public string? IdentificationType { get; set; }

    /// <summary>
    /// Search in full name, identification number, mobile number, or account number
    /// </summary>
    [JsonPropertyName("searchTerm")]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Include expired registrations in results
    /// </summary>
    [JsonPropertyName("includeExpired")]
    public bool IncludeExpired { get; set; } = true;

    /// <summary>
    /// Validates and normalizes the search parameters
    /// </summary>
    public new void Normalize()
    {
        base.Normalize();

        // Normalize status filter
        Status = Status?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(Status))
        {
            Status = null;
        }

        // Normalize identification type
        IdentificationType = IdentificationType?.Trim();
        if (string.IsNullOrEmpty(IdentificationType))
        {
            IdentificationType = null;
        }

        // Normalize search term
        SearchTerm = SearchTerm?.Trim();
        if (string.IsNullOrEmpty(SearchTerm))
        {
            SearchTerm = null;
        }

        // Validate date range
        if (FromDate.HasValue && ToDate.HasValue && FromDate > ToDate)
        {
            // Swap dates if they are in wrong order
            (FromDate, ToDate) = (ToDate, FromDate);
        }

        // Set default sort field if not specified
        if (string.IsNullOrEmpty(SortBy))
        {
            SortBy = "CreatedAt";
            SortDirection = "desc"; // Show newest first by default
        }
    }
}