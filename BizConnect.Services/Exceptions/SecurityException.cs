using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BizConnect.Services.Exceptions
{
    /// <summary>
    /// Exception thrown for security-related violations such as unauthorized access,
    /// authentication failures, or security policy violations.
    /// </summary>
    [Serializable]
    public class SecurityException : Exception
    {
        /// <summary>
        /// Security event type for categorization and logging
        /// </summary>
        public string SecurityEventType { get; }

        /// <summary>
        /// IP address of the client that triggered this security exception
        /// </summary>
        public string ClientIpAddress { get; }

        /// <summary>
        /// User agent string from the request
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Username associated with the security violation (if available)
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Resource or action that was attempted to be accessed
        /// </summary>
        public string AttemptedResource { get; }

        /// <summary>
        /// Security context information for auditing
        /// </summary>
        public Dictionary<string, object> SecurityContext { get; }

        /// <summary>
        /// Severity level of the security violation
        /// </summary>
        public SecuritySeverity Severity { get; }

        /// <summary>
        /// Whether this security violation should trigger additional security measures
        /// </summary>
        public bool RequiresImmediateAction { get; }

        /// <summary>
        /// Recommendation for handling this security violation
        /// </summary>
        public string SecurityRecommendation { get; set; }

        public SecurityException(string message) : base(message)
        {
            SecurityContext = new Dictionary<string, object>();
            Severity = SecuritySeverity.Medium;
        }

        public SecurityException(string message, Exception innerException) : base(message, innerException)
        {
            SecurityContext = new Dictionary<string, object>();
            Severity = SecuritySeverity.Medium;
        }

        public SecurityException(string message, string securityEventType, SecuritySeverity severity = SecuritySeverity.Medium) 
            : base(message)
        {
            SecurityEventType = securityEventType;
            Severity = severity;
            SecurityContext = new Dictionary<string, object>();
        }

        public SecurityException(string message, string securityEventType, string clientIpAddress, 
            string username = null, string attemptedResource = null, SecuritySeverity severity = SecuritySeverity.Medium,
            bool requiresImmediateAction = false) : base(message)
        {
            SecurityEventType = securityEventType;
            ClientIpAddress = clientIpAddress;
            Username = username;
            AttemptedResource = attemptedResource;
            Severity = severity;
            RequiresImmediateAction = requiresImmediateAction;
            SecurityContext = new Dictionary<string, object>();
        }

        protected SecurityException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            SecurityEventType = info.GetString(nameof(SecurityEventType));
            ClientIpAddress = info.GetString(nameof(ClientIpAddress));
            UserAgent = info.GetString(nameof(UserAgent));
            Username = info.GetString(nameof(Username));
            AttemptedResource = info.GetString(nameof(AttemptedResource));
            Severity = (SecuritySeverity)info.GetInt32(nameof(Severity));
            RequiresImmediateAction = info.GetBoolean(nameof(RequiresImmediateAction));
            SecurityRecommendation = info.GetString(nameof(SecurityRecommendation));
            SecurityContext = new Dictionary<string, object>();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(SecurityEventType), SecurityEventType);
            info.AddValue(nameof(ClientIpAddress), ClientIpAddress);
            info.AddValue(nameof(UserAgent), UserAgent);
            info.AddValue(nameof(Username), Username);
            info.AddValue(nameof(AttemptedResource), AttemptedResource);
            info.AddValue(nameof(Severity), (int)Severity);
            info.AddValue(nameof(RequiresImmediateAction), RequiresImmediateAction);
            info.AddValue(nameof(SecurityRecommendation), SecurityRecommendation);
        }

        /// <summary>
        /// Add security context information
        /// </summary>
        public SecurityException WithSecurityContext(string key, object value)
        {
            SecurityContext[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple security context entries
        /// </summary>
        public SecurityException WithSecurityContext(Dictionary<string, object> additionalContext)
        {
            if (additionalContext != null)
            {
                foreach (var kvp in additionalContext)
                {
                    SecurityContext[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// Set user agent information
        /// </summary>
        public SecurityException WithUserAgent(string userAgent)
        {
            return new SecurityException(Message, SecurityEventType, ClientIpAddress, Username, AttemptedResource, 
                Severity, RequiresImmediateAction)
            {
                UserAgent = userAgent,
                SecurityRecommendation = SecurityRecommendation
            }.WithSecurityContext(SecurityContext);
        }

        /// <summary>
        /// Set security recommendation
        /// </summary>
        public SecurityException WithRecommendation(string recommendation)
        {
            return new SecurityException(Message, SecurityEventType, ClientIpAddress, Username, AttemptedResource, 
                Severity, RequiresImmediateAction)
            {
                UserAgent = UserAgent,
                SecurityRecommendation = recommendation
            }.WithSecurityContext(SecurityContext);
        }

        /// <summary>
        /// Get a sanitized message safe for client responses
        /// </summary>
        public string GetSanitizedMessage()
        {
            return Severity switch
            {
                SecuritySeverity.Critical => "Access denied due to security policy violation.",
                SecuritySeverity.High => "Access denied. Please contact administrator if you believe this is an error.",
                SecuritySeverity.Medium => "Access denied. Insufficient permissions.",
                SecuritySeverity.Low => "Access denied.",
                SecuritySeverity.Info => Message, // Info level can show original message
                _ => "Access denied."
            };
        }
    }

    /// <summary>
    /// Security severity levels for proper categorization and response
    /// </summary>
    public enum SecuritySeverity
    {
        /// <summary>
        /// Informational security event (e.g., successful login)
        /// </summary>
        Info = 0,

        /// <summary>
        /// Low severity security issue (e.g., invalid form data)
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium severity security issue (e.g., unauthorized access attempt)
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High severity security issue (e.g., repeated failed login attempts)
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical security issue (e.g., suspected attack, system compromise)
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Common security event types for consistent categorization
    /// </summary>
    public static class SecurityEventTypes
    {
        public const string UnauthorizedAccess = "UNAUTHORIZED_ACCESS";
        public const string AuthenticationFailure = "AUTHENTICATION_FAILURE";
        public const string AuthorizationFailure = "AUTHORIZATION_FAILURE";
        public const string InvalidToken = "INVALID_TOKEN";
        public const string TokenExpired = "TOKEN_EXPIRED";
        public const string SessionViolation = "SESSION_VIOLATION";
        public const string RateLimitViolation = "RATE_LIMIT_VIOLATION";
        public const string SuspiciousActivity = "SUSPICIOUS_ACTIVITY";
        public const string PolicyViolation = "POLICY_VIOLATION";
        public const string DataAccessViolation = "DATA_ACCESS_VIOLATION";
        public const string PrivilegeEscalation = "PRIVILEGE_ESCALATION";
        public const string CsrfViolation = "CSRF_VIOLATION";
        public const string InputValidationFailure = "INPUT_VALIDATION_FAILURE";
        public const string SecurityHeaderViolation = "SECURITY_HEADER_VIOLATION";
        public const string IpBlocklistViolation = "IP_BLOCKLIST_VIOLATION";
        public const string AccountLockout = "ACCOUNT_LOCKOUT";
        public const string PasswordPolicyViolation = "PASSWORD_POLICY_VIOLATION";
        public const string TwoFactorFailure = "TWO_FACTOR_FAILURE";
        public const string OtacViolation = "OTAC_VIOLATION";
        public const string ConcurrentSessionViolation = "CONCURRENT_SESSION_VIOLATION";
        public const string ConfigurationTampering = "CONFIGURATION_TAMPERING";
    }

    /// <summary>
    /// Factory class for creating common security exceptions
    /// </summary>
    public static class SecurityExceptionFactory
    {
        public static SecurityException UnauthorizedAccess(string username, string resource, string ipAddress)
        {
            return new SecurityException(
                $"User '{username}' attempted unauthorized access to '{resource}'",
                SecurityEventTypes.UnauthorizedAccess,
                ipAddress,
                username,
                resource,
                SecuritySeverity.Medium,
                false
            ).WithRecommendation("Review user permissions and access policies");
        }

        public static SecurityException AuthenticationFailure(string username, string ipAddress, string reason)
        {
            return new SecurityException(
                $"Authentication failed for user '{username}': {reason}",
                SecurityEventTypes.AuthenticationFailure,
                ipAddress,
                username,
                null,
                SecuritySeverity.Medium,
                false
            ).WithRecommendation("Monitor for repeated authentication failures");
        }

        public static SecurityException RateLimitExceeded(string ipAddress, string resource, int attemptCount)
        {
            return new SecurityException(
                $"Rate limit exceeded from IP {ipAddress} for resource {resource} ({attemptCount} attempts)",
                SecurityEventTypes.RateLimitViolation,
                ipAddress,
                null,
                resource,
                SecuritySeverity.High,
                true
            ).WithRecommendation("Consider IP blocking or additional rate limiting")
             .WithSecurityContext("AttemptCount", attemptCount);
        }

        public static SecurityException SuspiciousActivity(string activity, string ipAddress, string username = null)
        {
            return new SecurityException(
                $"Suspicious activity detected: {activity}",
                SecurityEventTypes.SuspiciousActivity,
                ipAddress,
                username,
                null,
                SecuritySeverity.High,
                true
            ).WithRecommendation("Investigate activity patterns and consider blocking");
        }

        public static SecurityException OtacViolation(string code, string ipAddress, string violationType)
        {
            return new SecurityException(
                $"OTAC violation detected: {violationType}",
                SecurityEventTypes.OtacViolation,
                ipAddress,
                null,
                $"OTAC:{code}",
                SecuritySeverity.Medium,
                false
            ).WithRecommendation("Monitor OTAC usage patterns")
             .WithSecurityContext("ViolationType", violationType);
        }

        public static SecurityException CsrfViolation(string ipAddress, string username, string action)
        {
            return new SecurityException(
                $"CSRF token validation failed for action '{action}'",
                SecurityEventTypes.CsrfViolation,
                ipAddress,
                username,
                action,
                SecuritySeverity.High,
                true
            ).WithRecommendation("Investigate potential CSRF attack");
        }
    }
}