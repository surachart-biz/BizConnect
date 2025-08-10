using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace BizConnect.Services;

/// <summary>
/// Service for dashboard data operations
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly BizConnectContext _context;
    private readonly IUserService _userService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        BizConnectContext context,
        IUserService userService,
        ICacheService cacheService,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _userService = userService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive dashboard statistics including user, ODD registration, and OTAC metrics
    /// </summary>
    /// <param name="language">Language code for localization (optional, defaults to "en")</param>
    /// <returns>Dashboard statistics model</returns>
    public async Task<DashboardStatistics> GetDashboardStatisticsAsync(string language = "en")
    {
        try
        {
            _logger.LogInformation("Fetching dashboard statistics for language {Language}", language);

            // Get user statistics from user service
            var users = await _userService.GetAllUsersAsync();
            var userList = users.ToList();

            // Get date ranges for filtering
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);

            // Get KBank ODD registrations (now includes OTAC data)
            var oddRegistrations = await _context.KbankOddRegistrations.ToListAsync();

            // Get recent registrations for activity feed
            var recentRegistrations = oddRegistrations
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToList();

            var statistics = new DashboardStatistics
            {
                // User Statistics
                TotalUsers = userList.Count,
                ActiveUsers = userList.Count(u => u.IsActive),
                AdminUsers = userList.Count(u => u.Role == "Admin"),
                EmployeeUsers = userList.Count(u => u.Role == "Employee"),
                RegularUsers = userList.Count(u => u.Role == "User"),
                
                // KBank ODD Statistics
                TotalOddRegistrations = oddRegistrations.Count,
                PendingOddRegistrations = oddRegistrations.Count(r => r.Status == "Pending"),
                CompletedOddRegistrations = oddRegistrations.Count(r => r.Status == "Success"),
                FailedOddRegistrations = oddRegistrations.Count(r => r.Status == "Fail"),
                TodayOddRegistrations = oddRegistrations.Count(r => r.CreatedAt >= today),
                ThisMonthOddRegistrations = oddRegistrations.Count(r => r.CreatedAt >= thisMonth),
                
                // OTAC Statistics (now part of KBank registrations)
                TotalOtacCodes = oddRegistrations.Count,
                ActiveOtacCodes = oddRegistrations.Count(r => 
                    r.OtacExpiresAt.HasValue && r.OtacExpiresAt > DateTime.Now && r.OtacState != "Used"),
                ExpiredOtacCodes = oddRegistrations.Count(r => 
                    r.OtacExpiresAt.HasValue && r.OtacExpiresAt <= DateTime.Now && r.OtacState != "Used"),
                UsedOtacCodes = oddRegistrations.Count(r => r.OtacState == "Used"),
                TodayOtacGenerated = oddRegistrations.Count(r => r.CreatedAt >= today),
                
                // Recent Activity
                RecentRegistrations = recentRegistrations
            };

            _logger.LogInformation("Successfully fetched dashboard statistics: {TotalUsers} users, {TotalOddRegistrations} ODD registrations, {TotalOtacCodes} OTAC codes",
                statistics.TotalUsers, statistics.TotalOddRegistrations, statistics.TotalOtacCodes);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch dashboard statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets real-time dashboard statistics using optimized database views with caching
    /// </summary>
    /// <returns>Real-time dashboard metrics</returns>
    public async Task<RealTimeDashboardStats> GetRealTimeStatsAsync()
    {
        const string cacheKey = "dashboard:realtime:stats";
        const int cacheMinutes = 2; // Short cache for real-time data

        return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
        {
            try
            {
                _logger.LogInformation("Fetching real-time dashboard statistics from optimized views");

                // Use cached database function for better performance
                var result = await _context.Database
                    .SqlQueryRaw<string>("SELECT get_cached_dashboard_stats()::text")
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(result))
                {
                    _logger.LogWarning("No dashboard statistics returned from cached function");
                    return new RealTimeDashboardStats();
                }

                // Parse JSON result from database function
                var statsJson = System.Text.Json.JsonDocument.Parse(result);
                var root = statsJson.RootElement;

                return new RealTimeDashboardStats
                {
                    TodayTotal = root.GetProperty("registrations_today").GetInt32(),
                    TodaySuccess = root.GetProperty("approved_registrations").GetInt32(),
                    TodayFailed = root.GetProperty("rejected_registrations").GetInt32(),
                    MonthTotal = root.GetProperty("registrations_month").GetInt32(),
                    MonthSuccess = root.GetProperty("approved_registrations").GetInt32(),
                    OtacGenerated = root.GetProperty("active_otac_codes").GetInt32(),
                    OtacValidated = root.GetProperty("validated_otac_codes").GetInt32(),
                    OtacUsed = root.GetProperty("used_otac_codes").GetInt32(),
                    ActiveOtac = root.GetProperty("active_otac_codes").GetInt32()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch real-time dashboard statistics");
                
                // Fallback to direct view query
                var stats = await _context.VRealtimeDashboardStats.FirstOrDefaultAsync();
                if (stats == null)
                {
                    return new RealTimeDashboardStats();
                }

                return new RealTimeDashboardStats
                {
                    TodayTotal = (int)(stats.RegistrationsToday ?? 0),
                    TodaySuccess = (int)(stats.ApprovedRegistrations ?? 0),
                    TodayFailed = (int)(stats.RejectedRegistrations ?? 0),
                    MonthTotal = (int)(stats.RegistrationsWeek ?? 0),
                    MonthSuccess = (int)(stats.ApprovedRegistrations ?? 0),
                    OtacGenerated = (int)(stats.ActiveOtacCodes ?? 0),
                    OtacValidated = (int)(stats.ValidatedOtacCodes ?? 0),
                    OtacUsed = (int)(stats.ValidatedOtacCodes ?? 0),
                    ActiveOtac = (int)(stats.ActiveOtacCodes ?? 0)
                };
            }
        }, TimeSpan.FromMinutes(cacheMinutes));
    }

    /// <summary>
    /// Gets recent activity feed using optimized view
    /// </summary>
    /// <param name="limit">Maximum number of recent activities to return (default 20)</param>
    /// <returns>List of recent activities</returns>
    public async Task<List<RecentActivityItem>> GetRecentActivityAsync(int limit = 20)
    {
        try
        {
            _logger.LogInformation("Fetching recent activity with limit {Limit}", limit);

            var activities = await _context.VRecentActivities
                .OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                .ThenByDescending(a => a.UpdatedAt ?? DateTime.MinValue)
                .Take(limit)
                .ToListAsync();

            return activities.Select(a => new RecentActivityItem
            {
                Id = a.Id ?? 0,
                ExternalReference = a.ExternalReference,
                OtacCode = string.Empty,
                OtacState = a.OtacState ?? string.Empty,
                Status = a.Status,
                CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                UpdatedAt = a.UpdatedAt,
                BranchName = a.BranchNameEn ?? string.Empty,
                CreatedBy = a.GeneratedByUsername ?? string.Empty
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent activity");
            throw;
        }
    }

    /// <summary>
    /// Gets simple performance metrics using database function
    /// </summary>
    /// <returns>Simple performance metrics</returns>
    public async Task<SimplePerformanceMetrics> GetSimpleMetricsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching simple performance metrics from database function");

            // Call the get_simple_metrics() PostgreSQL function
            var result = await _context.Database
                .SqlQueryRaw<SimplePerformanceMetricsResult>(
                    "SELECT total_today, success_today, success_rate, active_otac FROM get_simple_metrics()")
                .FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogWarning("No metrics returned from database function");
                return new SimplePerformanceMetrics();
            }

            return new SimplePerformanceMetrics
            {
                TotalToday = result.total_today,
                SuccessToday = result.success_today,
                SuccessRate = result.success_rate,
                ActiveOtac = result.active_otac
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch simple performance metrics");
            throw;
        }
    }
}

/// <summary>
/// Internal model for database function results
/// </summary>
internal class SimplePerformanceMetricsResult
{
    public int total_today { get; set; }
    public int success_today { get; set; }
    public decimal success_rate { get; set; }
    public int active_otac { get; set; }
}