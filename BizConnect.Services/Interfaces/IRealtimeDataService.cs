using BizConnect.Services.DTOs;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for real-time data operations supporting modern UI requirements
/// </summary>
public interface IRealtimeDataService
{
    /// <summary>
    /// Get live dashboard statistics with <300ms response time requirement
    /// </summary>
    /// <returns>Real-time dashboard statistics</returns>
    Task<DashboardRealTimeStats> GetLiveStatsAsync();

    /// <summary>
    /// Get recent activities for dashboard feed
    /// </summary>
    /// <param name="count">Maximum number of activities to return</param>
    /// <returns>List of recent activities</returns>
    Task<List<RecentActivityDto>> GetRecentActivitiesAsync(int count = 10);

    /// <summary>
    /// Get KPI summary for specified time period
    /// </summary>
    /// <param name="period">Time period (today, week, month)</param>
    /// <returns>KPI summary with trends</returns>
    Task<KpiSummary> GetKpiSummaryAsync(string period);

    /// <summary>
    /// Get chart data for analytics visualization
    /// </summary>
    /// <param name="chartType">Type of chart data to retrieve</param>
    /// <param name="timeRange">Time range for data points</param>
    /// <returns>Chart data optimized for frontend consumption</returns>
    Task<ChartData> GetChartDataAsync(string chartType, string timeRange);

    /// <summary>
    /// Get trend analysis data for performance monitoring
    /// </summary>
    /// <param name="metricType">Type of metric to analyze</param>
    /// <param name="timeWindow">Time window for trend analysis</param>
    /// <returns>Trend analysis results</returns>
    Task<TrendAnalysis> GetTrendAnalysisAsync(string metricType, TimeSpan timeWindow);
}

/// <summary>
/// Service interface for system health monitoring
/// </summary>
public interface ISystemHealthService
{
    /// <summary>
    /// Get comprehensive system health status
    /// </summary>
    /// <returns>System health status with detailed checks</returns>
    Task<SystemHealthStatus> GetSystemHealthAsync();

    /// <summary>
    /// Get active alerts and notifications
    /// </summary>
    /// <param name="severityFilter">Optional severity filter</param>
    /// <returns>List of active alerts</returns>
    Task<List<AlertMessage>> GetActiveAlertsAsync(string? severityFilter = null);

    /// <summary>
    /// Get public system status for guest users
    /// </summary>
    /// <returns>Public system status information</returns>
    Task<PublicSystemStatus> GetPublicStatusAsync();

    /// <summary>
    /// Check specific system component health
    /// </summary>
    /// <param name="componentName">Name of component to check</param>
    /// <returns>Component health status</returns>
    Task<HealthCheck> CheckComponentHealthAsync(string componentName);
}

/// <summary>
/// Service interface for performance monitoring
/// </summary>
public interface IPerformanceMonitorService
{
    /// <summary>
    /// Get current performance metrics
    /// </summary>
    /// <returns>Current performance metrics</returns>
    Task<PerformanceMetrics> GetCurrentMetricsAsync();

    /// <summary>
    /// Get average response time for dashboard display
    /// </summary>
    /// <returns>Average response time in milliseconds</returns>
    Task<int> GetAvgResponseTimeAsync();

    /// <summary>
    /// Record performance metric for tracking
    /// </summary>
    /// <param name="endpoint">Endpoint name</param>
    /// <param name="responseTimeMs">Response time in milliseconds</param>
    /// <param name="isSuccess">Whether the request was successful</param>
    Task RecordMetricAsync(string endpoint, int responseTimeMs, bool isSuccess);

    /// <summary>
    /// Get performance history for analysis
    /// </summary>
    /// <param name="timeRange">Time range for performance data</param>
    /// <returns>Performance history data</returns>
    Task<PerformanceHistory> GetPerformanceHistoryAsync(TimeSpan timeRange);
}

/// <summary>
/// Service interface for trust indicators and system confidence
/// </summary>
public interface ITrustService
{
    /// <summary>
    /// Get trust indicators for guest landing page
    /// </summary>
    /// <returns>List of trust indicators to display</returns>
    Task<List<TrustIndicator>> GetTrustIndicatorsAsync();

    /// <summary>
    /// Get system confidence score
    /// </summary>
    /// <returns>Current system confidence score (0-100)</returns>
    Task<int> GetSystemConfidenceScoreAsync();

    /// <summary>
    /// Get security badges and certifications
    /// </summary>
    /// <returns>List of security badges to display</returns>
    Task<List<SecurityBadge>> GetSecurityBadgesAsync();
}

