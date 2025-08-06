using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for dashboard data operations with modern UI optimizations
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets comprehensive dashboard statistics including user, ODD registration, and OTAC metrics
    /// </summary>
    /// <param name="language">Language code for localization (optional, defaults to "en")</param>
    /// <returns>Dashboard statistics model</returns>
    Task<DashboardStatistics> GetDashboardStatisticsAsync(string language = "en");

    /// <summary>
    /// Gets real-time dashboard statistics using optimized database views
    /// </summary>
    /// <returns>Real-time dashboard metrics</returns>
    Task<RealTimeDashboardStats> GetRealTimeStatsAsync();

    /// <summary>
    /// Gets recent activity feed using optimized view
    /// </summary>
    /// <param name="limit">Maximum number of recent activities to return (default 20)</param>
    /// <returns>List of recent activities</returns>
    Task<List<RecentActivityItem>> GetRecentActivityAsync(int limit = 20);

    /// <summary>
    /// Gets simple performance metrics using database function
    /// </summary>
    /// <returns>Simple performance metrics</returns>
    Task<SimplePerformanceMetrics> GetSimpleMetricsAsync();
}

/// <summary>
/// Model containing comprehensive dashboard statistics
/// </summary>
public class DashboardStatistics
{
    // User Statistics
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int AdminUsers { get; set; }
    public int EmployeeUsers { get; set; }
    public int RegularUsers { get; set; }
    
    // KBank ODD Registration Statistics
    public int TotalOddRegistrations { get; set; }
    public int PendingOddRegistrations { get; set; }
    public int CompletedOddRegistrations { get; set; }
    public int FailedOddRegistrations { get; set; }
    public int TodayOddRegistrations { get; set; }
    public int ThisMonthOddRegistrations { get; set; }
    
    // OTAC Code Statistics
    public int TotalOtacCodes { get; set; }
    public int ActiveOtacCodes { get; set; }
    public int ExpiredOtacCodes { get; set; }
    public int UsedOtacCodes { get; set; }
    public int TodayOtacGenerated { get; set; }
    
    // Recent Activity
    public List<KbankOddRegistration> RecentRegistrations { get; set; } = new List<KbankOddRegistration>();
    
    // Calculated Properties
    public double SuccessRate => TotalOddRegistrations > 0 
        ? Math.Round((double)CompletedOddRegistrations / TotalOddRegistrations * 100, 1) 
        : 0;
        
    public double OtacUsageRate => TotalOtacCodes > 0 
        ? Math.Round((double)UsedOtacCodes / TotalOtacCodes * 100, 1) 
        : 0;
}

/// <summary>
/// Real-time dashboard statistics from optimized database views
/// </summary>
public class RealTimeDashboardStats
{
    public int TodayTotal { get; set; }
    public int TodaySuccess { get; set; }
    public int TodayFailed { get; set; }
    public int MonthTotal { get; set; }
    public int MonthSuccess { get; set; }
    public int OtacGenerated { get; set; }
    public int OtacValidated { get; set; }
    public int OtacUsed { get; set; }
    public int ActiveOtac { get; set; }
    
    // Calculated properties
    public decimal SuccessRateToday => TodayTotal > 0 
        ? Math.Round((decimal)TodaySuccess / TodayTotal * 100, 2) 
        : 0;
    
    public decimal SuccessRateMonth => MonthTotal > 0 
        ? Math.Round((decimal)MonthSuccess / MonthTotal * 100, 2) 
        : 0;
}

/// <summary>
/// Recent activity item from optimized view
/// </summary>
public class RecentActivityItem
{
    public int Id { get; set; }
    public string? ExternalReference { get; set; }
    public string OtacCode { get; set; } = null!;
    public string OtacState { get; set; } = null!;
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? BranchName { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Simple performance metrics from database function
/// </summary>
public class SimplePerformanceMetrics
{
    public int TotalToday { get; set; }
    public int SuccessToday { get; set; }
    public decimal SuccessRate { get; set; }
    public int ActiveOtac { get; set; }
}