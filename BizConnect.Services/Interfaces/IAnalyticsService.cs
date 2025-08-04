using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Models.Analytics;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service interface for analytics and dashboard metrics
    /// Aggregates data from multiple services to provide comprehensive system insights
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Gets comprehensive dashboard metrics for the main analytics dashboard
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete dashboard metrics with KPIs and trends</returns>
        Task<DashboardMetrics> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets real-time metrics for live dashboard updates
        /// Used for periodic refresh of key indicators
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Real-time system metrics</returns>
        Task<RealTimeMetrics> GetRealTimeMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets OTAC generation and validation trends over specified period
        /// </summary>
        /// <param name="days">Number of days to analyze (default: 7)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Trend data for chart visualization</returns>
        Task<TrendData> GetOtacTrendsAsync(int days = 7, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets comprehensive security metrics from monitoring service
        /// </summary>
        /// <param name="timeRange">Time range for security data analysis</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Security metrics and threat analysis</returns>
        Task<SecuritySummary> GetSecurityMetricsAsync(TimeSpan? timeRange = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets system performance metrics including response times and resource usage
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Performance metrics for optimization</returns>
        Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets registration statistics with trend analysis and status breakdown
        /// </summary>
        /// <param name="fromDate">Start date for statistics (default: 30 days ago)</param>
        /// <param name="toDate">End date for statistics (default: now)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Registration statistics with trends</returns>
        Task<RegistrationStats> GetRegistrationStatsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets KBank integration health and performance metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>KBank integration status and metrics</returns>
        Task<KBankIntegrationStatus> GetKBankStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets cache performance metrics from caching service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache performance statistics</returns>
        Task<CachePerformance> GetCachePerformanceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates composite system health score based on all metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>System health score (0-100)</returns>
        Task<double> CalculateSystemHealthScoreAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed analytics for a specific time period with custom filters
        /// </summary>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <param name="filters">Custom filters for data analysis</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed analytics data</returns>
        Task<Dictionary<string, object>> GetDetailedAnalyticsAsync(DateTime fromDate, DateTime toDate, Dictionary<string, object>? filters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets top performing branches by registration volume and success rate
        /// </summary>
        /// <param name="limit">Number of top branches to return (default: 10)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of top performing branches</returns>
        Task<List<BranchPerformance>> GetTopBranchesAsync(int limit = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets system alerts and anomalies detected in the analytics data
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of system alerts</returns>
        Task<List<SystemAlert>> GetSystemAlertsAsync(CancellationToken cancellationToken = default);
    }
}