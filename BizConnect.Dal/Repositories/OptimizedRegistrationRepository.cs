using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Dal.Repositories;

/// <summary>
/// Optimized repository for KbankOddRegistration entities with performance-focused query implementations.
/// Includes specialized methods for common registration queries with optimal Entity Framework usage.
/// Follows Phase 3A.1 database optimization specifications.
/// </summary>
public class OptimizedRegistrationRepository : Repository<KbankOddRegistration>
{
    private readonly ILogger<OptimizedRegistrationRepository> _logger;

    public OptimizedRegistrationRepository(
        BizConnectContext context,
        ILogger<OptimizedRegistrationRepository> logger) : base(context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets registrations by status with optimized query and projection.
    /// Uses indexed fields for efficient filtering and includes only necessary data.
    /// Batch size: 100 records per batch for optimal performance.
    /// </summary>
    /// <param name="status">The registration status to filter by</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result of registration summaries</returns>
    public async Task<PagedResult<RegistrationSummary>> GetRegistrationsByStatusOptimizedAsync(
        string status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching optimized registrations by status: {Status}, Page: {PageNumber}, Size: {PageSize}", 
            status, pageNumber, pageSize);

        var query = Context.Set<KbankOddRegistration>()
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistrationSummary
            {
                Id = r.Id,
                FullName = r.FullName ?? "N/A",
                IdValue = r.IdValue ?? "N/A",
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt ?? r.CreatedAt,
                ExternalReference = r.ExternalReference,
                MobileNo = r.MobileNo ?? "N/A"
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} registrations out of {Total} total for status {Status}", 
            items.Count, totalCount, status);

        return new PagedResult<RegistrationSummary>
        {
            Items = items.Cast<RegistrationSummary>(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets registration statistics grouped by status with a single optimized query.
    /// Uses aggregation functions for efficient counting and grouping.
    /// </summary>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of status counts</returns>
    public async Task<Dictionary<string, int>> GetRegistrationStatisticsOptimizedAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching optimized registration statistics from {FromDate} to {ToDate}", fromDate, toDate);

        var query = Context.Set<KbankOddRegistration>()
            .AsNoTracking();

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= toDate.Value);
        }

        var statistics = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = statistics.ToDictionary(s => s.Status, s => s.Count);
        
        _logger.LogDebug("Retrieved statistics for {StatusCount} different statuses", result.Count);
        
        return result;
    }

    /// <summary>
    /// Gets recent registrations with minimal data projection for dashboard display.
    /// Optimized for quick loading with only essential fields.
    /// </summary>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent registration summaries</returns>
    public async Task<List<RegistrationSummary>> GetRecentRegistrationsOptimizedAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching {Limit} recent registrations with optimized query", limit);

        var registrations = await Context.Set<KbankOddRegistration>()
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new RegistrationSummary
            {
                Id = r.Id,
                FullName = r.FullName ?? "N/A",
                IdValue = r.IdValue ?? "N/A",
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt ?? r.CreatedAt,
                ExternalReference = r.ExternalReference,
                MobileNo = r.MobileNo ?? "N/A"
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} recent registrations", registrations.Count);
        
        return registrations;
    }

    /// <summary>
    /// Searches registrations with optimized full-text search across multiple fields.
    /// Uses LIKE operations with proper indexing considerations.
    /// </summary>
    /// <param name="searchTerm">The search term to look for</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    public async Task<PagedResult<RegistrationSummary>> SearchRegistrationsOptimizedAsync(
        string searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new PagedResult<RegistrationSummary>
            {
                Items = Enumerable.Empty<RegistrationSummary>(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        _logger.LogDebug("Searching registrations with term: {SearchTerm}, Page: {PageNumber}, Size: {PageSize}", 
            searchTerm, pageNumber, pageSize);

        var searchTermLower = searchTerm.ToLower();
        
        var query = Context.Set<KbankOddRegistration>()
            .AsNoTracking()
            .Where(r => 
                (r.FullName != null && EF.Functions.Like(r.FullName.ToLower(), $"%{searchTermLower}%")) ||
                (r.IdValue != null && EF.Functions.Like(r.IdValue, $"%{searchTerm}%")) ||
                EF.Functions.Like(r.ExternalReference, $"%{searchTerm}%") ||
                (r.MobileNo != null && EF.Functions.Like(r.MobileNo, $"%{searchTerm}%")) ||
                EF.Functions.Like(r.Status.ToLower(), $"%{searchTermLower}%"))
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RegistrationSummary
            {
                Id = r.Id,
                FullName = r.FullName ?? "N/A",
                IdValue = r.IdValue ?? "N/A",
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt ?? r.CreatedAt,
                ExternalReference = r.ExternalReference,
                MobileNo = r.MobileNo ?? "N/A"
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Search returned {Count} registrations out of {Total} total matches", 
            items.Count, totalCount);

        return new PagedResult<RegistrationSummary>
        {
            Items = items.Cast<RegistrationSummary>(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Gets registration trends over time with optimized aggregation.
    /// Groups registrations by date and status for trend analysis.
    /// </summary>
    /// <param name="fromDate">Start date for trend analysis</param>
    /// <param name="toDate">End date for trend analysis</param>
    /// <param name="groupBy">Grouping interval (day, week, month)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of trend data points</returns>
    public async Task<List<RegistrationTrendData>> GetRegistrationTrendsOptimizedAsync(
        DateTime fromDate,
        DateTime toDate,
        string groupBy = "day",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching registration trends from {FromDate} to {ToDate} grouped by {GroupBy}", 
            fromDate, toDate, groupBy);

        var query = Context.Set<KbankOddRegistration>()
            .AsNoTracking()
            .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate);

        List<RegistrationTrendData> trends;

        switch (groupBy.ToLower())
        {
            case "week":
                // For PostgreSQL, we'll use a simpler approach - group by week of year
                trends = await query
                    .GroupBy(r => new { 
                        Year = r.CreatedAt.Year, 
                        Week = (r.CreatedAt.DayOfYear - 1) / 7 + 1, // Simple week calculation
                        Status = r.Status 
                    })
                    .Select(g => new RegistrationTrendData
                    {
                        Period = $"{g.Key.Year}-W{g.Key.Week:00}",
                        Status = g.Key.Status,
                        Count = g.Count(),
                        Date = new DateTime(g.Key.Year, 1, 1).AddDays((g.Key.Week - 1) * 7)
                    })
                    .OrderBy(t => t.Period)
                    .ToListAsync(cancellationToken);
                break;

            case "month":
                trends = await query
                    .GroupBy(r => new { 
                        Year = r.CreatedAt.Year, 
                        Month = r.CreatedAt.Month,
                        Status = r.Status 
                    })
                    .Select(g => new RegistrationTrendData
                    {
                        Period = $"{g.Key.Year}-{g.Key.Month:00}",
                        Status = g.Key.Status,
                        Count = g.Count(),
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1)
                    })
                    .OrderBy(t => t.Period)
                    .ToListAsync(cancellationToken);
                break;

            default: // day
                trends = await query
                    .GroupBy(r => new { 
                        Date = r.CreatedAt.Date,
                        Status = r.Status 
                    })
                    .Select(g => new RegistrationTrendData
                    {
                        Period = g.Key.Date.ToString("yyyy-MM-dd"),
                        Status = g.Key.Status,
                        Count = g.Count(),
                        Date = g.Key.Date
                    })
                    .OrderBy(t => t.Date)
                    .ToListAsync(cancellationToken);
                break;
        }

        _logger.LogDebug("Retrieved {Count} trend data points for {GroupBy} grouping", trends.Count, groupBy);
        
        return trends;
    }

    /// <summary>
    /// Batch updates registration statuses with optimized bulk operation.
    /// Uses ExecuteUpdate for maximum performance on large updates.
    /// Batch size: 100 records per batch as specified.
    /// </summary>
    /// <param name="registrationIds">List of registration IDs to update</param>
    /// <param name="newStatus">The new status to set</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records updated</returns>
    public async Task<int> BatchUpdateStatusOptimizedAsync(
        List<int> registrationIds,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!registrationIds.Any())
            return 0;

        _logger.LogDebug("Batch updating {Count} registrations to status: {NewStatus}", 
            registrationIds.Count, newStatus);

        var totalUpdated = 0;
        const int batchSize = 100; // Batch operations for 100 records per batch as specified

        // Process in batches for optimal performance
        for (int i = 0; i < registrationIds.Count; i += batchSize)
        {
            var batch = registrationIds.Skip(i).Take(batchSize).ToList();
            
            var updatedCount = await Context.Set<KbankOddRegistration>()
                .Where(r => batch.Contains(r.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, newStatus)
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);

            totalUpdated += updatedCount;
            
            _logger.LogDebug("Updated {UpdatedCount} registrations in batch {BatchNumber}", 
                updatedCount, (i / batchSize) + 1);
        }

        _logger.LogDebug("Successfully updated {TotalUpdated} registrations to status: {NewStatus}", 
            totalUpdated, newStatus);

        return totalUpdated;
    }
}

/// <summary>
/// Lightweight registration summary for optimized queries.
/// Contains only the most commonly accessed fields to reduce memory usage and transfer time.
/// </summary>
public class RegistrationSummary
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public string? IdValue { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ExternalReference { get; set; }
    public string? MobileNo { get; set; }
}

/// <summary>
/// Data structure for registration trend analysis.
/// Used for dashboard charts and reporting.
/// </summary>
public class RegistrationTrendData
{
    public string Period { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime Date { get; set; }
}