using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    /// <returns>Dashboard statistics model</returns>
    public async Task<DashboardStatistics> GetDashboardStatisticsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching dashboard statistics");

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
}