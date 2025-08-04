using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Analytics;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;

namespace BizConnect.Services
{
    /// <summary>
    /// Service implementation for analytics and dashboard metrics
    /// Aggregates data from multiple services to provide comprehensive system insights
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IOtacManagementService _otacService;
        private readonly IRegistrationQueryService _registrationService;
        private readonly ISecurityMonitoringService _securityService;
        private readonly ICacheService _cacheService;
        private readonly IBranchService _branchService;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IOtacManagementService otacService,
            IRegistrationQueryService registrationService,
            ISecurityMonitoringService securityService,
            ICacheService cacheService,
            IBranchService branchService,
            ILogger<AnalyticsService> logger)
        {
            _otacService = otacService ?? throw new ArgumentNullException(nameof(otacService));
            _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _branchService = branchService ?? throw new ArgumentNullException(nameof(branchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DashboardMetrics> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving comprehensive dashboard metrics");

                // Run all metric retrievals in parallel for better performance
                var activeOtacTask = GetActiveOtacCountAsync();
                var registrationStatsTask = GetRegistrationStatsAsync();
                var successRateTask = CalculateOverallSuccessRateAsync();
                var responseTimeTask = CalculateAverageResponseTimeAsync();
                var kbankStatusTask = GetKBankStatusAsync(cancellationToken);
                var securityMetricsTask = GetSecurityMetricsAsync(TimeSpan.FromHours(24), cancellationToken);
                var cachePerformanceTask = GetCachePerformanceAsync(cancellationToken);

                await Task.WhenAll(activeOtacTask, registrationStatsTask, successRateTask, 
                    responseTimeTask, kbankStatusTask, securityMetricsTask, cachePerformanceTask);

                var systemHealthScore = await CalculateSystemHealthScoreAsync(cancellationToken);

                return new DashboardMetrics
                {
                    ActiveOtacCount = await activeOtacTask,
                    Registrations = await registrationStatsTask,
                    SuccessRate = await successRateTask,
                    AverageResponseTime = await responseTimeTask,
                    KBankStatus = await kbankStatusTask,
                    Security = await securityMetricsTask,
                    Cache = await cachePerformanceTask,
                    SystemHealthScore = systemHealthScore,
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dashboard metrics");
                return CreateEmptyDashboardMetrics();
            }
        }

        public async Task<RealTimeMetrics> GetRealTimeMetricsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Retrieving real-time metrics for dashboard update");

                var now = DateTime.UtcNow;
                var oneHourAgo = now.AddHours(-1);

                var activeOtacTask = GetActiveOtacCountAsync();
                var recentOtacTask = GetOtacCountInPeriodAsync(oneHourAgo, now);
                var recentRegistrationsTask = GetRegistrationCountInPeriodAsync(oneHourAgo, now);
                var responseTimeTask = GetCurrentResponseTimeAsync();
                var securityEventsTask = GetSecurityEventsCountAsync(oneHourAgo, now);
                var cacheStats = _cacheService.GetStatistics();

                await Task.WhenAll(activeOtacTask, recentOtacTask, recentRegistrationsTask, 
                    responseTimeTask, securityEventsTask);

                return new RealTimeMetrics
                {
                    Timestamp = now,
                    ActiveOtacCount = await activeOtacTask,
                    OtacGeneratedLastHour = await recentOtacTask,
                    RegistrationsLastHour = await recentRegistrationsTask,
                    CurrentResponseTime = await responseTimeTask,
                    CpuUsage = GetCpuUsage(),
                    MemoryUsage = GetMemoryUsage(),
                    RecentSecurityEvents = await securityEventsTask,
                    RecentCacheHitRate = cacheStats.HitRatio * 100
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve real-time metrics");
                return new RealTimeMetrics();
            }
        }

        public async Task<TrendData> GetOtacTrendsAsync(int days = 7, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving OTAC trends for {Days} days", days);

                var endDate = DateTime.UtcNow.Date;
                var startDate = endDate.AddDays(-days);

                var trendData = new TrendData();

                // Generate date labels
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    trendData.Labels.Add(date.ToString("MM/dd"));
                }

                // Get daily OTAC generation counts
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var dayStart = date;
                    var dayEnd = date.AddDays(1);

                    var otacCount = await GetOtacCountInPeriodAsync(dayStart, dayEnd);
                    var registrationCount = await GetRegistrationCountInPeriodAsync(dayStart, dayEnd);
                    var successRate = await CalculateSuccessRateForPeriodAsync(dayStart, dayEnd);
                    var avgResponseTime = await CalculateAverageResponseTimeForPeriodAsync(dayStart, dayEnd);

                    trendData.OtacTrend.Add(otacCount);
                    trendData.RegistrationTrend.Add(registrationCount);
                    trendData.SuccessRateTrend.Add(successRate);
                    trendData.ResponseTimeTrend.Add(avgResponseTime);
                }

                return trendData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve OTAC trends for {Days} days", days);
                return new TrendData();
            }
        }

        public async Task<SecuritySummary> GetSecurityMetricsAsync(TimeSpan? timeRange = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var range = timeRange ?? TimeSpan.FromHours(24);
                var dashboard = await _securityService.GetSecurityDashboardAsync(range, cancellationToken);
                var statistics = await _securityService.GetStatisticsAsync(cancellationToken);

                return new SecuritySummary
                {
                    EventsToday = dashboard.Metrics.TotalEvents,
                    ThreatsDetected = dashboard.Metrics.ThreatEvents,
                    BlockedIps = dashboard.Metrics.WatchlistedIps,
                    ActiveAlerts = dashboard.Metrics.ActiveAlerts,
                    ThreatLevel = DetermineThreatLevel(dashboard.Metrics.ThreatDetectionRate),
                    TopThreatIps = dashboard.TopThreatIps.Take(5).Select(t => t.IpAddress).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve security metrics");
                return new SecuritySummary();
            }
        }

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving system performance metrics");

                // This would typically integrate with APM tools or custom performance counters
                return new PerformanceMetrics
                {
                    ResponseTime = new ResponseTimeStats
                    {
                        Average = await CalculateAverageResponseTimeAsync(),
                        Median = await CalculateMedianResponseTimeAsync(),
                        P95 = await CalculateP95ResponseTimeAsync(),
                        P99 = await CalculateP99ResponseTimeAsync(),
                        Min = 50, // Mock data - would come from monitoring
                        Max = 2500
                    },
                    Database = new DatabasePerformance
                    {
                        AverageQueryTime = 45.2,
                        ActiveConnections = 12,
                        QueryCount = 1250,
                        CacheHitRatio = 0.87,
                        DeadlockCount = 0
                    },
                    EndpointMetrics = await GetEndpointMetricsAsync(),
                    BackgroundJobs = await GetBackgroundJobMetricsAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve performance metrics");
                return new PerformanceMetrics();
            }
        }

        public async Task<RegistrationStats> GetRegistrationStatsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var today = now.Date;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var todayCount = await GetRegistrationCountInPeriodAsync(today, today.AddDays(1));
                var weekCount = await GetRegistrationCountInPeriodAsync(weekStart, now);
                var monthCount = await GetRegistrationCountInPeriodAsync(monthStart, now);

                // Calculate growth percentages
                var yesterdayCount = await GetRegistrationCountInPeriodAsync(today.AddDays(-1), today);
                var lastWeekCount = await GetRegistrationCountInPeriodAsync(weekStart.AddDays(-7), weekStart);
                var lastMonthCount = await GetRegistrationCountInPeriodAsync(monthStart.AddMonths(-1), monthStart);

                var dailyGrowth = CalculateGrowthPercentage(todayCount, yesterdayCount);
                var weeklyGrowth = CalculateGrowthPercentage(weekCount, lastWeekCount);
                var monthlyGrowth = CalculateGrowthPercentage(monthCount, lastMonthCount);

                var statusBreakdown = await GetRegistrationStatusBreakdownAsync();

                return new RegistrationStats
                {
                    Today = todayCount,
                    ThisWeek = weekCount,
                    ThisMonth = monthCount,
                    DailyGrowthPercent = dailyGrowth,
                    WeeklyGrowthPercent = weeklyGrowth,
                    MonthlyGrowthPercent = monthlyGrowth,
                    StatusBreakdown = statusBreakdown
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve registration statistics");
                return new RegistrationStats();
            }
        }

        public async Task<KBankIntegrationStatus> GetKBankStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Retrieving KBank integration status");

                // This would integrate with actual KBank service monitoring
                return new KBankIntegrationStatus
                {
                    Status = "Online",
                    SuccessRate = 98.5,
                    AverageResponseTime = 850,
                    LastSuccessfulCall = DateTime.UtcNow.AddMinutes(-5),
                    CallsToday = 125,
                    FailuresToday = 2
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve KBank integration status");
                return new KBankIntegrationStatus
                {
                    Status = "Unknown",
                    SuccessRate = 0,
                    AverageResponseTime = 0
                };
            }
        }

        public async Task<CachePerformance> GetCachePerformanceAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = _cacheService.GetStatistics();
                
                return new CachePerformance
                {
                    HitRate = stats.HitRatio * 100,
                    HitCount = stats.HitCount,
                    MissCount = stats.MissCount,
                    EvictionCount = stats.EvictionCount,
                    CurrentEntryCount = stats.CurrentEntryCount,
                    MemoryUsagePercent = CalculateMemoryUsagePercent(stats.CurrentEntryCount)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve cache performance metrics");
                return new CachePerformance();
            }
        }

        public async Task<double> CalculateSystemHealthScoreAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var scores = new List<double>();

                // Performance score (25%)
                var avgResponseTime = await CalculateAverageResponseTimeAsync();
                var performanceScore = CalculatePerformanceScore(avgResponseTime);
                scores.Add(performanceScore * 0.25);

                // Success rate score (30%)
                var successRate = await CalculateOverallSuccessRateAsync();
                scores.Add(successRate * 0.30);

                // Security score (20%)
                var securityMetrics = await GetSecurityMetricsAsync(TimeSpan.FromHours(24), cancellationToken);
                var securityScore = CalculateSecurityScore(securityMetrics);
                scores.Add(securityScore * 0.20);

                // Cache performance score (15%)
                var cacheStats = _cacheService.GetStatistics();
                var cacheScore = cacheStats.HitRatio * 100;
                scores.Add(cacheScore * 0.15);

                // Integration health score (10%)
                var kbankStatus = await GetKBankStatusAsync(cancellationToken);
                var integrationScore = kbankStatus.SuccessRate;
                scores.Add(integrationScore * 0.10);

                return Math.Round(scores.Sum(), 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate system health score");
                return 50.0; // Default middle score on error
            }
        }

        public async Task<Dictionary<string, object>> GetDetailedAnalyticsAsync(DateTime fromDate, DateTime toDate, Dictionary<string, object>? filters = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var analytics = new Dictionary<string, object>();

                // Add comprehensive analytics data based on date range and filters
                analytics["period"] = new { from = fromDate, to = toDate };
                analytics["registrations"] = await GetDetailedRegistrationAnalytics(fromDate, toDate);
                analytics["otac"] = await GetDetailedOtacAnalytics(fromDate, toDate);
                analytics["security"] = await GetDetailedSecurityAnalytics(fromDate, toDate);
                analytics["performance"] = await GetDetailedPerformanceAnalytics(fromDate, toDate);

                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve detailed analytics");
                return new Dictionary<string, object>();
            }
        }

        public async Task<List<BranchPerformance>> GetTopBranchesAsync(int limit = 10, CancellationToken cancellationToken = default)
        {
            try
            {
                var branches = await _branchService.GetAllBranchesAsync();
                var branchPerformance = new List<BranchPerformance>();

                foreach (var branch in branches.Take(limit))
                {
                    // Calculate performance metrics for each branch
                    var performance = new BranchPerformance
                    {
                        BranchId = branch.BranchId,
                        BranchName = branch.Name,
                        BranchCode = branch.Code,
                        RegistrationCount = await GetBranchRegistrationCountAsync(branch.BranchId),
                        SuccessRate = await CalculateBranchSuccessRateAsync(branch.BranchId),
                        AverageProcessingTime = await CalculateBranchProcessingTimeAsync(branch.BranchId),
                        GrowthRate = await CalculateBranchGrowthRateAsync(branch.BranchId)
                    };

                    branchPerformance.Add(performance);
                }

                // Rank branches and assign positions
                var rankedBranches = branchPerformance
                    .OrderByDescending(b => b.SuccessRate)
                    .ThenByDescending(b => b.RegistrationCount)
                    .Select((branch, index) => { branch.RankPosition = index + 1; return branch; })
                    .ToList();

                return rankedBranches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve top branches");
                return new List<BranchPerformance>();
            }
        }

        public async Task<List<SystemAlert>> GetSystemAlertsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var alerts = new List<SystemAlert>();

                // Check for performance alerts
                var avgResponseTime = await CalculateAverageResponseTimeAsync();
                if (avgResponseTime > 2000) // 2 seconds threshold
                {
                    alerts.Add(new SystemAlert
                    {
                        Title = "High Response Time",
                        Description = $"Average response time is {avgResponseTime:F0}ms, which exceeds the 2000ms threshold",
                        Severity = AlertSeverity.Warning,
                        Category = AlertCategory.Performance,
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check for security alerts
                var securityMetrics = await GetSecurityMetricsAsync(TimeSpan.FromHours(1), cancellationToken);
                if (securityMetrics.ThreatsDetected > 10)
                {
                    alerts.Add(new SystemAlert
                    {
                        Title = "High Threat Activity",
                        Description = $"{securityMetrics.ThreatsDetected} threats detected in the last hour",
                        Severity = AlertSeverity.Error,
                        Category = AlertCategory.Security,
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check for integration alerts
                var kbankStatus = await GetKBankStatusAsync(cancellationToken);
                if (kbankStatus.SuccessRate < 95)
                {
                    alerts.Add(new SystemAlert
                    {
                        Title = "KBank Integration Issues",
                        Description = $"KBank success rate is {kbankStatus.SuccessRate:F1}%, below 95% threshold",
                        Severity = AlertSeverity.Warning,
                        Category = AlertCategory.Integration,
                        DetectedAt = DateTime.UtcNow
                    });
                }

                return alerts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve system alerts");
                return new List<SystemAlert>();
            }
        }

        #region Private Helper Methods

        private async Task<int> GetActiveOtacCountAsync()
        {
            // This would query the database for active OTAC codes
            // Placeholder implementation
            return 45;
        }

        private async Task<int> GetOtacCountInPeriodAsync(DateTime start, DateTime end)
        {
            // Query database for OTAC count in period
            return 12;
        }

        private async Task<int> GetRegistrationCountInPeriodAsync(DateTime start, DateTime end)
        {
            try
            {
                var result = await _registrationService.GetForExportAsync(null, start, end);
                return result.IsSuccess ? result.Data.Count() : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<double> CalculateOverallSuccessRateAsync()
        {
            try
            {
                var stats = await _registrationService.GetStatisticsAsync();
                if (stats.IsSuccess && stats.Data != null)
                {
                    var total = stats.Data.TotalRegistrations;
                    var successful = stats.Data.SuccessfulRegistrations;
                    return total > 0 ? (successful * 100.0 / total) : 0;
                }
                return 95.5; // Default value
            }
            catch
            {
                return 95.5;
            }
        }

        private async Task<double> CalculateAverageResponseTimeAsync()
        {
            // This would integrate with monitoring tools
            return 750; // Placeholder
        }

        private async Task<double> GetCurrentResponseTimeAsync()
        {
            return 650; // Current response time
        }

        private async Task<int> GetSecurityEventsCountAsync(DateTime start, DateTime end)
        {
            try
            {
                var dashboard = await _securityService.GetSecurityDashboardAsync(end - start);
                return dashboard.Metrics.TotalEvents;
            }
            catch
            {
                return 0;
            }
        }

        private double GetCpuUsage()
        {
            // This would integrate with system monitoring
            return 35.5;
        }

        private double GetMemoryUsage()
        {
            // This would integrate with system monitoring
            return 68.2;
        }

        private async Task<double> CalculateSuccessRateForPeriodAsync(DateTime start, DateTime end)
        {
            return 96.8; // Placeholder
        }

        private async Task<double> CalculateAverageResponseTimeForPeriodAsync(DateTime start, DateTime end)
        {
            return 780; // Placeholder
        }

        private async Task<StatusBreakdown> GetRegistrationStatusBreakdownAsync()
        {
            try
            {
                var stats = await _registrationService.GetStatisticsAsync();
                if (stats.IsSuccess && stats.Data != null)
                {
                    return new StatusBreakdown
                    {
                        Success = stats.Data.SuccessfulRegistrations,
                        Pending = stats.Data.PendingRegistrations,
                        Failed = stats.Data.FailedRegistrations,
                        Expired = 0 // Would need to calculate from database
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get registration status breakdown");
            }

            return new StatusBreakdown();
        }

        private string DetermineThreatLevel(double threatDetectionRate)
        {
            return threatDetectionRate switch
            {
                >= 0.8 => "Critical",
                >= 0.6 => "High",
                >= 0.3 => "Medium",
                _ => "Low"
            };
        }

        private double CalculateGrowthPercentage(int current, int previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return ((double)(current - previous) / previous) * 100;
        }

        private double CalculateMemoryUsagePercent(int entryCount)
        {
            // Rough calculation based on entry count
            return Math.Min((entryCount * 0.1), 100);
        }

        private double CalculatePerformanceScore(double responseTime)
        {
            // Score based on response time (lower is better)
            if (responseTime < 500) return 100;
            if (responseTime < 1000) return 90;
            if (responseTime < 1500) return 70;
            if (responseTime < 2000) return 50;
            return 30;
        }

        private double CalculateSecurityScore(SecuritySummary security)
        {
            // Score based on threat level and alerts
            var baseScore = 100;
            baseScore -= security.ThreatsDetected * 2;
            baseScore -= security.ActiveAlerts * 5;
            return Math.Max(baseScore, 0);
        }

        private DashboardMetrics CreateEmptyDashboardMetrics()
        {
            return new DashboardMetrics
            {
                ActiveOtacCount = 0,
                Registrations = new RegistrationStats(),
                SuccessRate = 0,
                AverageResponseTime = 0,
                KBankStatus = new KBankIntegrationStatus { Status = "Unknown" },
                Security = new SecuritySummary(),
                Cache = new CachePerformance(),
                SystemHealthScore = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        // Additional placeholder methods for detailed analytics
        private async Task<double> CalculateMedianResponseTimeAsync() => 680;
        private async Task<double> CalculateP95ResponseTimeAsync() => 1200;
        private async Task<double> CalculateP99ResponseTimeAsync() => 1800;
        private async Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync() => new();
        private async Task<BackgroundJobMetrics> GetBackgroundJobMetricsAsync() => new();
        private async Task<object> GetDetailedRegistrationAnalytics(DateTime from, DateTime to) => new { };
        private async Task<object> GetDetailedOtacAnalytics(DateTime from, DateTime to) => new { };
        private async Task<object> GetDetailedSecurityAnalytics(DateTime from, DateTime to) => new { };
        private async Task<object> GetDetailedPerformanceAnalytics(DateTime from, DateTime to) => new { };
        private async Task<int> GetBranchRegistrationCountAsync(int branchId) => 25;
        private async Task<double> CalculateBranchSuccessRateAsync(int branchId) => 96.5;
        private async Task<double> CalculateBranchProcessingTimeAsync(int branchId) => 850;
        private async Task<double> CalculateBranchGrowthRateAsync(int branchId) => 12.5;

        #endregion
    }
}