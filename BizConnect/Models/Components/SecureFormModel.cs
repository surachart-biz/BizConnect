using System.ComponentModel.DataAnnotations;

namespace BizConnect.Models.Components;

/// <summary>
/// Model for the secure form component with enhanced security features
/// </summary>
public class SecureFormModel
{
    /// <summary>
    /// Form action URL
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, etc.)
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// Unique form identifier
    /// </summary>
    public string? FormId { get; set; }

    /// <summary>
    /// CSS class to apply to the form
    /// </summary>
    public string CssClass { get; set; } = "secure-form";

    /// <summary>
    /// Security level: normal, elevated, high, critical
    /// </summary>
    [AllowedValues("normal", "elevated", "high", "critical")]
    public string SecurityLevel { get; set; } = "normal";

    /// <summary>
    /// Whether to show security indicators and status
    /// </summary>
    public bool ShowSecurityIndicators { get; set; } = true;

    /// <summary>
    /// Whether this form handles OTAC (One-Time Access Code) input
    /// </summary>
    public bool IsOtacForm { get; set; } = false;

    /// <summary>
    /// Whether this form contains sensitive data requiring extra protection
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// Whether to enable rate limiting for this form
    /// </summary>
    public bool EnableRateLimit { get; set; } = true;

    /// <summary>
    /// Rate limit cooldown period in milliseconds
    /// </summary>
    public int RateLimitCooldown { get; set; } = 3000; // 3 seconds

    /// <summary>
    /// Whether to enable client-side input validation
    /// </summary>
    public bool EnableClientValidation { get; set; } = true;

    /// <summary>
    /// Whether to log security events for this form
    /// </summary>
    public bool EnableSecurityLogging { get; set; } = true;

    /// <summary>
    /// Additional data attributes to add to the form
    /// </summary>
    public Dictionary<string, string>? DataAttributes { get; set; }

    /// <summary>
    /// Custom validation rules for form fields
    /// </summary>
    public Dictionary<string, FormFieldValidation>? FieldValidations { get; set; }

    /// <summary>
    /// Security configuration for this form
    /// </summary>
    public FormSecurityConfig? SecurityConfig { get; set; }
}

/// <summary>
/// Security configuration for secure forms
/// </summary>
public class FormSecurityConfig
{
    /// <summary>
    /// Require HTTPS for form submission
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// Enable CSRF protection
    /// </summary>
    public bool EnableCsrfProtection { get; set; } = true;

    /// <summary>
    /// Enable session validation
    /// </summary>
    public bool ValidateSession { get; set; } = true;

    /// <summary>
    /// Enable client fingerprinting
    /// </summary>
    public bool EnableFingerprinting { get; set; } = true;

    /// <summary>
    /// Maximum allowed form submission attempts per session
    /// </summary>
    public int MaxSubmissionAttempts { get; set; } = 5;

    /// <summary>
    /// Form submission timeout in minutes
    /// </summary>
    public int SubmissionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to validate form integrity (detect tampering)
    /// </summary>
    public bool ValidateFormIntegrity { get; set; } = false;

    /// <summary>
    /// Security headers to include in the form section
    /// </summary>
    public Dictionary<string, string>? SecurityHeaders { get; set; }
}

/// <summary>
/// Field validation configuration
/// </summary>
public class FormFieldValidation
{
    /// <summary>
    /// Field name/identifier
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Field type for specialized validation
    /// </summary>
    public FormFieldType FieldType { get; set; } = FormFieldType.Text;

    /// <summary>
    /// Whether the field is required
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Minimum length for the field value
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length for the field value
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Regular expression pattern for validation
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Custom validation message
    /// </summary>
    public string? ValidationMessage { get; set; }

    /// <summary>
    /// Whether to sanitize input for this field
    /// </summary>
    public bool SanitizeInput { get; set; } = true;

    /// <summary>
    /// Whether this field contains sensitive data
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// Whether to mask the input (for passwords, etc.)
    /// </summary>
    public bool MaskInput { get; set; } = false;
}

/// <summary>
/// Form field types for specialized validation
/// </summary>
public enum FormFieldType
{
    /// <summary>
    /// Regular text input
    /// </summary>
    Text,

    /// <summary>
    /// Email address
    /// </summary>
    Email,

    /// <summary>
    /// Password field
    /// </summary>
    Password,

    /// <summary>
    /// Phone number
    /// </summary>
    Phone,

    /// <summary>
    /// OTAC (One-Time Access Code)
    /// </summary>
    Otac,

    /// <summary>
    /// National ID number
    /// </summary>
    NationalId,

    /// <summary>
    /// Bank account number
    /// </summary>
    BankAccount,

    /// <summary>
    /// Numeric input
    /// </summary>
    Numeric,

    /// <summary>
    /// Date input
    /// </summary>
    Date,

    /// <summary>
    /// URL input
    /// </summary>
    Url,

    /// <summary>
    /// Multi-line text
    /// </summary>
    TextArea
}

/// <summary>
/// Security status for form monitoring
/// </summary>
public class FormSecurityStatus
{
    /// <summary>
    /// Current security level
    /// </summary>
    public string SecurityLevel { get; set; } = "normal";

    /// <summary>
    /// Whether CSRF protection is active
    /// </summary>
    public bool CsrfProtectionActive { get; set; } = true;

    /// <summary>
    /// Whether rate limiting is active
    /// </summary>
    public bool RateLimitActive { get; set; } = false;

    /// <summary>
    /// Current attempt count for rate limiting
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Maximum allowed attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Remaining cooldown time in seconds
    /// </summary>
    public int CooldownSeconds { get; set; } = 0;

    /// <summary>
    /// Whether the form is currently locked due to security concerns
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Security validation messages
    /// </summary>
    public List<string> ValidationMessages { get; set; } = new();

    /// <summary>
    /// Last security event timestamp
    /// </summary>
    public DateTime? LastSecurityEvent { get; set; }
}

/// <summary>
/// Security event for form monitoring
/// </summary>
public class FormSecurityEvent
{
    /// <summary>
    /// Unique event identifier
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Form identifier
    /// </summary>
    public string FormId { get; set; } = string.Empty;

    /// <summary>
    /// Event type
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Event timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User session identifier
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Event details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    /// Security level at the time of the event
    /// </summary>
    public string SecurityLevel { get; set; } = "normal";

    /// <summary>
    /// Whether this event requires immediate attention
    /// </summary>
    public bool IsHighPriority { get; set; } = false;
}

/// <summary>
/// Allowed values attribute for validation
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class AllowedValuesAttribute : ValidationAttribute
{
    private readonly string[] _allowedValues;

    public AllowedValuesAttribute(params string[] allowedValues)
    {
        _allowedValues = allowedValues;
    }

    public override bool IsValid(object? value)
    {
        if (value == null) return true;
        return _allowedValues.Contains(value.ToString());
    }

    public override string FormatErrorMessage(string name)
    {
        return $"The field {name} must be one of the following values: {string.Join(", ", _allowedValues)}";
    }
}