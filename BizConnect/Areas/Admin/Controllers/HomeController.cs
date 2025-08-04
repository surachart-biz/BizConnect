using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOrEmployee")]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;

    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var statistics = await _dashboardService.GetDashboardStatisticsAsync();
        
        var model = new DashboardViewModel
        {
            // User Statistics
            TotalUsers = statistics.TotalUsers,
            ActiveUsers = statistics.ActiveUsers,
            AdminUsers = statistics.AdminUsers,
            EmployeeUsers = statistics.EmployeeUsers,
            RegularUsers = statistics.RegularUsers,
            
            // KBank ODD Statistics
            TotalOddRegistrations = statistics.TotalOddRegistrations,
            PendingOddRegistrations = statistics.PendingOddRegistrations,
            CompletedOddRegistrations = statistics.CompletedOddRegistrations,
            FailedOddRegistrations = statistics.FailedOddRegistrations,
            TodayOddRegistrations = statistics.TodayOddRegistrations,
            ThisMonthOddRegistrations = statistics.ThisMonthOddRegistrations,
            
            // OTAC Statistics
            TotalOtacCodes = statistics.TotalOtacCodes,
            ActiveOtacCodes = statistics.ActiveOtacCodes,
            ExpiredOtacCodes = statistics.ExpiredOtacCodes,
            UsedOtacCodes = statistics.UsedOtacCodes,
            TodayOtacGenerated = statistics.TodayOtacGenerated,
            
            // Recent Activity
            RecentRegistrations = statistics.RecentRegistrations,
            
            // Calculated values from service (no business logic in presentation layer)
            SuccessRate = statistics.SuccessRate,
            OtacUsageRate = statistics.OtacUsageRate
        };

        return View(model);
    }

    public IActionResult LoadingDemo()
    {
        return View();
    }

    public IActionResult InteractionsDemo()
    {
        return View();
    }
}

public class DashboardViewModel
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
    
    // Calculated properties - moved from business logic, now just data transfer
    public double SuccessRate { get; set; }
    public double OtacUsageRate { get; set; }
}
