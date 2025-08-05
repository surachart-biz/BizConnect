using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for dashboard data operations
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets comprehensive dashboard statistics including user, ODD registration, and OTAC metrics
    /// </summary>
    /// <param name="language">Language code for localization (optional, defaults to "en")</param>
    /// <returns>Dashboard statistics model</returns>
    Task<DashboardStatistics> GetDashboardStatisticsAsync(string language = "en");
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