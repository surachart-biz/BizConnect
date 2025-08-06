using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    public class QuickAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string Color { get; set; } = "primary";
        public string Permission { get; set; } = string.Empty;
        public string PermissionRequired { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "primary";
        public bool IsEnabled { get; set; } = true;
        public bool RequiresConfirmation { get; set; }
    }

    public class DashboardWidget
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = "medium";
        public int Position { get; set; }
        public bool IsVisible { get; set; } = true;
        public int RefreshInterval { get; set; } = 30;
        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, object> Data { get; set; } = new();
        public Dictionary<string, object> ChartConfig { get; set; } = new();
        public string Value { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string ColorClass { get; set; } = "primary";
        public string Trend { get; set; } = "stable";
        public double ChangePercentage { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class UserPermissions
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public bool CanViewAnalytics { get; set; }
        public bool CanManageUsers { get; set; }
        public bool CanManageOtac { get; set; }
        public bool CanManageRegistrations { get; set; }
        public bool CanExportData { get; set; }
        public bool CanViewSystemHealth { get; set; }
        public bool CanManageSystem { get; set; }
        public bool CanGenerateOtac { get; set; }
        public bool CanViewRegistrations { get; set; }
        public bool CanAccessAdmin { get; set; }
        public bool CanAccessSystem { get; set; }
        public bool CanViewReports { get; set; }
        public List<string> Roles { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public Dictionary<string, bool> FeatureFlags { get; set; } = new();
    }
}