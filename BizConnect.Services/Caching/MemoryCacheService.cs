using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Caching;

/// <summary>
/// Memory cache implementation of the ICacheService interface.
/// Provides high-performance in-memory caching with comprehensive statistics tracking.
/// Implements Phase 3A.1 caching infrastructure specification.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, bool> _keyTracker;
    private readonly object _lockObject = new();
    
    // Statistics tracking for performance monitoring
    private long _hitCount = 0;
    private long _missCount = 0;
    private long _evictionCount = 0;
    
    // Default sliding expiration: 30 minutes as specified
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(30);

    public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyTracker = new ConcurrentDictionary<string, bool>();
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        try
        {
            var success = _memoryCache.TryGetValue(key, out var value);
            
            if (success)
            {
                Interlocked.Increment(ref _hitCount);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return Task.FromResult(value is T typedValue ? typedValue : default(T?));
            }
            else
            {
                Interlocked.Increment(ref _missCount);
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return Task.FromResult(default(T?));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            return Task.FromResult(default(T?));
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(T value, string key, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        try
        {
            var cacheExpiration = expiration ?? DefaultCacheDuration;
            
            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = cacheExpiration,
                Priority = CacheItemPriority.Normal
            };

            // Register callback to track key removal
            options.RegisterPostEvictionCallback(OnCacheEntryEvicted);

            _memoryCache.Set(key, value, options);
            _keyTracker.TryAdd(key, true);
            
            _logger.LogDebug("Set cache entry for key: {Key} with expiration: {Expiration}", key, cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));

        try
        {
            _memoryCache.Remove(key);
            _keyTracker.TryRemove(key, out _);
            _logger.LogDebug("Removed cache entry for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveByPatternAsync(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Key pattern cannot be null or whitespace.", nameof(pattern));

        try
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            
            var keysToRemove = _keyTracker.Keys.Where(key => regex.IsMatch(key)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
                _keyTracker.TryRemove(key, out _);
            }
            
            _logger.LogDebug("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        try
        {
            // Try to get from cache first
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            // Use lock to prevent multiple threads from executing the expensive operation
            lock (_lockObject)
            {
                // Double-check after acquiring lock
                if (_memoryCache.TryGetValue(key, out var lockValue) && lockValue is T lockTypedValue)
                {
                    _logger.LogDebug("Cache hit after lock for key: {Key}", key);
                    return lockTypedValue;
                }
            }

            // Not in cache, execute the factory function
            _logger.LogDebug("Cache miss for key: {Key}, executing factory function", key);
            var value = await factory();
            
            // Cache the result
            await SetAsync(value, key, expiration);
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrCreateAsync for key: {Key}", key);
            
            // If caching fails, still try to get the value
            try
            {
                return await factory();
            }
            catch (Exception getEx)
            {
                _logger.LogError(getEx, "Error executing factory function for key: {Key}", key);
                throw;
            }
        }
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            HitCount = Interlocked.Read(ref _hitCount),
            MissCount = Interlocked.Read(ref _missCount),
            EvictionCount = Interlocked.Read(ref _evictionCount),
            CurrentEntryCount = _keyTracker.Count,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Callback method invoked when a cache entry is evicted.
    /// Updates internal key tracking for pattern-based operations.
    /// </summary>
    private void OnCacheEntryEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (key is string stringKey)
        {
            _keyTracker.TryRemove(stringKey, out _);
            Interlocked.Increment(ref _evictionCount);
            _logger.LogDebug("Cache entry evicted for key: {Key}, Reason: {Reason}", stringKey, reason);
        }
    }
}