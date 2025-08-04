using System;

namespace BizConnect.Services.Security.Models
{
    /// <summary>
    /// Result of a rate limit check operation
    /// </summary>
    public class RateLimitResult
    {
        /// <summary>
        /// Whether the operation is allowed (within rate limits)
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Number of attempts already made in the current time window
        /// </summary>
        public int CurrentAttempts { get; set; }

        /// <summary>
        /// Maximum attempts allowed in the time window
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Time remaining until the rate limit window resets
        /// </summary>
        public TimeSpan TimeUntilReset { get; set; }

        /// <summary>
        /// The operation that was rate limited
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// The IP address being rate limited
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata about the rate limit check
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Creates a successful rate limit result (operation allowed)
        /// </summary>
        public static RateLimitResult Allow(string operation, string ipAddress, int currentAttempts, int maxAttempts, TimeSpan timeUntilReset)
        {
            return new RateLimitResult
            {
                IsAllowed = true,
                Operation = operation,
                IpAddress = ipAddress,
                CurrentAttempts = currentAttempts,
                MaxAttempts = maxAttempts,
                TimeUntilReset = timeUntilReset
            };
        }

        /// <summary>
        /// Creates a failed rate limit result (operation denied)
        /// </summary>
        public static RateLimitResult Deny(string operation, string ipAddress, int currentAttempts, int maxAttempts, TimeSpan timeUntilReset, string? message = null)
        {
            return new RateLimitResult
            {
                IsAllowed = false,
                Operation = operation,
                IpAddress = ipAddress,
                CurrentAttempts = currentAttempts,
                MaxAttempts = maxAttempts,
                TimeUntilReset = timeUntilReset,
                Message = message ?? "Rate limit exceeded"
            };
        }
    }
}