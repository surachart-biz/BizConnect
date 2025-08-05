using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Services.Models.Results;
using BizConnect.Dal.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Caching;

/// <summary>
/// Cached wrapper for IBranchService that provides caching capabilities for branch operations.
/// Uses the ICacheService to cache frequently accessed branch data with appropriate expiration policies.
/// </summary>
public class CachedBranchService : IBranchService
{
    private readonly IBranchService _innerBranchService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedBranchService> _logger;

    // Cache duration constants
    private static readonly TimeSpan BranchCacheDuration = TimeSpan.FromMinutes(30); // Branches don't change often
    private static readonly TimeSpan ActiveBranchCacheDuration = TimeSpan.FromMinutes(60); // Active status changes even less
    private static readonly TimeSpan DropdownCacheDuration = TimeSpan.FromHours(2); // Dropdown data can be cached longer

    // Cache key constants
    private const string AllActiveBranchesKey = "BranchService:AllActiveBranches";
    private const string ActiveBranchesDropdownKey = "BranchService:ActiveBranchesDropdown";
    private const string BranchByIdKeyPrefix = "BranchService:BranchById";
    private const string BranchNameKeyPrefix = "BranchService:BranchName";
    private const string BranchActiveStatusKeyPrefix = "BranchService:BranchActiveStatus";

    public CachedBranchService(
        IBranchService innerBranchService,
        ICacheService cacheService,
        ILogger<CachedBranchService> logger)
    {
        _innerBranchService = innerBranchService ?? throw new ArgumentNullException(nameof(innerBranchService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<(int BranchId, string Name)>> GetActiveBranchesForDropdownAsync(string language = "en")
    {
        var cacheKey = $"{ActiveBranchesDropdownKey}:{language}";
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for active branches dropdown in {Language}, fetching from service", language);
                var branches = await _innerBranchService.GetActiveBranchesForDropdownAsync(language);
                _logger.LogDebug("Cached {Count} active branches for dropdown in {Language}", branches.Count, language);
                return branches;
            },
            DropdownCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<Branch?> GetBranchByIdAsync(int id)
    {
        var cacheKey = $"{BranchByIdKeyPrefix}:{id}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for branch ID {BranchId}, fetching from service", id);
                var branch = await _innerBranchService.GetBranchByIdAsync(id);
                
                if (branch != null)
                {
                    _logger.LogDebug("Cached branch: {BranchName} (ID: {BranchId})", branch.Name, id);
                }
                else
                {
                    _logger.LogDebug("Branch not found for ID {BranchId}, caching null result", id);
                }
                
                return branch;
            },
            BranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Branch>> GetAllActiveBranchesAsync()
    {
        return await _cacheService.GetOrCreateAsync(
            AllActiveBranchesKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for all active branches, fetching from service");
                var branches = await _innerBranchService.GetAllActiveBranchesAsync();
                var branchesList = branches.ToList();
                _logger.LogDebug("Cached {Count} active branches", branchesList.Count);
                return branchesList;
            },
            ActiveBranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<bool> IsActiveBranchAsync(int branchId)
    {
        var cacheKey = $"{BranchActiveStatusKeyPrefix}:{branchId}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for branch active status {BranchId}, fetching from service", branchId);
                var isActive = await _innerBranchService.IsActiveBranchAsync(branchId);
                _logger.LogDebug("Cached active status for branch {BranchId}: {IsActive}", branchId, isActive);
                return isActive;
            },
            ActiveBranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<string?> GetBranchNameAsync(int branchId, string language = "en")
    {
        var cacheKey = $"{BranchNameKeyPrefix}:{branchId}:{language}";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for branch name {BranchId} in {Language}, fetching from service", branchId, language);
                var name = await _innerBranchService.GetBranchNameAsync(branchId, language);
                
                if (name != null)
                {
                    _logger.LogDebug("Cached branch name: {BranchName} for ID {BranchId} in {Language}", name, branchId, language);
                }
                else
                {
                    _logger.LogDebug("Branch name not found for ID {BranchId} in {Language}, caching null result", branchId, language);
                }
                
                return name;
            },
            BranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
    {
        const string cacheKey = "BranchService:AllBranches";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for all branches, fetching from service");
                var branches = await _innerBranchService.GetAllBranchesAsync();
                var branchesList = branches.ToList();
                _logger.LogDebug("Cached {Count} branches (all)", branchesList.Count);
                return branchesList;
            },
            BranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<Branch>>> GetActiveBranchesAsync()
    {
        const string cacheKey = "BranchService:ActiveBranchesResult";
        
        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for active branches (Result pattern), fetching from service");
                var result = await _innerBranchService.GetActiveBranchesAsync();
                _logger.LogDebug("Cached active branches result: Success={IsSuccess}", result.IsSuccess);
                return result;
            },
            ActiveBranchCacheDuration
        );
    }

    /// <inheritdoc />
    public async Task<Result<List<BranchPerformance>>> GetBranchPerformanceAsync(int days = 30, string language = "en")
    {
        // Don't cache performance data as it changes frequently and is computationally expensive
        _logger.LogDebug("Fetching branch performance data for {Days} days in {Language} (not cached)", days, language);
        return await _innerBranchService.GetBranchPerformanceAsync(days, language);
    }

    /// <summary>
    /// Invalidates all branch-related cache entries.
    /// Should be called when branch data is modified in the system.
    /// </summary>
    public async Task InvalidateBranchCacheAsync()
    {
        try
        {
            // Remove all branch-related cache entries
            await _cacheService.RemoveAsync(AllActiveBranchesKey);
            await _cacheService.RemoveAsync(ActiveBranchesDropdownKey);
            await _cacheService.RemoveByPatternAsync($"{BranchByIdKeyPrefix}:*");
            await _cacheService.RemoveByPatternAsync($"{BranchNameKeyPrefix}:*");
            await _cacheService.RemoveByPatternAsync($"{BranchActiveStatusKeyPrefix}:*");
            
            _logger.LogInformation("Successfully invalidated all branch cache entries");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating branch cache entries");
        }
    }

    /// <summary>
    /// Invalidates cache entries for a specific branch.
    /// Should be called when a specific branch is modified.
    /// </summary>
    /// <param name="branchId">The ID of the branch to invalidate cache for</param>
    public async Task InvalidateBranchCacheAsync(int branchId)
    {
        try
        {
            // Remove specific branch cache entries
            await _cacheService.RemoveAsync($"{BranchByIdKeyPrefix}:{branchId}");
            await _cacheService.RemoveAsync($"{BranchNameKeyPrefix}:{branchId}");
            await _cacheService.RemoveAsync($"{BranchActiveStatusKeyPrefix}:{branchId}");
            
            // Also invalidate collection caches as they might be affected
            await _cacheService.RemoveAsync(AllActiveBranchesKey);
            await _cacheService.RemoveAsync(ActiveBranchesDropdownKey);
            
            _logger.LogInformation("Successfully invalidated cache entries for branch {BranchId}", branchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache entries for branch {BranchId}", branchId);
        }
    }

    /// <summary>
    /// Pre-loads frequently accessed branch data into cache.
    /// Can be called during application startup or maintenance windows.
    /// </summary>
    public async Task WarmUpCacheAsync()
    {
        try
        {
            _logger.LogInformation("Starting branch cache warm-up");

            // Warm up the most commonly accessed data for both languages
            await GetActiveBranchesForDropdownAsync("en");
            await GetActiveBranchesForDropdownAsync("th");
            await GetAllActiveBranchesAsync();

            _logger.LogInformation("Branch cache warm-up completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during branch cache warm-up");
        }
    }

    /// <summary>
    /// Gets cache statistics for branch-related entries.
    /// Useful for monitoring and performance analysis.
    /// </summary>
    /// <returns>Cache statistics summary</returns>
    public CacheStatisticsSummary GetBranchCacheStatistics()
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
            _logger.LogError(ex, "Error getting branch cache statistics");
            return new CacheStatisticsSummary();
        }
    }
}

/// <summary>
/// Summary of cache statistics for monitoring purposes.
/// </summary>
public class CacheStatisticsSummary
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public double HitRatio { get; set; }
    public long EntryCount { get; set; }
    public long MemoryUsage { get; set; }
}