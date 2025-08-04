using System;
using System.Threading.Tasks;

namespace BizConnect.Services.Caching;

/// <summary>
/// Interface for cache service operations.
/// Provides caching capabilities with support for expiration, pattern-based removal, and statistics tracking.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <returns>The cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Sets a value in the cache with optional expiration.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache</typeparam>
    /// <param name="value">Value to cache</param>
    /// <param name="key">Cache key</param>
    /// <param name="expiration">Optional expiration time</param>
    Task SetAsync<T>(T value, string key, TimeSpan? expiration = null);

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes cached values matching a pattern.
    /// </summary>
    /// <param name="pattern">Pattern to match (supports wildcards)</param>
    Task RemoveByPatternAsync(string pattern);

    /// <summary>
    /// Gets or creates a cached value using a factory function.
    /// </summary>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Factory function to create the value if not cached</param>
    /// <param name="expiration">Optional expiration time</param>
    /// <returns>The cached or newly created value</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// Gets cache statistics for monitoring and performance analysis.
    /// </summary>
    /// <returns>Current cache statistics</returns>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Cache performance statistics for monitoring and analytics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache hits
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// Total number of cache misses
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// Number of entries evicted from cache
    /// </summary>
    public long EvictionCount { get; set; }

    /// <summary>
    /// Current number of entries in cache
    /// </summary>
    public int CurrentEntryCount { get; set; }

    /// <summary>
    /// Cache hit ratio (0.0 to 1.0)
    /// </summary>
    public double HitRatio => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0.0;

    /// <summary>
    /// Timestamp when statistics were captured
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of cache operations
    /// </summary>
    public long TotalOperations => HitCount + MissCount;

    /// <summary>
    /// Creates a successful Result containing these cache statistics
    /// </summary>
    public BizConnect.Services.Common.Result<CacheStatistics> Result()
    {
        return BizConnect.Services.Common.Result<CacheStatistics>.Success(this);
    }
}