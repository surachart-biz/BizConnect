using BizConnect.Dal.Models;
using BizConnect.Services.DTOs;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BizConnect.Areas.Admin.Controllers;

public class HomeController : BaseAdminController
{
    private readonly IDashboardService _dashboardService;

    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var language = GetCurrentLanguage();
        var statistics = await _dashboardService.GetDashboardStatisticsAsync(language);
        
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

    public async Task<IActionResult> Index()
    {
        var quickActions = GetQuickActions();
        var widgets = GetDashboardWidgets();
        var permissions = GetUserPermissions();
        
        ViewBag.QuickActions = quickActions;
        ViewBag.Widgets = widgets;
        ViewBag.UserPermissions = permissions;
        
        return View();
    }

    public IActionResult LoadingDemo()
    {
        return View();
    }

    public IActionResult InteractionsDemo()
    {
        return View();
    }

    #region Private Helper Methods for Modern Admin Dashboard

    /// <summary>
    /// Get quick actions for admin dashboard
    /// </summary>
    private List<QuickAction> GetQuickActions()
    {
        var actions = new List<QuickAction>
        {
            new QuickAction
            {
                Title = "Generate OTAC",
                Description = "Generate new OTAC codes",
                ActionUrl = Url.Action("Index", "Otac"),
                IconClass = "fas fa-key",
                Color = "primary",
                Permission = "ManageOtac",
                DisplayOrder = 1
            },
            new QuickAction
            {
                Title = "View Registrations",
                Description = "Manage ODD registrations",
                ActionUrl = Url.Action("Index", "OddRegistration"),
                IconClass = "fas fa-list-alt",
                Color = "info",
                Permission = "ViewRegistrations",
                DisplayOrder = 2
            },
            new QuickAction
            {
                Title = "System Health",
                Description = "Monitor system status",
                ActionUrl = "#",
                IconClass = "fas fa-heartbeat",
                Color = "success",
                Permission = "ViewSystemHealth",
                DisplayOrder = 3
            },
            new QuickAction
            {
                Title = "Analytics",
                Description = "View detailed analytics",
                ActionUrl = Url.Action("Index", "Analytics"),
                IconClass = "fas fa-chart-bar",
                Color = "warning",
                Permission = "ViewAnalytics",
                DisplayOrder = 4
            }
        };

        return actions.Where(a => HasPermission(a.Permission)).ToList();
    }

    /// <summary>
    /// Get dashboard widgets configuration
    /// </summary>
    private List<DashboardWidget> GetDashboardWidgets()
    {
        return new List<DashboardWidget>
        {
            new DashboardWidget
            {
                Id = "live-stats",
                Title = "Live Statistics",
                Type = "metric",
                Size = "large",
                Position = 1,
                Configuration = new Dictionary<string, object>
                {
                    ["refreshInterval"] = 30,
                    ["showTrends"] = true
                }
            },
            new DashboardWidget
            {
                Id = "recent-activity",
                Title = "Recent Activity",
                Type = "table",
                Size = "medium",
                Position = 2,
                Configuration = new Dictionary<string, object>
                {
                    ["maxRows"] = 10,
                    ["autoRefresh"] = true
                }
            },
            new DashboardWidget
            {
                Id = "performance-chart",
                Title = "Performance Metrics",
                Type = "chart",
                Size = "medium",
                Position = 3,
                Configuration = new Dictionary<string, object>
                {
                    ["chartType"] = "line",
                    ["timeRange"] = "24h"
                }
            },
            new DashboardWidget
            {
                Id = "system-alerts",
                Title = "System Alerts",
                Type = "custom",
                Size = "small",
                Position = 4,
                Configuration = new Dictionary<string, object>
                {
                    ["showOnlyActive"] = true,
                    ["maxAlerts"] = 5
                }
            }
        };
    }

    /// <summary>
    /// Get user permissions for UI customization
    /// </summary>
    private UserPermissions GetUserPermissions()
    {
        // Get current user roles and permissions
        var userRoles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var isAdmin = userRoles.Contains("Admin");
        var isEmployee = userRoles.Contains("Employee");

        return new UserPermissions
        {
            CanViewAnalytics = isAdmin || isEmployee,
            CanManageUsers = isAdmin,
            CanManageOtac = isAdmin || isEmployee,
            CanManageRegistrations = isAdmin || isEmployee,
            CanExportData = isAdmin,
            CanViewSystemHealth = isAdmin,
            CanManageSystem = isAdmin,
            Roles = userRoles,
            FeatureFlags = new Dictionary<string, bool>
            {
                ["modernUI"] = true,
                ["realtimeUpdates"] = isAdmin || isEmployee,
                ["advancedAnalytics"] = isAdmin,
                ["systemMonitoring"] = isAdmin,
                ["exportFeatures"] = isAdmin
            }
        };
    }

    /// <summary>
    /// Check if current user has specified permission
    /// </summary>
    private bool HasPermission(string? permission)
    {
        if (string.IsNullOrEmpty(permission))
            return true;

        var permissions = GetUserPermissions();
        
        return permission switch
        {
            "ManageOtac" => permissions.CanManageOtac,
            "ViewRegistrations" => permissions.CanManageRegistrations,
            "ViewSystemHealth" => permissions.CanViewSystemHealth,
            "ViewAnalytics" => permissions.CanViewAnalytics,
            "ManageUsers" => permissions.CanManageUsers,
            "ExportData" => permissions.CanExportData,
            _ => false
        };
    }

    #endregion
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