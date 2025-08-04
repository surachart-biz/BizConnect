using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Common;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services
{
    /// <summary>
    /// Implementation of security audit service for logging and tracking security events
    /// </summary>
    public class SecurityAuditService : ISecurityAuditService
    {
        private readonly BizConnectContext _context;
        private readonly ILogger<SecurityAuditService> _logger;

        public SecurityAuditService(BizConnectContext context, ILogger<SecurityAuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogSuccessfulLoginAsync(string username, string ipAddress, string userAgent = null)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.Login,
                Username = username,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = $"User {username} successfully logged in",
                Severity = "Info",
                Success = true,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Security Audit: Successful login for {Username} from {IP}", username, ipAddress);
        }

        public async Task LogFailedLoginAsync(string username, string ipAddress, string reason, string userAgent = null)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.LoginFailed,
                Username = username,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = $"Failed login attempt for {username}: {reason}",
                Severity = "Warning",
                Success = false,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { Reason = reason })
            });

            _logger.LogWarning("Security Audit: Failed login for {Username} from {IP} - {Reason}", username, ipAddress, reason);
        }

        public async Task LogLogoutAsync(string username, string ipAddress)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.Logout,
                Username = username,
                IpAddress = ipAddress,
                Details = $"User {username} logged out",
                Severity = "Info",
                Success = true,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Security Audit: User {Username} logged out from {IP}", username, ipAddress);
        }

        public async Task LogAccountLockoutAsync(string ipAddress, int failedAttempts)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.AccountLockout,
                Username = "System",
                IpAddress = ipAddress,
                Details = $"IP {ipAddress} locked out after {failedAttempts} failed attempts",
                Severity = "Critical",
                Success = false,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { FailedAttempts = failedAttempts })
            });

            _logger.LogCritical("Security Audit: IP {IP} locked out after {Attempts} failed attempts", ipAddress, failedAttempts);
        }

        public async Task LogUnauthorizedAccessAsync(string username, string resource, string ipAddress)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.UnauthorizedAccess,
                Username = username ?? "Anonymous",
                IpAddress = ipAddress,
                Details = $"Unauthorized access attempt to {resource}",
                Severity = "Warning",
                Success = false,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { Resource = resource })
            });

            _logger.LogWarning("Security Audit: Unauthorized access to {Resource} by {Username} from {IP}", 
                resource, username ?? "Anonymous", ipAddress);
        }

        public async Task LogOtacGeneratedAsync(string code, string purpose, string generatedBy)
        {
            // Don't log the actual code for security, just metadata
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.OtacGenerated,
                Username = generatedBy,
                IpAddress = "System",
                Details = $"OTAC generated for {purpose}",
                Severity = "Info",
                Success = true,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    Purpose = purpose,
                    CodeHash = GetHashedCode(code) // Store hash, not actual code
                })
            });

            _logger.LogInformation("Security Audit: OTAC generated for {Purpose} by {GeneratedBy}", purpose, generatedBy);
        }

        public async Task LogOtacValidationAsync(string code, bool success, string ipAddress, int attemptNumber)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.OtacValidation,
                Username = "System",
                IpAddress = ipAddress,
                Details = success ? "OTAC validation successful" : $"OTAC validation failed (attempt {attemptNumber})",
                Severity = success ? "Info" : "Warning",
                Success = success,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    AttemptNumber = attemptNumber,
                    CodeHash = GetHashedCode(code)
                })
            });

            if (success)
            {
                _logger.LogInformation("Security Audit: OTAC validation successful from {IP}", ipAddress);
            }
            else
            {
                _logger.LogWarning("Security Audit: OTAC validation failed from {IP} (attempt {Attempt})", ipAddress, attemptNumber);
            }
        }

        public async Task LogOtacLockoutAsync(string code, int failedAttempts)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.OtacLockout,
                Username = "System",
                IpAddress = "System",
                Details = $"OTAC locked after {failedAttempts} failed attempts",
                Severity = "Critical",
                Success = false,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    FailedAttempts = failedAttempts,
                    CodeHash = GetHashedCode(code)
                })
            });

            _logger.LogCritical("Security Audit: OTAC locked after {Attempts} failed attempts", failedAttempts);
        }

        public async Task LogPasswordChangeAsync(string username, string changedBy, string ipAddress)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.PasswordChange,
                Username = username,
                IpAddress = ipAddress,
                Details = $"Password changed for {username} by {changedBy}",
                Severity = "Warning",
                Success = true,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { ChangedBy = changedBy })
            });

            _logger.LogWarning("Security Audit: Password changed for {Username} by {ChangedBy} from {IP}", 
                username, changedBy, ipAddress);
        }

        public async Task LogRoleChangeAsync(string username, string oldRole, string newRole, string changedBy)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.RoleChange,
                Username = username,
                IpAddress = "System",
                Details = $"Role changed from {oldRole} to {newRole} by {changedBy}",
                Severity = "Warning",
                Success = true,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    OldRole = oldRole, 
                    NewRole = newRole, 
                    ChangedBy = changedBy 
                })
            });

            _logger.LogWarning("Security Audit: Role changed for {Username} from {OldRole} to {NewRole} by {ChangedBy}", 
                username, oldRole, newRole, changedBy);
        }

        public async Task LogSecurityConfigurationChangeAsync(string setting, string oldValue, string newValue, string changedBy)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.ConfigurationChange,
                Username = changedBy,
                IpAddress = "System",
                Details = $"Security setting '{setting}' changed",
                Severity = "Critical",
                Success = true,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    Setting = setting,
                    OldValue = oldValue,
                    NewValue = newValue
                })
            });

            _logger.LogCritical("Security Audit: Configuration '{Setting}' changed by {ChangedBy}", setting, changedBy);
        }

        public async Task LogSuspiciousActivityAsync(string activityType, string details, string ipAddress)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.SuspiciousActivity,
                Username = "System",
                IpAddress = ipAddress,
                Details = $"Suspicious activity detected: {activityType} - {details}",
                Severity = "Critical",
                Success = false,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { ActivityType = activityType })
            });

            _logger.LogCritical("Security Audit: Suspicious activity '{ActivityType}' from {IP}: {Details}", 
                activityType, ipAddress, details);
        }

        public async Task LogDataAccessAsync(string username, string dataType, string operation, string entityId)
        {
            await LogEventAsync(new SecurityAuditEntry
            {
                EventType = SecurityEventTypes.DataAccess,
                Username = username,
                IpAddress = "System",
                Details = $"{operation} operation on {dataType} (ID: {entityId})",
                Severity = "Info",
                Success = true,
                Timestamp = DateTime.UtcNow,
                AdditionalData = JsonSerializer.Serialize(new { 
                    DataType = dataType, 
                    Operation = operation, 
                    EntityId = entityId 
                })
            });

            _logger.LogInformation("Security Audit: {Username} performed {Operation} on {DataType} ID: {EntityId}", 
                username, operation, dataType, entityId);
        }

        public async Task<IEnumerable<SecurityAuditEntry>> GetRecentEventsAsync(int count = 100)
        {
            // In a real implementation, this would query from a SecurityAuditLog table
            // For now, returning empty list as the table doesn't exist yet
            await Task.CompletedTask;
            return new List<SecurityAuditEntry>();
        }

        public async Task<IEnumerable<SecurityAuditEntry>> GetUserEventsAsync(string username, DateTime? fromDate = null)
        {
            // In a real implementation, this would query from a SecurityAuditLog table
            // For now, returning empty list as the table doesn't exist yet
            await Task.CompletedTask;
            return new List<SecurityAuditEntry>();
        }

        public async Task<IEnumerable<SecurityAuditEntry>> GetIpEventsAsync(string ipAddress, DateTime? fromDate = null)
        {
            // In a real implementation, this would query from a SecurityAuditLog table
            // For now, returning empty list as the table doesn't exist yet
            await Task.CompletedTask;
            return new List<SecurityAuditEntry>();
        }

        private async Task LogEventAsync(SecurityAuditEntry entry)
        {
            // In a real implementation, this would save to a SecurityAuditLog table
            // For now, we're just logging to the application logs
            // The database table would need to be created via migration
            
            // TODO: When SecurityAuditLog table is created, uncomment this:
            // _context.SecurityAuditLogs.Add(entry);
            // await _context.SaveChangesAsync();
            
            await Task.CompletedTask;
        }

        private string GetHashedCode(string code)
        {
            // Hash the code for security (don't store actual codes in logs)
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(code);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Substring(0, 10); // First 10 chars of hash
        }

        // Result-pattern methods for enhanced error handling
        
        public async Task<Result> LogSuccessfulLoginResultAsync(string username, string ipAddress, string userAgent = null)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogSuccessfulLoginAsync(username, ipAddress, userAgent);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogFailedLoginResultAsync(string username, string ipAddress, string reason, string userAgent = null)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogFailedLoginAsync(username, ipAddress, reason, userAgent);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogLogoutResultAsync(string username, string ipAddress)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogLogoutAsync(username, ipAddress);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogAccountLockoutResultAsync(string ipAddress, int failedAttempts)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogAccountLockoutAsync(ipAddress, failedAttempts);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogUnauthorizedAccessResultAsync(string username, string resource, string ipAddress)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogUnauthorizedAccessAsync(username, resource, ipAddress);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogOtacGeneratedResultAsync(string code, string purpose, string generatedBy)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogOtacGeneratedAsync(code, purpose, generatedBy);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogOtacValidationResultAsync(string code, bool success, string ipAddress, int attemptNumber)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogOtacValidationAsync(code, success, ipAddress, attemptNumber);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogOtacLockoutResultAsync(string code, int failedAttempts)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogOtacLockoutAsync(code, failedAttempts);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result> LogSuspiciousActivityResultAsync(string activityType, string details, string ipAddress)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                await LogSuspiciousActivityAsync(activityType, details, ipAddress);
            }, "AUDIT_LOG_FAILED");
        }

        public async Task<Result<IEnumerable<SecurityAuditEntry>>> GetRecentEventsResultAsync(int count = 100)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                var events = await GetRecentEventsAsync(count);
                return events;
            }, "AUDIT_QUERY_FAILED");
        }

        public async Task<Result<IEnumerable<SecurityAuditEntry>>> GetUserEventsResultAsync(string username, DateTime? fromDate = null)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                var events = await GetUserEventsAsync(username, fromDate);
                return events;
            }, "AUDIT_QUERY_FAILED");
        }

        public async Task<Result<IEnumerable<SecurityAuditEntry>>> GetIpEventsResultAsync(string ipAddress, DateTime? fromDate = null)
        {
            return await ResultExtensions.TryAsync(async () =>
            {
                var events = await GetIpEventsAsync(ipAddress, fromDate);
                return events;
            }, "AUDIT_QUERY_FAILED");
        }
    }
}