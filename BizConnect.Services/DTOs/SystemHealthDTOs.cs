using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    public class SystemHealthStatus
    {
        public string OverallStatus { get; set; } = "Healthy";
        public int HealthScore { get; set; } = 100;
        public List<HealthCheck> Checks { get; set; } = new();
        public List<AlertMessage> Alerts { get; set; } = new();
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;
        
        // Additional properties expected by services
        public List<HealthCheck> HealthChecks { get; set; } = new();
        public bool DatabaseConnected { get; set; } = true;
        public bool KBankApiAvailable { get; set; } = true;
        public bool BackgroundJobsRunning { get; set; } = true;
        public int ResponseTimeMs { get; set; }
        public decimal CpuUsagePercent { get; set; }
        public decimal MemoryUsagePercent { get; set; }
        public decimal DiskUsagePercent { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class HealthCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Healthy";
        public string Message { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;
        
        // Additional properties expected by services
        public string? Description { get; set; }
        public int ResponseTimeMs { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    public class AlertMessage
    {
        public int Id { get; set; }
        public string Type { get; set; } = "Info";
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        
        // Additional properties expected by services
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; }
        public Dictionary<string, object>? Details { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}