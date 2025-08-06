using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
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
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        BizConnectContext context,
        IUserService userService,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _userService = userService;
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
    /// Gets real-time dashboard statistics using optimized database views
    /// </summary>
    /// <returns>Real-time dashboard metrics</returns>
    public async Task<RealTimeDashboardStats> GetRealTimeStatsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching real-time dashboard statistics from optimized views");

            var stats = await _context.DashboardStats.FirstOrDefaultAsync();
            
            if (stats == null)
            {
                _logger.LogWarning("No dashboard statistics found in optimized view");
                return new RealTimeDashboardStats();
            }

            return new RealTimeDashboardStats
            {
                TodayTotal = (int)(stats.today_total ?? 0),
                TodaySuccess = (int)(stats.today_success ?? 0),
                TodayFailed = (int)(stats.today_failed ?? 0),
                MonthTotal = (int)(stats.month_total ?? 0),
                MonthSuccess = (int)(stats.month_success ?? 0),
                OtacGenerated = (int)(stats.otac_generated ?? 0),
                OtacValidated = (int)(stats.otac_validated ?? 0),
                OtacUsed = (int)(stats.otac_used ?? 0),
                ActiveOtac = (int)(stats.active_otac ?? 0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch real-time dashboard statistics");
            throw;
        }
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

            var activities = await _context.RecentActivities
                .Take(limit)
                .ToListAsync();

            return activities.Select(a => new RecentActivityItem
            {
                Id = a.Id ?? 0,
                ExternalReference = a.ExternalReference,
                OtacCode = a.OtacCode ?? string.Empty,
                OtacState = a.OtacState ?? string.Empty,
                Status = a.Status,
                CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                UpdatedAt = a.UpdatedAt,
                BranchName = a.BranchName,
                CreatedBy = a.CreatedBy
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