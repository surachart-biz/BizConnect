using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BizConnect.Services.Interfaces;
using BizConnect.Services.DTOs;
using BizConnect.Models.Api;

namespace BizConnect.Controllers.Api;

/// <summary>
/// API controller for real-time dashboard data and system metrics
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardApiController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IRealtimeDataService _realtimeDataService;
    private readonly ISystemHealthService _systemHealthService;
    private readonly IPerformanceMonitorService _performanceService;
    private readonly ILogger<DashboardApiController> _logger;

    public DashboardApiController(
        IDashboardService dashboardService,
        IRealtimeDataService realtimeDataService,
        ISystemHealthService systemHealthService,
        IPerformanceMonitorService performanceService,
        ILogger<DashboardApiController> logger)
    {
        _dashboardService = dashboardService;
        _realtimeDataService = realtimeDataService;
        _systemHealthService = systemHealthService;
        _performanceService = performanceService;
        _logger = logger;
    }

    /// <summary>
    /// Get real-time dashboard statistics with <300ms response time requirement
    /// </summary>
    /// <returns>Real-time statistics for dashboard widgets</returns>
    [HttpGet("stats")]
    [ResponseCache(Duration = 60)] // Cache for 1 minute
    public async Task<ActionResult<ApiResponse<DashboardRealTimeStats>>> GetRealTimeStats()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var stats = await _realtimeDataService.GetLiveStatsAsync();

            stopwatch.Stop();
            _logger.LogDebug("Dashboard stats retrieved in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > 300)
            {
                _logger.LogWarning("Dashboard stats query exceeded 300ms threshold: {ElapsedMs}ms", 
                    stopwatch.ElapsedMilliseconds);
            }

            return Ok(ApiResponse<DashboardRealTimeStats>.Ok(stats, "Real-time stats retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving real-time dashboard stats");
            return StatusCode(500, ApiResponse<DashboardRealTimeStats>.Error("Failed to retrieve dashboard stats"));
        }
    }

    /// <summary>
    /// Get system health status for monitoring dashboard
    /// </summary>
    /// <returns>System health metrics and status indicators</returns>
    [HttpGet("system-health")]
    public async Task<ActionResult<ApiResponse<SystemHealthStatus>>> GetSystemHealth()
    {
        try
        {
            var healthStatus = await _systemHealthService.GetSystemHealthAsync();

            return Ok(ApiResponse<SystemHealthStatus>.Ok(healthStatus, "System health retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health status");
            return StatusCode(500, ApiResponse<SystemHealthStatus>.Error("Failed to retrieve system health"));
        }
    }

    /// <summary>
    /// Get recent activity for dashboard feed
    /// </summary>
    /// <param name="count">Number of recent activities to return (max 50)</param>
    /// <returns>List of recent system activities</returns>
    [HttpGet("recent-activity")]
    public async Task<ActionResult<ApiResponse<List<RecentActivityDto>>>> GetRecentActivity([FromQuery] int count = 10)
    {
        try
        {
            if (count < 1 || count > 50)
            {
                return BadRequest(ApiResponse<List<RecentActivityDto>>.Error("Count must be between 1 and 50"));
            }

            var activities = await _realtimeDataService.GetRecentActivitiesAsync(count);

            return Ok(ApiResponse<List<RecentActivityDto>>.Ok(activities, "Recent activities retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activities with count {Count}", count);
            return StatusCode(500, ApiResponse<List<RecentActivityDto>>.Error("Failed to retrieve recent activities"));
        }
    }

    /// <summary>
    /// Get performance metrics for system monitoring
    /// </summary>
    /// <returns>Performance metrics including response times and throughput</returns>
    [HttpGet("performance")]
    public async Task<ActionResult<ApiResponse<PerformanceMetrics>>> GetPerformanceMetrics()
    {
        try
        {
            var metrics = await _performanceService.GetCurrentMetricsAsync();

            return Ok(ApiResponse<PerformanceMetrics>.Ok(metrics, "Performance metrics retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return StatusCode(500, ApiResponse<PerformanceMetrics>.Error("Failed to retrieve performance metrics"));
        }
    }

    /// <summary>
    /// Get KPI summary for executive dashboard
    /// </summary>
    /// <param name="period">Time period for KPI calculation (today, week, month)</param>
    /// <returns>Key Performance Indicators summary</returns>
    [HttpGet("kpi")]
    public async Task<ActionResult<ApiResponse<KpiSummary>>> GetKpiSummary([FromQuery] string period = "today")
    {
        try
        {
            if (!new[] { "today", "week", "month" }.Contains(period.ToLower()))
            {
                return BadRequest(ApiResponse<KpiSummary>.Error("Period must be 'today', 'week', or 'month'"));
            }

            var kpiSummary = await _realtimeDataService.GetKpiSummaryAsync(period);

            return Ok(ApiResponse<KpiSummary>.Ok(kpiSummary, $"KPI summary for {period} retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI summary for period {Period}", period);
            return StatusCode(500, ApiResponse<KpiSummary>.Error("Failed to retrieve KPI summary"));
        }
    }

    /// <summary>
    /// Get alerts and notifications for admin dashboard
    /// </summary>
    /// <param name="severity">Filter by severity level (info, warning, error, critical)</param>
    /// <returns>System alerts and notifications</returns>
    [HttpGet("alerts")]
    public async Task<ActionResult<ApiResponse<List<AlertMessage>>>> GetAlerts([FromQuery] string? severity = null)
    {
        try
        {
            var alerts = await _systemHealthService.GetActiveAlertsAsync(severity);

            return Ok(ApiResponse<List<AlertMessage>>.Ok(alerts, "Alerts retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts with severity filter {Severity}", severity);
            return StatusCode(500, ApiResponse<List<AlertMessage>>.Error("Failed to retrieve alerts"));
        }
    }

    /// <summary>
    /// Get chart data for analytics dashboard
    /// </summary>
    /// <param name="chartType">Type of chart data (registrations, success-rate, otac-usage, response-times)</param>
    /// <param name="timeRange">Time range for chart data (24h, 7d, 30d)</param>
    /// <returns>Chart data optimized for frontend visualization</returns>
    [HttpGet("charts/{chartType}")]
    public async Task<ActionResult<ApiResponse<ChartData>>> GetChartData(string chartType, [FromQuery] string timeRange = "24h")
    {
        try
        {
            var validChartTypes = new[] { "registrations", "success-rate", "otac-usage", "response-times" };
            var validTimeRanges = new[] { "24h", "7d", "30d" };

            if (!validChartTypes.Contains(chartType.ToLower()))
            {
                return BadRequest(ApiResponse<ChartData>.Error($"Invalid chart type. Supported types: {string.Join(", ", validChartTypes)}"));
            }

            if (!validTimeRanges.Contains(timeRange.ToLower()))
            {
                return BadRequest(ApiResponse<ChartData>.Error($"Invalid time range. Supported ranges: {string.Join(", ", validTimeRanges)}"));
            }

            var chartData = await _realtimeDataService.GetChartDataAsync(chartType, timeRange);

            return Ok(ApiResponse<ChartData>.Ok(chartData, $"Chart data for {chartType} retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chart data for type {ChartType} and range {TimeRange}", chartType, timeRange);
            return StatusCode(500, ApiResponse<ChartData>.Error("Failed to retrieve chart data"));
        }
    }
}