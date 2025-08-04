using System;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Security.Models;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service for managing rate limiting and preventing brute force attacks
    /// </summary>
    public interface IRateLimitingService
    {
        /// <summary>
        /// Check if an IP address is currently locked out
        /// </summary>
        Task<RateLimitStatus> CheckRateLimitAsync(string ipAddress, string context = "login");

        /// <summary>
        /// Check rate limit for a specific operation and identifier with cancellation support
        /// </summary>
        Task<RateLimitStatus> CheckRateLimitAsync(string operation, string identifier, CancellationToken cancellationToken = default);

        /// <summary>
        /// Record a failed attempt for rate limiting
        /// </summary>
        Task RecordFailedAttemptAsync(string ipAddress, string context = "login", string username = null);

        /// <summary>
        /// Clear failed attempts for an IP address (after successful operation)
        /// </summary>
        Task ClearFailedAttemptsAsync(string ipAddress, string context = "login");

        /// <summary>
        /// Get current attempt count for an IP address
        /// </summary>
        Task<int> GetAttemptCountAsync(string ipAddress, string context = "login");

        /// <summary>
        /// Check if a specific user account is locked
        /// </summary>
        Task<UserLockoutStatus> CheckUserLockoutAsync(string username);

        /// <summary>
        /// Record a failed attempt for a specific user
        /// </summary>
        Task RecordUserFailedAttemptAsync(string username, string ipAddress);

        /// <summary>
        /// Clear user lockout
        /// </summary>
        Task ClearUserLockoutAsync(string username);

        /// <summary>
        /// Get rate limiting configuration
        /// </summary>
        RateLimitConfiguration GetConfiguration(string context = "login");

        /// <summary>
        /// Clean up expired lockout entries
        /// </summary>
        Task CleanupExpiredEntriesAsync();
    }

    // RateLimitStatus is now defined in BizConnect.Services.Security.Models.SecurityModels

    /// <summary>
    /// User-specific lockout status
    /// </summary>
    public class UserLockoutStatus
    {
        public bool IsLocked { get; set; }
        public DateTime? LockoutEndTime { get; set; }
        public int FailedAttempts { get; set; }
        public string LastFailedIpAddress { get; set; }
        public DateTime? LastFailedAttempt { get; set; }
    }

    /// <summary>
    /// Configuration for rate limiting
    /// </summary>
    public class RateLimitConfiguration
    {
        public string Context { get; set; }
        public int MaxAttempts { get; set; }
        public int LockoutDurationMinutes { get; set; }
        public int AttemptWindowMinutes { get; set; }
        public bool EnableUserLockout { get; set; }
        public bool EnableIpLockout { get; set; }
    }
}