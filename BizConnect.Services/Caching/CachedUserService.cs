using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Caching;

/// <summary>
/// Cached wrapper for IUserService that provides caching capabilities for user operations.
/// Uses the ICacheService to cache frequently accessed user data with appropriate expiration policies.
/// Implements thread-safe caching with cache invalidation on user updates.
/// </summary>
public class CachedUserService : IUserService
{
    private readonly IUserService _innerUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedUserService> _logger;

    // Cache duration constants
    private static readonly TimeSpan UserCacheDuration = TimeSpan.FromMinutes(15); // Users change infrequently
    private static readonly TimeSpan AuthCacheDuration = TimeSpan.FromMinutes(5); // Authentication should be fresher
    private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(10); // User lists change occasionally

    // Cache key constants
    private const string UserByIdKeyPrefix = "User:ById";
    private const string UserByUsernameKeyPrefix = "User:ByUsername";
    private const string UsernameExistsKeyPrefix = "User:UsernameExists";
    private const string AllUsersKey = "User:AllUsers";
    private const string AuthenticationKeyPrefix = "User:Auth";

    public CachedUserService(
        IUserService innerUserService,
        ICacheService cacheService,
        ILogger<CachedUserService> logger)
    {
        _innerUserService = innerUserService ?? throw new ArgumentNullException(nameof(innerUserService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        // Authentication is sensitive and should not be cached for security reasons
        // We only cache the result temporarily to prevent brute force attacks
        var authKey = $"{AuthenticationKeyPrefix}:{username}:{GetPasswordHash(password)}";

        try
        {
            _logger.LogDebug("Authenticating user: {Username}", username);
            var user = await _innerUserService.AuthenticateAsync(username, password);
            
            if (user != null)
            {
                _logger.LogDebug("Authentication successful for user: {Username}", username);
                
                // Cache the user data for subsequent lookups
                await CacheUserData(user);
            }
            else
            {
                _logger.LogDebug("Authentication failed for user: {Username}", username);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for user: {Username}", username);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username)
    {
        var cacheKey = $"{UserByUsernameKeyPrefix}:{username}";

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for username {Username}, fetching from service", username);
                var user = await _innerUserService.GetByUsernameAsync(username);
                
                if (user != null)
                {
                    _logger.LogDebug("Cached user data for username: {Username} (ID: {UserId})", username, user.Id);
                    // Also cache by ID for cross-reference
                    await CacheUserById(user);
                }
                else
                {
                    _logger.LogDebug("User not found for username: {Username}, caching null result", username);
                }

                return user;
            },
            UserCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(int id)
    {
        var cacheKey = $"{UserByIdKeyPrefix}:{id}";

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for user ID {UserId}, fetching from service", id);
                var user = await _innerUserService.GetByIdAsync(id);
                
                if (user != null)
                {
                    _logger.LogDebug("Cached user data for ID: {UserId} (Username: {Username})", id, user.Username);
                    // Also cache by username for cross-reference
                    await CacheUserByUsername(user);
                }
                else
                {
                    _logger.LogDebug("User not found for ID: {UserId}, caching null result", id);
                }

                return user;
            },
            UserCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _cacheService.GetOrCreateAsync(
            AllUsersKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for all users, fetching from service");
                var users = await _innerUserService.GetAllUsersAsync();
                var usersList = users.ToList();
                
                _logger.LogDebug("Cached {Count} users", usersList.Count);
                
                // Cache individual users for faster individual lookups
                foreach (var user in usersList)
                {
                    await CacheUserData(user);
                }

                return usersList;
            },
            ListCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<User> CreateUserAsync(string username, string password, string role)
    {
        try
        {
            _logger.LogInformation("Creating new user: {Username} with role: {Role}", username, role);
            var user = await _innerUserService.CreateUserAsync(username, password, role);
            
            // Invalidate caches that might be affected
            await InvalidateUserListCacheAsync();
            await InvalidateUsernameExistsCacheAsync(username);
            
            // Cache the new user
            await CacheUserData(user);
            
            _logger.LogInformation("Successfully created and cached user: {Username} (ID: {UserId})", username, user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", username);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        try
        {
            _logger.LogInformation("Resetting password for user ID: {UserId}", userId);
            var result = await _innerUserService.ResetPasswordAsync(userId, newPassword);
            
            if (result)
            {
                // Invalidate authentication-related caches for this user
                await InvalidateUserCacheAsync(userId);
                _logger.LogInformation("Password reset successful and cache invalidated for user ID: {UserId}", userId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user ID: {UserId}", userId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateUserAsync(User user)
    {
        try
        {
            _logger.LogInformation("Updating user: {Username} (ID: {UserId})", user.Username, user.Id);
            var result = await _innerUserService.UpdateUserAsync(user);
            
            if (result)
            {
                // Invalidate all caches for this user
                await InvalidateUserCacheAsync(user.Id);
                await InvalidateUserCacheAsync(user.Username);
                await InvalidateUserListCacheAsync();
                
                _logger.LogInformation("User updated successfully and cache invalidated: {Username} (ID: {UserId})", user.Username, user.Id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {Username} (ID: {UserId})", user.Username, user.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUserAsync(int userId)
    {
        try
        {
            // Get user info before deletion for cache invalidation
            var user = await GetByIdAsync(userId);
            
            _logger.LogInformation("Deleting user ID: {UserId}", userId);
            var result = await _innerUserService.DeleteUserAsync(userId);
            
            if (result)
            {
                // Invalidate all caches for this user
                await InvalidateUserCacheAsync(userId);
                if (user != null)
                {
                    await InvalidateUserCacheAsync(user.Username);
                    await InvalidateUsernameExistsCacheAsync(user.Username);
                }
                await InvalidateUserListCacheAsync();
                
                _logger.LogInformation("User deleted successfully and cache invalidated for user ID: {UserId}", userId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user ID: {UserId}", userId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UsernameExistsAsync(string username)
    {
        var cacheKey = $"{UsernameExistsKeyPrefix}:{username}";

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for username exists check: {Username}, fetching from service", username);
                var exists = await _innerUserService.UsernameExistsAsync(username);
                _logger.LogDebug("Username exists check result for {Username}: {Exists}", username, exists);
                return exists;
            },
            UserCacheDuration
        );
    }

    /// <summary>
    /// Caches user data using both ID and username keys for cross-reference lookups.
    /// </summary>
    private async Task CacheUserData(User user)
    {
        if (user == null) return;

        try
        {
            await CacheUserById(user);
            await CacheUserByUsername(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching user data for user: {Username} (ID: {UserId})", user.Username, user.Id);
        }
    }

    /// <summary>
    /// Caches user data by ID.
    /// </summary>
    private async Task CacheUserById(User user)
    {
        var cacheKey = $"{UserByIdKeyPrefix}:{user.Id}";
        await _cacheService.SetAsync(user, cacheKey, UserCacheDuration);
    }

    /// <summary>
    /// Caches user data by username.
    /// </summary>
    private async Task CacheUserByUsername(User user)
    {
        var cacheKey = $"{UserByUsernameKeyPrefix}:{user.Username}";
        await _cacheService.SetAsync(user, cacheKey, UserCacheDuration);
    }

    /// <summary>
    /// Invalidates all cache entries for a specific user by ID.
    /// </summary>
    private async Task InvalidateUserCacheAsync(int userId)
    {
        try
        {
            await _cacheService.RemoveAsync($"{UserByIdKeyPrefix}:{userId}");
            await _cacheService.RemoveByPatternAsync($"{AuthenticationKeyPrefix}:*");
            _logger.LogDebug("Invalidated cache entries for user ID: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for user ID: {UserId}", userId);
        }
    }

    /// <summary>
    /// Invalidates all cache entries for a specific user by username.
    /// </summary>
    private async Task InvalidateUserCacheAsync(string username)
    {
        try
        {
            await _cacheService.RemoveAsync($"{UserByUsernameKeyPrefix}:{username}");
            await _cacheService.RemoveAsync($"{UsernameExistsKeyPrefix}:{username}");
            await _cacheService.RemoveByPatternAsync($"{AuthenticationKeyPrefix}:{username}:*");
            _logger.LogDebug("Invalidated cache entries for username: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for username: {Username}", username);
        }
    }

    /// <summary>
    /// Invalidates the username exists cache for a specific username.
    /// </summary>
    private async Task InvalidateUsernameExistsCacheAsync(string username)
    {
        try
        {
            await _cacheService.RemoveAsync($"{UsernameExistsKeyPrefix}:{username}");
            _logger.LogDebug("Invalidated username exists cache for: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating username exists cache for: {Username}", username);
        }
    }

    /// <summary>
    /// Invalidates the all users list cache.
    /// </summary>
    private async Task InvalidateUserListCacheAsync()
    {
        try
        {
            await _cacheService.RemoveAsync(AllUsersKey);
            _logger.LogDebug("Invalidated all users list cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all users list cache");
        }
    }

    /// <summary>
    /// Creates a simple hash of the password for cache key generation.
    /// This is only used for cache key generation, not for security.
    /// </summary>
    private static string GetPasswordHash(string password)
    {
        // Simple hash for cache key - not for security
        return password.GetHashCode().ToString();
    }

    /// <summary>
    /// Invalidates all user-related cache entries.
    /// Should be called during maintenance or when bulk user operations are performed.
    /// </summary>
    public async Task InvalidateAllUserCacheAsync()
    {
        try
        {
            await _cacheService.RemoveByPatternAsync($"{UserByIdKeyPrefix}:*");
            await _cacheService.RemoveByPatternAsync($"{UserByUsernameKeyPrefix}:*");
            await _cacheService.RemoveByPatternAsync($"{UsernameExistsKeyPrefix}:*");
            await _cacheService.RemoveByPatternAsync($"{AuthenticationKeyPrefix}:*");
            await _cacheService.RemoveAsync(AllUsersKey);
            
            _logger.LogInformation("Successfully invalidated all user cache entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all user cache entries");
        }
    }

    /// <summary>
    /// Pre-loads frequently accessed user data into cache.
    /// Can be called during application startup or maintenance windows.
    /// </summary>
    public async Task WarmUpCacheAsync()
    {
        try
        {
            _logger.LogInformation("Starting user cache warm-up");

            // Warm up the most commonly accessed data
            await GetAllUsersAsync();

            _logger.LogInformation("User cache warm-up completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user cache warm-up");
        }
    }

    /// <summary>
    /// Gets cache statistics for user-related entries.
    /// Useful for monitoring and performance analysis.
    /// </summary>
    /// <returns>Cache statistics summary</returns>
    public CacheStatisticsSummary GetUserCacheStatistics()
    {
        try
        {
            var stats = _cacheService.GetStatistics();
            
            return new CacheStatisticsSummary
            {
                TotalHits = stats.HitCount,
                TotalMisses = stats.MissCount,
                HitRatio = stats.HitRatio,
                EntryCount = stats.CurrentEntryCount,
                MemoryUsage = 0 // Memory usage not available in current implementation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user cache statistics");
            return new CacheStatisticsSummary();
        }
    }
}