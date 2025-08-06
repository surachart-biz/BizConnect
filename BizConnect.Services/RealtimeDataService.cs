using BizConnect.Services.DTOs;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using RecentActivityItem = BizConnect.Services.Interfaces.RecentActivityItem;

namespace BizConnect.Services;

/// <summary>
/// Real-time data service implementation with caching and performance optimization
/// </summary>
public class RealtimeDataService : IRealtimeDataService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<RealtimeDataService> _logger;

    // Cache keys for consistent caching strategy
    private const string LIVE_STATS_CACHE_KEY = "dashboard:live_stats";
    private const string RECENT_ACTIVITIES_CACHE_KEY = "dashboard:recent_activities";
    private const string KPI_SUMMARY_CACHE_KEY = "dashboard:kpi_summary";
    
    private static readonly TimeSpan DefaultCacheExpiry = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LiveStatsCacheExpiry = TimeSpan.FromSeconds(30);

    public RealtimeDataService(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IDashboardService dashboardService,
        ILogger<RealtimeDataService> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get live dashboard statistics with <300ms response time requirement
    /// Uses optimized database views and aggressive caching
    /// </summary>
    public async Task<DashboardRealTimeStats> GetLiveStatsAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Try cache first for <50ms response
            var cachedStats = await _cacheService.GetAsync<DashboardRealTimeStats>(LIVE_STATS_CACHE_KEY);
            if (cachedStats != null)
            {
                stopwatch.Stop();
                _logger.LogDebug("Live stats served from cache in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                return cachedStats;
            }

            // Use optimized database view for fast retrieval
            var realTimeStats = await _dashboardService.GetRealTimeStatsAsync();
            var performanceMetrics = await GetCurrentPerformanceSnapshotAsync();

            var liveStats = new DashboardRealTimeStats
            {
                ActiveOtacCodes = realTimeStats.ActiveOtac,
                RegistrationsToday = realTimeStats.TodayTotal,
                RegistrationsThisWeek = await GetWeeklyRegistrationsAsync(),
                RegistrationsThisMonth = realTimeStats.MonthTotal,
                SuccessRate = realTimeStats.SuccessRateToday,
                AvgResponseTimeMs = performanceMetrics.AvgResponseTimeMs,
                TotalUsers = await GetTotalUsersCountAsync(),
                ActiveSessions = await GetActiveSessionsCountAsync(),
                PendingRegistrations = realTimeStats.TodayTotal - realTimeStats.TodaySuccess - realTimeStats.TodayFailed,
                SystemLoad = await GetSystemLoadAsync(),
                LastUpdated = DateTime.UtcNow
            };

            // Cache with short expiry for real-time data
            await _cacheService.SetAsync(liveStats, LIVE_STATS_CACHE_KEY, LiveStatsCacheExpiry);

            stopwatch.Stop();
            _logger.LogDebug("Live stats retrieved and cached in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > 300)
            {
                _logger.LogWarning("Live stats query exceeded 300ms threshold: {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }

            return liveStats;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error retrieving live dashboard stats in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            // Return default stats to prevent dashboard failure
            return new DashboardRealTimeStats
            {
                LastUpdated = DateTime.UtcNow,
                AvgResponseTimeMs = 0
            };
        }
    }

    /// <summary>
    /// Get recent activities for dashboard feed with caching
    /// </summary>
    public async Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10)
    {
        try
        {
            var cacheKey = $"{RECENT_ACTIVITIES_CACHE_KEY}:{count}";
            var cachedActivities = await _cacheService.GetAsync<List<RecentActivityDto>>(cacheKey);
            
            if (cachedActivities != null)
            {
                return cachedActivities;
            }

            // Use optimized view for recent activities
            var recentActivities = await _dashboardService.GetRecentActivityAsync(count);

            var activities = recentActivities.Select(activity => new RecentActivityDto
            {
                Id = activity.Id,
                Type = DetermineActivityType(activity),
                Description = GenerateActivityDescription(activity),
                Status = activity.Status ?? activity.OtacState,
                Timestamp = activity.UpdatedAt ?? activity.CreatedAt,
                UserName = activity.CreatedBy,
                ExternalReference = activity.ExternalReference,
                Metadata = new Dictionary<string, object>
                {
                    ["otacCode"] = MaskOtacCode(activity.OtacCode),
                    ["branchName"] = activity.BranchName ?? "Unknown",
                    ["otacState"] = activity.OtacState
                }
            }).ToList();

            await _cacheService.SetAsync(activities, cacheKey, DefaultCacheExpiry);

            return activities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activities for count {Count}", count);
            return new List<RecentActivityDto>();
        }
    }

    /// <summary>
    /// Get KPI summary for specified time period
    /// </summary>
    public async Task<KpiSummary> GetKpiSummaryAsync(string period)
    {
        try
        {
            var cacheKey = $"{KPI_SUMMARY_CACHE_KEY}:{period.ToLower()}";
            var cachedKpi = await _cacheService.GetAsync<KpiSummary>(cacheKey);
            
            if (cachedKpi != null)
            {
                return cachedKpi;
            }

            var kpiSummary = await GenerateKpiSummaryForPeriodAsync(period);
            
            // Cache KPI data longer as it changes less frequently
            var cacheExpiry = period.ToLower() == "today" ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(1);
            await _cacheService.SetAsync(kpiSummary, cacheKey, cacheExpiry);

            return kpiSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating KPI summary for period {Period}", period);
            return new KpiSummary { Period = period };
        }
    }

    /// <summary>
    /// Get chart data for analytics visualization
    /// </summary>
    public async Task<ChartData> GetChartDataAsync(string chartType, string timeRange)
    {
        try
        {
            var cacheKey = $"dashboard:chart_data:{chartType}:{timeRange}";
            var cachedChart = await _cacheService.GetAsync<ChartData>(cacheKey);
            
            if (cachedChart != null)
            {
                return cachedChart;
            }

            var chartData = await GenerateChartDataAsync(chartType, timeRange);
            
            // Cache chart data based on time range sensitivity
            var cacheExpiry = timeRange == "24h" ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(2);
            await _cacheService.SetAsync(chartData, cacheKey, cacheExpiry);

            return chartData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chart data for type {ChartType} and range {TimeRange}", chartType, timeRange);
            return new ChartData 
            { 
                ChartType = chartType, 
                TimeRange = timeRange,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Get trend analysis data for performance monitoring
    /// </summary>
    public async Task<TrendAnalysis> GetTrendAnalysisAsync(string metricType, TimeSpan timeWindow)
    {
        try
        {
            var cacheKey = $"dashboard:trend_analysis:{metricType}:{timeWindow.TotalHours}h";
            var cachedTrend = await _cacheService.GetAsync<TrendAnalysis>(cacheKey);
            
            if (cachedTrend != null)
            {
                return cachedTrend;
            }

            var trendAnalysis = await GenerateTrendAnalysisAsync(metricType, timeWindow);
            
            await _cacheService.SetAsync(trendAnalysis, cacheKey, TimeSpan.FromMinutes(15));

            return trendAnalysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trend analysis for metric {MetricType} and window {TimeWindow}", metricType, timeWindow);
            return new TrendAnalysis 
            { 
                MetricType = metricType,
                TimeWindow = timeWindow,
                TrendDirection = "Unknown",
                AnalyzedAt = DateTime.UtcNow
            };
        }
    }

    #region Private Helper Methods

    private async Task<PerformanceSnapshot> GetCurrentPerformanceSnapshotAsync()
    {
        // Implementation would integrate with performance monitoring
        // For now, return simulated performance data
        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ResponseTimeMs = Random.Shared.Next(50, 300),
            RequestCount = Random.Shared.Next(10, 100),
            ErrorCount = Random.Shared.Next(0, 5),
            CpuUsage = Random.Shared.Next(10, 80),
            MemoryUsage = Random.Shared.Next(30, 90)
        };
    }

    private async Task<int> GetWeeklyRegistrationsAsync()
    {
        // Use repository to get weekly count
        var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        // Implementation would query database for weekly registrations
        return Random.Shared.Next(50, 200); // Placeholder
    }

    private async Task<int> GetTotalUsersCountAsync()
    {
        // Implementation would query user repository
        return Random.Shared.Next(100, 1000); // Placeholder
    }

    private async Task<int> GetActiveSessionsCountAsync()
    {
        // Implementation would query session store
        return Random.Shared.Next(5, 50); // Placeholder
    }

    private async Task<decimal> GetSystemLoadAsync()
    {
        // Implementation would check system resources
        return Random.Shared.Next(10, 95); // Placeholder percentage
    }

    private string DetermineActivityType(RecentActivityItem activity)
    {
        return activity.OtacState switch
        {
            "Generated" => "OTAC",
            "Validated" => "OTAC",
            "Used" => "Registration",
            _ => activity.Status switch
            {
                "Success" => "Registration",
                "Fail" => "Registration",
                "Pending" => "Registration",
                _ => "System"
            }
        };
    }

    private string GenerateActivityDescription(RecentActivityItem activity)
    {
        return activity.OtacState switch
        {
            "Generated" => $"OTAC {MaskOtacCode(activity.OtacCode)} generated",
            "Validated" => $"OTAC {MaskOtacCode(activity.OtacCode)} validated",
            "Used" => $"Registration completed for OTAC {MaskOtacCode(activity.OtacCode)}",
            _ => activity.Status switch
            {
                "Success" => $"Registration {activity.ExternalReference ?? "N/A"} completed successfully",
                "Fail" => $"Registration {activity.ExternalReference ?? "N/A"} failed",
                "Pending" => $"Registration {activity.ExternalReference ?? "N/A"} in progress",
                _ => $"System activity for {MaskOtacCode(activity.OtacCode)}"
            }
        };
    }

    private string MaskOtacCode(string otacCode)
    {
        if (string.IsNullOrEmpty(otacCode) || otacCode.Length < 4)
            return "****";
            
        return $"{otacCode[..2]}****{otacCode[^2..]}";
    }

    private async Task<KpiSummary> GenerateKpiSummaryForPeriodAsync(string period)
    {
        // Implementation would calculate KPIs based on period
        var (startDate, endDate) = GetDateRangeForPeriod(period);
        
        // Placeholder implementation
        var totalRegistrations = Random.Shared.Next(10, 500);
        var successfulRegistrations = Random.Shared.Next(8, totalRegistrations);
        
        return new KpiSummary
        {
            Period = period,
            TotalRegistrations = totalRegistrations,
            SuccessfulRegistrations = successfulRegistrations,
            FailedRegistrations = totalRegistrations - successfulRegistrations,
            SuccessRate = totalRegistrations > 0 ? Math.Round((decimal)successfulRegistrations / totalRegistrations * 100, 2) : 0,
            OtacGenerated = Random.Shared.Next(totalRegistrations, totalRegistrations * 2),
            OtacUsed = totalRegistrations,
            OtacUsageRate = 75.5m,
            AvgProcessingTimeMs = Random.Shared.Next(1000, 5000),
            UserSatisfactionScore = Random.Shared.Next(80, 98),
            ComparisonToPreviousPeriod = Random.Shared.Next(-15, 25),
            Trends = GenerateTrendDataPoints(startDate, endDate)
        };
    }

    private async Task<ChartData> GenerateChartDataAsync(string chartType, string timeRange)
    {
        var (startDate, endDate) = GetDateRangeForTimeRange(timeRange);
        var dataPoints = new List<ChartDataPoint>();
        
        // Generate sample data points based on chart type
        var interval = GetIntervalForTimeRange(timeRange);
        var current = startDate;
        
        while (current <= endDate)
        {
            var value = GenerateValueForChartType(chartType, current);
            dataPoints.Add(new ChartDataPoint
            {
                Timestamp = current,
                Value = value,
                Label = current.ToString("HH:mm")
            });
            
            current = current.Add(interval);
        }

        return new ChartData
        {
            ChartType = chartType,
            TimeRange = timeRange,
            DataPoints = dataPoints,
            Metadata = GenerateChartMetadata(chartType),
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<TrendAnalysis> GenerateTrendAnalysisAsync(string metricType, TimeSpan timeWindow)
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.Subtract(timeWindow);
        var midPoint = startDate.Add(TimeSpan.FromTicks(timeWindow.Ticks / 2));

        var currentValue = GenerateValueForMetricType(metricType, endDate);
        var previousValue = GenerateValueForMetricType(metricType, midPoint);
        var changePercent = previousValue != 0 ? ((currentValue - previousValue) / previousValue) * 100 : 0;

        return new TrendAnalysis
        {
            MetricType = metricType,
            TimeWindow = timeWindow,
            CurrentValue = currentValue,
            PreviousValue = previousValue,
            ChangePercent = Math.Round(changePercent, 2),
            TrendDirection = DetermineTrendDirection(changePercent),
            DataPoints = GenerateTrendDataPoints(startDate, endDate),
            Recommendation = GenerateRecommendation(metricType, changePercent),
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private (DateTime startDate, DateTime endDate) GetDateRangeForPeriod(string period)
    {
        var now = DateTime.UtcNow;
        return period.ToLower() switch
        {
            "today" => (now.Date, now.Date.AddDays(1).AddSeconds(-1)),
            "week" => (now.Date.AddDays(-(int)now.DayOfWeek), now),
            "month" => (new DateTime(now.Year, now.Month, 1), now),
            _ => (now.Date, now)
        };
    }

    private (DateTime startDate, DateTime endDate) GetDateRangeForTimeRange(string timeRange)
    {
        var now = DateTime.UtcNow;
        return timeRange.ToLower() switch
        {
            "24h" => (now.AddHours(-24), now),
            "7d" => (now.AddDays(-7), now),
            "30d" => (now.AddDays(-30), now),
            _ => (now.AddHours(-24), now)
        };
    }

    private TimeSpan GetIntervalForTimeRange(string timeRange)
    {
        return timeRange.ToLower() switch
        {
            "24h" => TimeSpan.FromHours(1),
            "7d" => TimeSpan.FromHours(6),
            "30d" => TimeSpan.FromDays(1),
            _ => TimeSpan.FromHours(1)
        };
    }

    private decimal GenerateValueForChartType(string chartType, DateTime timestamp)
    {
        // Generate realistic sample data based on chart type
        return chartType.ToLower() switch
        {
            "registrations" => Random.Shared.Next(0, 20),
            "success-rate" => Random.Shared.Next(85, 98),
            "otac-usage" => Random.Shared.Next(70, 90),
            "response-times" => Random.Shared.Next(50, 300),
            _ => Random.Shared.Next(0, 100)
        };
    }

    private decimal GenerateValueForMetricType(string metricType, DateTime timestamp)
    {
        return metricType.ToLower() switch
        {
            "response-time" => Random.Shared.Next(100, 500),
            "success-rate" => Random.Shared.Next(85, 98),
            "throughput" => Random.Shared.Next(50, 200),
            "error-rate" => Random.Shared.Next(1, 5),
            _ => Random.Shared.Next(0, 100)
        };
    }

    private string DetermineTrendDirection(decimal changePercent)
    {
        return changePercent switch
        {
            > 5 => "Improving",
            < -5 => "Declining",
            _ => "Stable"
        };
    }

    private List<TrendDataPoint> GenerateTrendDataPoints(DateTime startDate, DateTime endDate)
    {
        var points = new List<TrendDataPoint>();
        var current = startDate;
        var interval = TimeSpan.FromHours((endDate - startDate).TotalHours / 10);

        while (current <= endDate && points.Count < 10)
        {
            points.Add(new TrendDataPoint
            {
                Timestamp = current,
                Value = Random.Shared.Next(0, 100),
                MetricName = "trend_value"
            });
            current = current.Add(interval);
        }

        return points;
    }

    private string GenerateRecommendation(string metricType, decimal changePercent)
    {
        return metricType.ToLower() switch
        {
            "response-time" when changePercent > 10 => "Consider optimizing database queries or scaling infrastructure",
            "success-rate" when changePercent < -5 => "Investigate recent failures and improve error handling",
            "error-rate" when changePercent > 10 => "Review error logs and implement additional monitoring",
            _ => changePercent > 5 ? "Performance is improving - maintain current practices" : "Monitor closely for any changes"
        };
    }

    private ChartMetadata GenerateChartMetadata(string chartType)
    {
        return chartType.ToLower() switch
        {
            "registrations" => new ChartMetadata
            {
                Title = "Registration Volume",
                XAxisLabel = "Time",
                YAxisLabel = "Count",
                Unit = "registrations",
                Colors = new List<string> { "#007bff", "#28a745", "#dc3545" }
            },
            "success-rate" => new ChartMetadata
            {
                Title = "Success Rate",
                XAxisLabel = "Time",
                YAxisLabel = "Percentage",
                Unit = "%",
                MaxValue = 100,
                Colors = new List<string> { "#28a745" }
            },
            "response-times" => new ChartMetadata
            {
                Title = "Response Times",
                XAxisLabel = "Time",
                YAxisLabel = "Response Time",
                Unit = "ms",
                Colors = new List<string> { "#ffc107" }
            },
            _ => new ChartMetadata
            {
                Title = chartType.Replace("-", " ").ToTitleCase(),
                XAxisLabel = "Time",
                YAxisLabel = "Value",
                Colors = new List<string> { "#007bff" }
            }
        };
    }

    #endregion
}

/// <summary>
/// Extension method for string title case conversion
/// </summary>
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}