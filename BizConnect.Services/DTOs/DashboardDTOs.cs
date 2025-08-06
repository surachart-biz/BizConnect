using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    public class DashboardRealTimeStats
    {
        public int ActiveOtacCodes { get; set; }
        public int PendingToday { get; set; }
        public int CompletedThisMonth { get; set; }
        public decimal SuccessRate { get; set; }
        public double AvgResponseTimeSeconds { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        // Additional properties expected by services
        public int RegistrationsToday { get; set; }
        public int RegistrationsThisWeek { get; set; }
        public int RegistrationsThisMonth { get; set; }
        public double AvgResponseTimeMs { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSessions { get; set; }
        public int PendingRegistrations { get; set; }
        public decimal SystemLoad { get; set; }
    }

    public class RecentActivityDto
    {
        public int Id { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserReference { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Additional properties expected by services
        public string Type { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ExternalReference { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class KpiSummary
    {
        public string Name { get; set; } = string.Empty;
        public object Value { get; set; } = 0;
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = "Normal";
        public double ChangePercentage { get; set; }
        public string Trend { get; set; } = "Stable";
        
        // Additional properties expected by service implementation
        public string Period { get; set; } = string.Empty;
        public int TotalRegistrations { get; set; }
        public int SuccessfulRegistrations { get; set; }
        public int FailedRegistrations { get; set; }
        public decimal SuccessRate { get; set; }
        public int OtacGenerated { get; set; }
        public int OtacUsed { get; set; }
        public decimal OtacUsageRate { get; set; }
        public int AvgProcessingTimeMs { get; set; }
        public int UserSatisfactionScore { get; set; }
        public decimal ComparisonToPreviousPeriod { get; set; }
        public List<TrendDataPoint> Trends { get; set; } = new();
    }
}