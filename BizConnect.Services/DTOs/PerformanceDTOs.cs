using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    /// <summary>
    /// Current performance metrics for monitoring
    /// </summary>
    public class PerformanceMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ActiveConnections { get; set; }
        public decimal CpuUsagePercent { get; set; }
        public decimal MemoryUsagePercent { get; set; }
        public int RequestsPerMinute { get; set; }
        public int ErrorsPerMinute { get; set; }
        public decimal AverageResponseTimeMs { get; set; }
        public decimal PeakResponseTimeMs { get; set; }
        public int DatabaseConnections { get; set; }
        public bool IsHealthy { get; set; } = true;
        
        // Additional properties for controller usage
        public double AvgResponseTimeMs => (double)AverageResponseTimeMs;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
    }

    /// <summary>
    /// Performance history data for analysis
    /// </summary>
    public class PerformanceHistory
    {
        public TimeSpan TimeRange { get; set; }
        public List<PerformanceSnapshot> Snapshots { get; set; } = new();
        public decimal AverageResponseTime { get; set; }
        public decimal PeakResponseTime { get; set; }
        public decimal ErrorRate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Performance snapshot at a point in time
    /// </summary>
    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public int ResponseTimeMs { get; set; }
        public double AvgResponseTimeMs { get; set; } // Added missing property
        public int RequestCount { get; set; }
        public int ErrorCount { get; set; }
        public decimal CpuUsage { get; set; }
        public decimal MemoryUsage { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
    }
}