using System;
using System.Collections.Generic;

namespace BizConnect.Services.Models.Analytics
{
    /// <summary>
    /// Main dashboard metrics model containing all key performance indicators
    /// for real-time analytics display
    /// </summary>
    public class DashboardMetrics
    {
        /// <summary>
        /// Number of currently active OTAC codes (not expired, not used)
        /// </summary>
        public int ActiveOtacCount { get; set; }

        /// <summary>
        /// Registration statistics and trends
        /// </summary>
        public RegistrationStats Registrations { get; set; } = new();

        /// <summary>
        /// Overall system success rate (percentage)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average API response time in milliseconds
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// KBank integration status and performance
        /// </summary>
        public KBankIntegrationStatus KBankStatus { get; set; } = new();

        /// <summary>
        /// Security monitoring summary
        /// </summary>
        public SecuritySummary Security { get; set; } = new();

        /// <summary>
        /// Cache performance metrics
        /// </summary>
        public CachePerformance Cache { get; set; } = new();

        /// <summary>
        /// Composite system health score (0-100)
        /// </summary>
        public double SystemHealthScore { get; set; }

        /// <summary>
        /// Timestamp when metrics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Registration statistics with trend analysis
    /// </summary>
    public class RegistrationStats
    {
        /// <summary>
        /// Number of registrations today
        /// </summary>
        public int Today { get; set; }

        /// <summary>
        /// Number of registrations this week
        /// </summary>
        public int ThisWeek { get; set; }

        /// <summary>
        /// Number of registrations this month
        /// </summary>
        public int ThisMonth { get; set; }

        /// <summary>
        /// Percentage change compared to yesterday
        /// </summary>
        public double DailyGrowthPercent { get; set; }

        /// <summary>
        /// Percentage change compared to last week
        /// </summary>
        public double WeeklyGrowthPercent { get; set; }

        /// <summary>
        /// Percentage change compared to last month
        /// </summary>
        public double MonthlyGrowthPercent { get; set; }

        /// <summary>
        /// Registration status breakdown
        /// </summary>
        public StatusBreakdown StatusBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Breakdown of registration statuses
    /// </summary>
    public class StatusBreakdown
    {
        public int Pending { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public int Expired { get; set; }
    }

    /// <summary>
    /// KBank integration status and performance metrics
    /// </summary>
    public class KBankIntegrationStatus
    {
        /// <summary>
        /// Current health status (Online, Degraded, Offline)
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// Success rate percentage for KBank API calls
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average response time for KBank API calls
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Timestamp of last successful API call
        /// </summary>
        public DateTime? LastSuccessfulCall { get; set; }

        /// <summary>
        /// Number of API calls made today
        /// </summary>
        public int CallsToday { get; set; }

        /// <summary>
        /// Number of failed API calls today
        /// </summary>
        public int FailuresToday { get; set; }
    }

    /// <summary>
    /// Security monitoring summary for dashboard display
    /// </summary>
    public class SecuritySummary
    {
        /// <summary>
        /// Total number of security events detected today
        /// </summary>
        public int EventsToday { get; set; }

        /// <summary>
        /// Number of threats detected today
        /// </summary>
        public int ThreatsDetected { get; set; }

        /// <summary>
        /// Number of blocked IP addresses
        /// </summary>
        public int BlockedIps { get; set; }

        /// <summary>
        /// Number of active security alerts
        /// </summary>
        public int ActiveAlerts { get; set; }

        /// <summary>
        /// Current security threat level (Low, Medium, High, Critical)
        /// </summary>
        public string ThreatLevel { get; set; } = "Low";

        /// <summary>
        /// Top threat IP addresses
        /// </summary>
        public List<string> TopThreatIps { get; set; } = new();
    }

    /// <summary>
    /// Cache performance metrics for system optimization
    /// </summary>
    public class CachePerformance
    {
        /// <summary>
        /// Cache hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Total number of cache hits
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Total number of cache misses
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Number of cache entries evicted
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Current number of entries in cache
        /// </summary>
        public int CurrentEntryCount { get; set; }

        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double MemoryUsagePercent { get; set; }
    }

    /// <summary>
    /// Real-time metrics for live dashboard updates
    /// </summary>
    public class RealTimeMetrics
    {
        /// <summary>
        /// Timestamp when metrics were captured
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current active OTAC count
        /// </summary>
        public int ActiveOtacCount { get; set; }

        /// <summary>
        /// OTAC codes generated in the last hour
        /// </summary>
        public int OtacGeneratedLastHour { get; set; }

        /// <summary>
        /// Registrations submitted in the last hour
        /// </summary>
        public int RegistrationsLastHour { get; set; }

        /// <summary>
        /// Current system response time
        /// </summary>
        public double CurrentResponseTime { get; set; }

        /// <summary>
        /// Current CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Current memory usage percentage
        /// </summary>
        public double MemoryUsage { get; set; }

        /// <summary>
        /// Recent security events count
        /// </summary>
        public int RecentSecurityEvents { get; set; }

        /// <summary>
        /// Cache hit rate for the last hour
        /// </summary>
        public double RecentCacheHitRate { get; set; }
    }

    /// <summary>
    /// Trend data for chart visualization
    /// </summary>
    public class TrendData
    {
        /// <summary>
        /// Time period labels for chart x-axis
        /// </summary>
        public List<string> Labels { get; set; } = new();

        /// <summary>
        /// OTAC generation trend data
        /// </summary>
        public List<int> OtacTrend { get; set; } = new();

        /// <summary>
        /// Registration trend data
        /// </summary>
        public List<int> RegistrationTrend { get; set; } = new();

        /// <summary>
        /// Success rate trend data
        /// </summary>
        public List<double> SuccessRateTrend { get; set; } = new();

        /// <summary>
        /// Response time trend data
        /// </summary>
        public List<double> ResponseTimeTrend { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics for system monitoring
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Server response time statistics
        /// </summary>
        public ResponseTimeStats ResponseTime { get; set; } = new();

        /// <summary>
        /// Database performance metrics
        /// </summary>
        public DatabasePerformance Database { get; set; } = new();

        /// <summary>
        /// API endpoint performance breakdown
        /// </summary>
        public Dictionary<string, EndpointMetrics> EndpointMetrics { get; set; } = new();

        /// <summary>
        /// Background job performance
        /// </summary>
        public BackgroundJobMetrics BackgroundJobs { get; set; } = new();
    }

    /// <summary>
    /// Response time statistics
    /// </summary>
    public class ResponseTimeStats
    {
        public double Average { get; set; }
        public double Median { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
    }

    /// <summary>
    /// Database performance metrics
    /// </summary>
    public class DatabasePerformance
    {
        public double AverageQueryTime { get; set; }
        public int ActiveConnections { get; set; }
        public long QueryCount { get; set; }
        public double CacheHitRatio { get; set; }
        public long DeadlockCount { get; set; }
    }

    /// <summary>
    /// Individual endpoint performance metrics
    /// </summary>
    public class EndpointMetrics
    {
        public string Endpoint { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public double AverageResponseTime { get; set; }
        public double SuccessRate { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastAccessed { get; set; }
    }

    /// <summary>
    /// Background job performance metrics
    /// </summary>
    public class BackgroundJobMetrics
    {
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int PendingJobs { get; set; }
        public double AverageExecutionTime { get; set; }
        public DateTime LastJobExecution { get; set; }
    }

    /// <summary>
    /// System alert information for monitoring and notifications
    /// </summary>
    public class SystemAlert
    {
        /// <summary>
        /// Alert title/summary
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed alert description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity level
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert category
        /// </summary>
        public AlertCategory Category { get; set; }

        /// <summary>
        /// When the alert was detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the alert has been acknowledged
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// When the alert was acknowledged
        /// </summary>
        public DateTime? AcknowledgedAt { get; set; }

        /// <summary>
        /// User who acknowledged the alert
        /// </summary>
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// Additional metadata for the alert
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Alert ID for tracking
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Branch performance metrics for ranking and comparison
    /// </summary>
    public class BranchPerformance
    {
        /// <summary>
        /// Branch database ID
        /// </summary>
        public int BranchId { get; set; }

        /// <summary>
        /// Branch display name
        /// </summary>
        public string BranchName { get; set; } = string.Empty;

        /// <summary>
        /// Branch code identifier
        /// </summary>
        public string BranchCode { get; set; } = string.Empty;

        /// <summary>
        /// Total number of registrations processed
        /// </summary>
        public int RegistrationCount { get; set; }

        /// <summary>
        /// Success rate percentage for the branch
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average processing time for registrations
        /// </summary>
        public double AverageProcessingTime { get; set; }

        /// <summary>
        /// Growth rate percentage compared to previous period
        /// </summary>
        public double GrowthRate { get; set; }

        /// <summary>
        /// Branch rank position based on performance
        /// </summary>
        public int RankPosition { get; set; }

        /// <summary>
        /// Last activity timestamp
        /// </summary>
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Performance score (composite metric)
        /// </summary>
        public double PerformanceScore { get; set; }
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        /// <summary>
        /// Informational alert
        /// </summary>
        Info = 1,

        /// <summary>
        /// Warning that requires attention
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Error that needs immediate action
        /// </summary>
        Error = 3,

        /// <summary>
        /// Critical system failure
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Alert categories for classification
    /// </summary>
    public enum AlertCategory
    {
        /// <summary>
        /// System performance related
        /// </summary>
        Performance,

        /// <summary>
        /// Security related
        /// </summary>
        Security,

        /// <summary>
        /// External integration related
        /// </summary>
        Integration,

        /// <summary>
        /// Database related
        /// </summary>
        Database,

        /// <summary>
        /// Application error related
        /// </summary>
        Application,

        /// <summary>
        /// Infrastructure related
        /// </summary>
        Infrastructure
    }

    /// <summary>
    /// Individual metric card data for dashboard widgets
    /// </summary>
    public class MetricCard
    {
        /// <summary>
        /// Card title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Primary metric value
        /// </summary>
        public object Value { get; set; } = 0;

        /// <summary>
        /// Metric unit (%, ms, count, etc.)
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Trend indicator (up, down, stable)
        /// </summary>
        public TrendIndicator Trend { get; set; }

        /// <summary>
        /// Trend percentage change
        /// </summary>
        public double TrendPercent { get; set; }

        /// <summary>
        /// Card icon identifier
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Card color scheme
        /// </summary>
        public string ColorScheme { get; set; } = "primary";

        /// <summary>
        /// Whether the metric is healthy/within bounds
        /// </summary>
        public bool IsHealthy { get; set; } = true;
    }

    /// <summary>
    /// Chart data structure for visualization components
    /// </summary>
    public class ChartData
    {
        /// <summary>
        /// Chart labels (x-axis)
        /// </summary>
        public List<string> Labels { get; set; } = new();

        /// <summary>
        /// Chart datasets
        /// </summary>
        public List<ChartDataset> Datasets { get; set; } = new();

        /// <summary>
        /// Chart options
        /// </summary>
        public Dictionary<string, object> Options { get; set; } = new();
    }

    /// <summary>
    /// Individual chart dataset
    /// </summary>
    public class ChartDataset
    {
        /// <summary>
        /// Dataset label
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Dataset values
        /// </summary>
        public List<double> Data { get; set; } = new();

        /// <summary>
        /// Dataset color
        /// </summary>
        public string BackgroundColor { get; set; } = string.Empty;

        /// <summary>
        /// Border color
        /// </summary>
        public string BorderColor { get; set; } = string.Empty;

        /// <summary>
        /// Fill area under line
        /// </summary>
        public bool Fill { get; set; }
    }

    /// <summary>
    /// Trend direction indicators
    /// </summary>
    public enum TrendIndicator
    {
        /// <summary>
        /// Trend is going up (positive)
        /// </summary>
        Up,

        /// <summary>
        /// Trend is going down (negative)
        /// </summary>
        Down,

        /// <summary>
        /// Trend is stable (no significant change)
        /// </summary>
        Stable,

        /// <summary>
        /// Trend data is not available
        /// </summary>
        Unknown
    }
}