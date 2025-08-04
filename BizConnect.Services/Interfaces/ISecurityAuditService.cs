using System;
using System.Threading.Tasks;
using BizConnect.Services.Common;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service for logging and auditing security-related events.
    /// Provides both traditional async methods (backward compatibility) and Result-pattern methods (enhanced error handling).
    /// </summary>
    public interface ISecurityAuditService
    {
        // Traditional async methods for backward compatibility
        /// <summary>
        /// Log a successful authentication event
        /// </summary>
        Task LogSuccessfulLoginAsync(string username, string ipAddress, string userAgent = null);

        /// <summary>
        /// Log a failed authentication attempt
        /// </summary>
        Task LogFailedLoginAsync(string username, string ipAddress, string reason, string userAgent = null);

        /// <summary>
        /// Log a user logout event
        /// </summary>
        Task LogLogoutAsync(string username, string ipAddress);

        /// <summary>
        /// Log an account lockout event
        /// </summary>
        Task LogAccountLockoutAsync(string ipAddress, int failedAttempts);

        /// <summary>
        /// Log unauthorized access attempts
        /// </summary>
        Task LogUnauthorizedAccessAsync(string username, string resource, string ipAddress);

        /// <summary>
        /// Log OTAC generation event
        /// </summary>
        Task LogOtacGeneratedAsync(string code, string purpose, string generatedBy);

        /// <summary>
        /// Log OTAC validation attempt
        /// </summary>
        Task LogOtacValidationAsync(string code, bool success, string ipAddress, int attemptNumber);

        /// <summary>
        /// Log OTAC lockout event
        /// </summary>
        Task LogOtacLockoutAsync(string code, int failedAttempts);

        /// <summary>
        /// Log password change event
        /// </summary>
        Task LogPasswordChangeAsync(string username, string changedBy, string ipAddress);

        /// <summary>
        /// Log role change event
        /// </summary>
        Task LogRoleChangeAsync(string username, string oldRole, string newRole, string changedBy);

        /// <summary>
        /// Log security configuration change
        /// </summary>
        Task LogSecurityConfigurationChangeAsync(string setting, string oldValue, string newValue, string changedBy);

        /// <summary>
        /// Log suspicious activity
        /// </summary>
        Task LogSuspiciousActivityAsync(string activityType, string details, string ipAddress);

        /// <summary>
        /// Log data access event for sensitive operations
        /// </summary>
        Task LogDataAccessAsync(string username, string dataType, string operation, string entityId);

        /// <summary>
        /// Log threat response actions
        /// </summary>
        Task LogThreatResponseAsync(string ipAddress, string threatLevel, int actionCount);

        /// <summary>
        /// Log IP blocking event
        /// </summary>
        Task LogIpBlockAsync(string ipAddress, string reason);

        /// <summary>
        /// Log IP blocking event with duration
        /// </summary>
        Task LogIpBlockAsync(string ipAddress, string reason, TimeSpan duration);

        /// <summary>
        /// Get recent security events for monitoring
        /// </summary>
        Task<IEnumerable<SecurityAuditEntry>> GetRecentEventsAsync(int count = 100);

        /// <summary>
        /// Get security events for a specific user
        /// </summary>
        Task<IEnumerable<SecurityAuditEntry>> GetUserEventsAsync(string username, DateTime? fromDate = null);

        /// <summary>
        /// Get security events from a specific IP address
        /// </summary>
        Task<IEnumerable<SecurityAuditEntry>> GetIpEventsAsync(string ipAddress, DateTime? fromDate = null);

        // Enhanced Result-pattern methods for improved error handling
        /// <summary>
        /// Log a successful authentication event with Result pattern
        /// </summary>
        Task<Result> LogSuccessfulLoginResultAsync(string username, string ipAddress, string userAgent = null);

        /// <summary>
        /// Log a failed authentication attempt with Result pattern
        /// </summary>
        Task<Result> LogFailedLoginResultAsync(string username, string ipAddress, string reason, string userAgent = null);

        /// <summary>
        /// Log a user logout event with Result pattern
        /// </summary>
        Task<Result> LogLogoutResultAsync(string username, string ipAddress);

        /// <summary>
        /// Log an account lockout event with Result pattern
        /// </summary>
        Task<Result> LogAccountLockoutResultAsync(string ipAddress, int failedAttempts);

        /// <summary>
        /// Log unauthorized access attempts with Result pattern
        /// </summary>
        Task<Result> LogUnauthorizedAccessResultAsync(string username, string resource, string ipAddress);

        /// <summary>
        /// Log OTAC generation event with Result pattern
        /// </summary>
        Task<Result> LogOtacGeneratedResultAsync(string code, string purpose, string generatedBy);

        /// <summary>
        /// Log OTAC validation attempt with Result pattern
        /// </summary>
        Task<Result> LogOtacValidationResultAsync(string code, bool success, string ipAddress, int attemptNumber);

        /// <summary>
        /// Log OTAC lockout event with Result pattern
        /// </summary>
        Task<Result> LogOtacLockoutResultAsync(string code, int failedAttempts);

        /// <summary>
        /// Log suspicious activity with Result pattern
        /// </summary>
        Task<Result> LogSuspiciousActivityResultAsync(string activityType, string details, string ipAddress);

        /// <summary>
        /// Get recent security events for monitoring with Result pattern
        /// </summary>
        Task<Result<IEnumerable<SecurityAuditEntry>>> GetRecentEventsResultAsync(int count = 100);

        /// <summary>
        /// Get security events for a specific user with Result pattern
        /// </summary>
        Task<Result<IEnumerable<SecurityAuditEntry>>> GetUserEventsResultAsync(string username, DateTime? fromDate = null);

        /// <summary>
        /// Get security events from a specific IP address with Result pattern
        /// </summary>
        Task<Result<IEnumerable<SecurityAuditEntry>>> GetIpEventsResultAsync(string ipAddress, DateTime? fromDate = null);

        /// <summary>
        /// Log a security event with category, action, and details
        /// </summary>
        Task LogSecurityEventAsync(string category, string action, object details, System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Log a security event object for monitoring and analysis
        /// </summary>
        Task LogSecurityEventAsync(BizConnect.Services.Security.Models.SecurityEvent securityEvent, System.Threading.CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a security audit log entry
    /// </summary>
    public class SecurityAuditEntry
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string Username { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Details { get; set; }
        public string Severity { get; set; } // Info, Warning, Critical
        public bool Success { get; set; }
        public string AdditionalData { get; set; } // JSON for flexible data storage
    }

    /// <summary>
    /// Security event types for consistent logging
    /// </summary>
    public static class SecurityEventTypes
    {
        public const string Login = "LOGIN";
        public const string LoginFailed = "LOGIN_FAILED";
        public const string Logout = "LOGOUT";
        public const string AccountLockout = "ACCOUNT_LOCKOUT";
        public const string UnauthorizedAccess = "UNAUTHORIZED_ACCESS";
        public const string OtacGenerated = "OTAC_GENERATED";
        public const string OtacValidation = "OTAC_VALIDATION";
        public const string OtacLockout = "OTAC_LOCKOUT";
        public const string PasswordChange = "PASSWORD_CHANGE";
        public const string RoleChange = "ROLE_CHANGE";
        public const string ConfigurationChange = "CONFIG_CHANGE";
        public const string SuspiciousActivity = "SUSPICIOUS_ACTIVITY";
        public const string DataAccess = "DATA_ACCESS";
    }
}