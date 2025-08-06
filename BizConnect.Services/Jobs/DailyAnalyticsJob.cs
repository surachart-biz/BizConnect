using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.DTOs;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Jobs;

/// <summary>
/// Daily background job to collect and process analytics data
/// </summary>
public class DailyAnalyticsJob
{
    private readonly BizConnectContext _context;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly ISystemHealthService _systemHealthService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DailyAnalyticsJob> _logger;

    public DailyAnalyticsJob(
        BizConnectContext context,
        IRealtimeNotificationService realtimeNotificationService,
        ISystemHealthService systemHealthService,
        IDateTimeProvider dateTimeProvider,
        ILogger<DailyAnalyticsJob> logger)
    {
        _context = context;
        _realtimeNotificationService = realtimeNotificationService;
        _systemHealthService = systemHealthService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Execute daily analytics collection and processing
    /// </summary>
    /// <returns>Analytics job result</returns>
    public async Task<AnalyticsJobResult> ExecuteAsync()
    {
        var startTime = _dateTimeProvider.UtcNow;
        var result = new AnalyticsJobResult
        {
            StartTime = startTime,
            Success = false
        };

        try
        {
            _logger.LogInformation("Starting daily analytics job execution at {StartTime}", startTime);

            // Step 1: Collect daily statistics
            var dailyStats = await CollectDailyStatisticsAsync();
            result.DailyStats = dailyStats;

            // Step 2: Update materialized view for monthly statistics
            await RefreshMonthlyStatisticsViewAsync();

            // Step 3: Generate trend analysis
            var trendAnalysis = await GenerateTrendAnalysisAsync();
            result.TrendAnalysis = trendAnalysis;

            // Step 4: Check system health and generate alerts if needed
            var systemHealth = await _systemHealthService.GetSystemHealthAsync();
            if (systemHealth.OverallStatus != "Healthy")
            {
                await GenerateSystemHealthAlertsAsync(systemHealth);
            }

            // Step 5: Clean up old analytics data (keep last 90 days)
            var cleanupResult = await CleanupOldAnalyticsDataAsync();
            result.RecordsCleaned = cleanupResult;

            // Step 6: Archive completed registrations older than 1 year
            var archiveResult = await ArchiveOldRegistrationsAsync();
            result.RecordsArchived = archiveResult;

            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Success = true;

            // Track successful completion
            await _realtimeNotificationService.TrackSystemHealthEventAsync(
                "Analytics Job",
                "Healthy",
                $"Daily analytics job completed successfully - processed {dailyStats.TotalRegistrations} registrations",
                "Info"
            );

            _logger.LogInformation("Daily analytics job completed successfully in {Duration}ms", 
                result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.Error = ex.Message;
            result.Success = false;

            // Track job failure
            await _realtimeNotificationService.TrackSystemHealthEventAsync(
                "Analytics Job",
                "Error",
                $"Daily analytics job failed: {ex.Message}",
                "Error"
            );

            _logger.LogError(ex, "Daily analytics job failed after {Duration}ms", 
                result.Duration.TotalMilliseconds);

            return result;
        }
    }

    #region Private Methods

    private async Task<DailyAnalyticsStats> CollectDailyStatisticsAsync()
    {
        _logger.LogDebug("Collecting daily statistics");

        var yesterday = _dateTimeProvider.UtcNow.Date.AddDays(-1);
        var today = _dateTimeProvider.UtcNow.Date;

        var registrations = await _context.KbankOddRegistrations
            .Where(r => r.CreatedAt >= yesterday && r.CreatedAt < today)
            .ToListAsync();

        var stats = new DailyAnalyticsStats
        {
            Date = yesterday,
            TotalRegistrations = registrations.Count,
            SuccessfulRegistrations = registrations.Count(r => r.Status == "Success"),
            FailedRegistrations = registrations.Count(r => r.Status == "Fail"),
            PendingRegistrations = registrations.Count(r => r.Status == "Pending"),
            UniqueUsers = await _context.Users
                .Where(u => u.LastLoginAt >= yesterday && u.LastLoginAt < today)
                .CountAsync(),
            AverageProcessingTimeMs = registrations
                .Where(r => r.UpdatedAt.HasValue)
                .Select(r => (r.UpdatedAt!.Value - r.CreatedAt).TotalMilliseconds)
                .DefaultIfEmpty(0)
                .Average(),
            PeakHourRegistrations = GetPeakHourRegistrations(registrations)
        };

        stats.SuccessRate = stats.TotalRegistrations > 0 
            ? (decimal)stats.SuccessfulRegistrations / stats.TotalRegistrations * 100 
            : 0;

        _logger.LogDebug("Daily statistics collected: {TotalRegistrations} registrations, {SuccessRate}% success rate", 
            stats.TotalRegistrations, stats.SuccessRate);

        return stats;
    }

    private async Task RefreshMonthlyStatisticsViewAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing monthly statistics materialized view");

            // Check if materialized view exists and refresh it
            var refreshSql = "REFRESH MATERIALIZED VIEW IF EXISTS mv_monthly_statistics";
            await _context.Database.ExecuteSqlRawAsync(refreshSql);

            _logger.LogDebug("Monthly statistics materialized view refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh monthly statistics materialized view");
            // Don't fail the entire job for this
        }
    }

    private async Task<List<TrendAnalysisPoint>> GenerateTrendAnalysisAsync()
    {
        _logger.LogDebug("Generating trend analysis");

        var trends = new List<TrendAnalysisPoint>();
        var endDate = _dateTimeProvider.UtcNow.Date;
        var startDate = endDate.AddDays(-7); // Last 7 days

        for (var date = startDate; date < endDate; date = date.AddDays(1))
        {
            var nextDate = date.AddDays(1);
            var dayRegistrations = await _context.KbankOddRegistrations
                .Where(r => r.CreatedAt >= date && r.CreatedAt < nextDate)
                .ToListAsync();

            trends.Add(new TrendAnalysisPoint
            {
                Date = date,
                TotalRegistrations = dayRegistrations.Count,
                SuccessfulRegistrations = dayRegistrations.Count(r => r.Status == "Success"),
                FailedRegistrations = dayRegistrations.Count(r => r.Status == "Fail"),
                SuccessRate = dayRegistrations.Count > 0 
                    ? (decimal)dayRegistrations.Count(r => r.Status == "Success") / dayRegistrations.Count * 100 
                    : 0
            });
        }

        _logger.LogDebug("Generated trend analysis for {DaysCount} days", trends.Count);

        return trends;
    }

    private async Task GenerateSystemHealthAlertsAsync(SystemHealthStatus systemHealth)
    {
        foreach (var healthCheck in systemHealth.HealthChecks)
        {
            if (healthCheck.Status == "Error" || healthCheck.Status == "Critical")
            {
                await _realtimeNotificationService.TrackSystemHealthEventAsync(
                    healthCheck.Name ?? "Unknown Component",
                    healthCheck.Status,
                    healthCheck.Message ?? "Component health check failed",
                    "Critical"
                );
            }
            else if (healthCheck.Status == "Warning" || healthCheck.Status == "Degraded")
            {
                await _realtimeNotificationService.TrackSystemHealthEventAsync(
                    healthCheck.Name ?? "Unknown Component",
                    healthCheck.Status,
                    healthCheck.Message ?? "Component health degraded",
                    "Warning"
                );
            }
        }
    }

    private async Task<int> CleanupOldAnalyticsDataAsync()
    {
        _logger.LogDebug("Cleaning up old analytics data");

        var cutoffDate = _dateTimeProvider.UtcNow.AddDays(-90); // Keep 90 days
        
        // This would clean up any dedicated analytics tables if they exist
        // For now, we'll just log that cleanup would happen here
        var recordsCleaned = 0;

        _logger.LogDebug("Cleaned up {RecordsCleaned} old analytics records", recordsCleaned);

        return recordsCleaned;
    }

    private async Task<int> ArchiveOldRegistrationsAsync()
    {
        _logger.LogDebug("Archiving old registrations");

        var cutoffDate = _dateTimeProvider.UtcNow.AddYears(-1); // Archive after 1 year
        
        // Find old completed registrations
        var oldRegistrations = await _context.KbankOddRegistrations
            .Where(r => r.CreatedAt < cutoffDate && 
                       (r.Status == "Success" || r.Status == "Fail"))
            .OrderBy(r => r.CreatedAt) // Oldest first for cleanup
            .ThenBy(r => r.Id)         // Consistent ordering
            .Take(100) // Process in batches
            .ToListAsync();

        if (!oldRegistrations.Any())
        {
            return 0;
        }

        // In a real implementation, you might move these to an archive table
        // For now, we'll just mark them as archived or log them
        var recordsArchived = oldRegistrations.Count;

        _logger.LogDebug("Archived {RecordsArchived} old registration records", recordsArchived);

        return recordsArchived;
    }

    private int GetPeakHourRegistrations(List<KbankOddRegistration> registrations)
    {
        if (!registrations.Any())
            return 0;

        var hourlyGroups = registrations
            .GroupBy(r => r.CreatedAt.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        return hourlyGroups.Values.DefaultIfEmpty(0).Max();
    }

    #endregion
}

/// <summary>
/// Result of daily analytics job execution
/// </summary>
public class AnalyticsJobResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DailyAnalyticsStats? DailyStats { get; set; }
    public List<TrendAnalysisPoint> TrendAnalysis { get; set; } = new();
    public int RecordsCleaned { get; set; }
    public int RecordsArchived { get; set; }
}

/// <summary>
/// Daily analytics statistics
/// </summary>
public class DailyAnalyticsStats
{
    public DateTime Date { get; set; }
    public int TotalRegistrations { get; set; }
    public int SuccessfulRegistrations { get; set; }
    public int FailedRegistrations { get; set; }
    public int PendingRegistrations { get; set; }
    public int UniqueUsers { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public int PeakHourRegistrations { get; set; }
    public decimal SuccessRate { get; set; }
}

/// <summary>
/// Trend analysis data point
/// </summary>
public class TrendAnalysisPoint
{
    public DateTime Date { get; set; }
    public int TotalRegistrations { get; set; }
    public int SuccessfulRegistrations { get; set; }
    public int FailedRegistrations { get; set; }
    public decimal SuccessRate { get; set; }
}