using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services
{
    /// <summary>
    /// Service for managing rate limiting using distributed caching
    /// </summary>
    public class RateLimitingService : IRateLimitingService
    {
        private readonly BizConnectContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RateLimitingService> _logger;
        private readonly ISecurityAuditService _auditService;
        private readonly IConfiguration _configuration;

        // Configuration constants (should be moved to appsettings)
        private const string CacheKeyPrefix = "RateLimit:";
        private const string UserLockoutPrefix = "UserLockout:";

        public RateLimitingService(
            BizConnectContext context,
            IMemoryCache cache,
            ILogger<RateLimitingService> logger,
            ISecurityAuditService auditService,
            IConfiguration configuration)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _auditService = auditService;
            _configuration = configuration;
        }

        public async Task<RateLimitStatus> CheckRateLimitAsync(string ipAddress, string context = "login")
        {
            var config = GetConfiguration(context);
            var cacheKey = $"{CacheKeyPrefix}{context}:{ipAddress}";

            // Check if IP is locked
            var lockKey = $"{cacheKey}:locked";
            if (_cache.TryGetValue<DateTime>(lockKey, out var lockoutEnd))
            {
                if (lockoutEnd > DateTime.UtcNow)
                {
                    var timeRemaining = lockoutEnd - DateTime.UtcNow;
                    return new RateLimitStatus
                    {
                        IsLocked = true,
                        LockoutEndTime = lockoutEnd,
                        RemainingAttempts = 0,
                        TotalAttempts = config.MaxAttempts,
                        TimeUntilUnlock = timeRemaining,
                        Message = $"Too many failed attempts. Please try again after {config.LockoutDurationMinutes} minutes."
                    };
                }
                else
                {
                    // Lockout expired, remove it
                    _cache.Remove(lockKey);
                }
            }

            // Get current attempts
            var attempts = GetRecentAttempts(cacheKey, config.AttemptWindowMinutes);
            var remainingAttempts = Math.Max(0, config.MaxAttempts - attempts.Count);

            await Task.CompletedTask; // Async for future database operations

            return new RateLimitStatus
            {
                IsLocked = false,
                RemainingAttempts = remainingAttempts,
                TotalAttempts = config.MaxAttempts,
                Message = remainingAttempts > 0 
                    ? $"You have {remainingAttempts} attempts remaining." 
                    : "Maximum attempts reached."
            };
        }

        public async Task RecordFailedAttemptAsync(string ipAddress, string context = "login", string username = null)
        {
            var config = GetConfiguration(context);
            var cacheKey = $"{CacheKeyPrefix}{context}:{ipAddress}";

            // Get current attempts
            var attempts = GetRecentAttempts(cacheKey, config.AttemptWindowMinutes);
            
            // Add new attempt
            attempts.Add(DateTime.UtcNow);
            
            // Store updated attempts
            var attemptKey = $"{cacheKey}:attempts";
            _cache.Set(attemptKey, attempts, TimeSpan.FromMinutes(config.AttemptWindowMinutes));

            // Check if we've exceeded max attempts
            if (attempts.Count >= config.MaxAttempts)
            {
                // Lock the IP
                var lockKey = $"{cacheKey}:locked";
                var lockoutEnd = DateTime.UtcNow.AddMinutes(config.LockoutDurationMinutes);
                _cache.Set(lockKey, lockoutEnd, TimeSpan.FromMinutes(config.LockoutDurationMinutes));

                // Log the lockout
                await _auditService.LogAccountLockoutAsync(ipAddress, attempts.Count);
                _logger.LogWarning("IP {IpAddress} locked out for {Context} after {Attempts} failed attempts", 
                    ipAddress, context, attempts.Count);
            }

            // Record user-specific failure if username provided
            if (!string.IsNullOrEmpty(username))
            {
                await RecordUserFailedAttemptAsync(username, ipAddress);
            }
        }

        public async Task ClearFailedAttemptsAsync(string ipAddress, string context = "login")
        {
            var cacheKey = $"{CacheKeyPrefix}{context}:{ipAddress}";
            
            _cache.Remove($"{cacheKey}:attempts");
            _cache.Remove($"{cacheKey}:locked");

            _logger.LogDebug("Cleared rate limit for IP {IpAddress} in context {Context}", ipAddress, context);
            
            await Task.CompletedTask;
        }

        public async Task<int> GetAttemptCountAsync(string ipAddress, string context = "login")
        {
            var config = GetConfiguration(context);
            var cacheKey = $"{CacheKeyPrefix}{context}:{ipAddress}";
            var attempts = GetRecentAttempts(cacheKey, config.AttemptWindowMinutes);
            
            await Task.CompletedTask;
            return attempts.Count;
        }

        public async Task<UserLockoutStatus> CheckUserLockoutAsync(string username)
        {
            // Check user lockout in database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            
            if (user == null)
            {
                return new UserLockoutStatus { IsLocked = false };
            }

            // Check cache for additional lockout info
            var cacheKey = $"{UserLockoutPrefix}{username}";
            if (_cache.TryGetValue<UserLockoutInfo>(cacheKey, out var lockoutInfo))
            {
                if (lockoutInfo.LockoutEnd > DateTime.UtcNow)
                {
                    return new UserLockoutStatus
                    {
                        IsLocked = true,
                        LockoutEndTime = lockoutInfo.LockoutEnd,
                        FailedAttempts = lockoutInfo.FailedAttempts,
                        LastFailedIpAddress = lockoutInfo.LastFailedIpAddress,
                        LastFailedAttempt = lockoutInfo.LastFailedAttempt
                    };
                }
            }

            return new UserLockoutStatus
            {
                IsLocked = false,
                FailedAttempts = lockoutInfo?.FailedAttempts ?? 0
            };
        }

        public async Task RecordUserFailedAttemptAsync(string username, string ipAddress)
        {
            var cacheKey = $"{UserLockoutPrefix}{username}";
            var config = GetConfiguration("login");

            // Get or create lockout info
            if (!_cache.TryGetValue<UserLockoutInfo>(cacheKey, out var lockoutInfo))
            {
                lockoutInfo = new UserLockoutInfo
                {
                    Username = username,
                    FailedAttempts = 0
                };
            }

            lockoutInfo.FailedAttempts++;
            lockoutInfo.LastFailedIpAddress = ipAddress;
            lockoutInfo.LastFailedAttempt = DateTime.UtcNow;

            // Check if user should be locked
            if (lockoutInfo.FailedAttempts >= config.MaxAttempts)
            {
                lockoutInfo.LockoutEnd = DateTime.UtcNow.AddMinutes(config.LockoutDurationMinutes);
                
                // Update user in database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user != null)
                {
                    // You might want to add a LockedUntil field to the User model
                    // For now, we'll just track in cache
                    _logger.LogWarning("User {Username} locked out after {Attempts} failed attempts from {IP}", 
                        username, lockoutInfo.FailedAttempts, ipAddress);
                }
            }

            // Store in cache
            _cache.Set(cacheKey, lockoutInfo, TimeSpan.FromMinutes(config.LockoutDurationMinutes));
        }

        public async Task ClearUserLockoutAsync(string username)
        {
            var cacheKey = $"{UserLockoutPrefix}{username}";
            _cache.Remove(cacheKey);

            _logger.LogInformation("Cleared lockout for user {Username}", username);
            
            await Task.CompletedTask;
        }

        public RateLimitConfiguration GetConfiguration(string context = "login")
        {
            // Load from configuration or use defaults
            return new RateLimitConfiguration
            {
                Context = context,
                MaxAttempts = _configuration.GetValue<int?>($"RateLimiting:{context}:MaxAttempts") ?? 5,
                LockoutDurationMinutes = _configuration.GetValue<int?>($"RateLimiting:{context}:LockoutDurationMinutes") ?? 15,
                AttemptWindowMinutes = _configuration.GetValue<int?>($"RateLimiting:{context}:AttemptWindowMinutes") ?? 15,
                EnableUserLockout = _configuration.GetValue<bool?>($"RateLimiting:{context}:EnableUserLockout") ?? true,
                EnableIpLockout = _configuration.GetValue<bool?>($"RateLimiting:{context}:EnableIpLockout") ?? true
            };
        }

        public async Task CleanupExpiredEntriesAsync()
        {
            // This would typically be called by a background job
            // For now, the MemoryCache handles expiration automatically
            
            _logger.LogDebug("Rate limiting cleanup completed");
            await Task.CompletedTask;
        }

        private List<DateTime> GetRecentAttempts(string cacheKey, int windowMinutes)
        {
            var attemptKey = $"{cacheKey}:attempts";
            if (_cache.TryGetValue<List<DateTime>>(attemptKey, out var attempts))
            {
                // Filter out old attempts outside the window
                var cutoff = DateTime.UtcNow.AddMinutes(-windowMinutes);
                return attempts.Where(a => a > cutoff).ToList();
            }
            
            return new List<DateTime>();
        }

        private class UserLockoutInfo
        {
            public string Username { get; set; }
            public int FailedAttempts { get; set; }
            public DateTime? LockoutEnd { get; set; }
            public string LastFailedIpAddress { get; set; }
            public DateTime? LastFailedAttempt { get; set; }
        }
    }
}