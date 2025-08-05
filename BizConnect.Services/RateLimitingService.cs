using BizConnect.Dal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Security.Models;
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

        public async Task<RateLimitStatus> CheckRateLimitAsync(string operation, string identifier, CancellationToken cancellationToken = default)
        {
            // Delegate to the existing method with parameter mapping
            return await CheckRateLimitAsync(identifier, operation);
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
            var attemptOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.AttemptWindowMinutes),
                Size = CalculateCacheEntrySize(attempts, attemptKey)
            };
            _cache.Set(attemptKey, attempts, attemptOptions);

            // Check if we've exceeded max attempts
            if (attempts.Count >= config.MaxAttempts)
            {
                // Lock the IP
                var lockKey = $"{cacheKey}:locked";
                var lockoutEnd = DateTime.UtcNow.AddMinutes(config.LockoutDurationMinutes);
                var lockOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.LockoutDurationMinutes),
                    Size = CalculateCacheEntrySize(lockoutEnd, lockKey)
                };
                _cache.Set(lockKey, lockoutEnd, lockOptions);

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
            var lockoutOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.LockoutDurationMinutes),
                Size = CalculateCacheEntrySize(lockoutInfo, cacheKey)
            };
            _cache.Set(cacheKey, lockoutInfo, lockoutOptions);
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

        // UserLockoutInfo is now defined in BizConnect.Services.Security.Models.SecurityModels

        /// <summary>
        /// Calculates the approximate memory size of a cache entry including key and value.
        /// Used for memory cache size limiting when SizeLimit is configured.
        /// </summary>
        /// <param name="value">The value to cache</param>
        /// <param name="key">The cache key</param>
        /// <returns>Estimated size in bytes</returns>
        private long CalculateCacheEntrySize<T>(T value, string key)
        {
            try
            {
                long size = 0;
                
                // Add key size (UTF-8 encoding)
                size += Encoding.UTF8.GetByteCount(key);
                
                // Calculate value size based on type
                if (value == null)
                {
                    size += 8; // null reference size
                }
                else if (value is string stringValue)
                {
                    size += Encoding.UTF8.GetByteCount(stringValue);
                }
                else if (value is DateTime)
                {
                    size += 8; // DateTime is 8 bytes
                }
                else if (value is List<DateTime> dateTimeList)
                {
                    size += dateTimeList.Count * 8 + 32; // DateTime list overhead
                }
                else if (value.GetType().IsPrimitive)
                {
                    // Handle primitive types
                    size += value.GetType().Name switch
                    {
                        "Boolean" => 1,
                        "Byte" => 1,
                        "SByte" => 1,
                        "Char" => 2,
                        "Int16" => 2,
                        "UInt16" => 2,
                        "Int32" => 4,
                        "UInt32" => 4,
                        "Int64" => 8,
                        "UInt64" => 8,
                        "Single" => 4,
                        "Double" => 8,
                        "Decimal" => 16,
                        _ => 8 // Default for unknown primitives
                    };
                }
                else
                {
                    // For complex objects, serialize to JSON to estimate size
                    try
                    {
                        var json = JsonSerializer.Serialize(value);
                        size += Encoding.UTF8.GetByteCount(json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to serialize object for size calculation, using default size estimate for key: {Key}", key);
                        size += 512; // Default 512 bytes for rate limiting objects
                    }
                }
                
                // Add overhead for cache entry metadata (approximately 64 bytes)
                size += 64;
                
                // Ensure minimum size of 1 byte
                return Math.Max(1, size);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating cache entry size for key: {Key}, using default size", key);
                return 512; // Default 512 bytes when calculation fails
            }
        }
    }
}