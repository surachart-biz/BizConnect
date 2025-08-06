using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BizConnect.Services.Interfaces;
using BizConnect.Services.DTOs;
using BizConnect.Models.Api;
using System.Text;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace BizConnect.Controllers.Api;

/// <summary>
/// API controller for analytics data and reporting functionality
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin,Employee")]
[Produces("application/json")]
public class AnalyticsApiController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IRealtimeDataService _realtimeDataService;
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<AnalyticsApiController> _logger;

    public AnalyticsApiController(
        IAnalyticsService analyticsService,
        IRealtimeDataService realtimeDataService,
        IDashboardService dashboardService,
        ILogger<AnalyticsApiController> logger)
    {
        _analyticsService = analyticsService;
        _realtimeDataService = realtimeDataService;
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Get registration chart data for analytics dashboard
    /// </summary>
    /// <param name="from">Start date for chart data</param>
    /// <param name="to">End date for chart data</param>
    /// <returns>Chart data optimized for Chart.js</returns>
    [HttpGet("charts/registrations")]
    [ResponseCache(Duration = 300)] // Cache for 5 minutes
    public async Task<ActionResult<ApiResponse<ChartData>>> GetRegistrationChartData([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        try
        {
            // Default to last 30 days if no dates provided
            var fromDate = from ?? DateTime.Today.AddDays(-30);
            var toDate = to ?? DateTime.Today.AddDays(1);

            if (fromDate > toDate)
            {
                return BadRequest(ApiResponse<ChartData>.Error("From date cannot be after To date"));
            }

            var chartData = await _realtimeDataService.GetChartDataAsync("registrations", "custom");
            
            // Filter data by date range if custom dates provided
            if (from.HasValue || to.HasValue)
            {
                chartData.DataPoints = chartData.DataPoints
                    .Where(dp => dp.Timestamp >= fromDate && dp.Timestamp <= toDate)
                    .ToList();
            }

            chartData.Metadata.Title = "Registration Trends";
            chartData.Metadata.XAxisLabel = "Date";
            chartData.Metadata.YAxisLabel = "Number of Registrations";
            chartData.Metadata.Unit = "registrations";

            return Ok(ApiResponse<ChartData>.Ok(chartData, "Registration chart data retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving registration chart data from {From} to {To}", from, to);
            return StatusCode(500, ApiResponse<ChartData>.Error("Failed to retrieve registration chart data"));
        }
    }

    /// <summary>
    /// Get OTAC usage statistics for analytics
    /// </summary>
    /// <param name="days">Number of days to analyze (default 7)</param>
    /// <returns>OTAC usage data with trends</returns>
    [HttpGet("charts/otac-usage")]
    [ResponseCache(Duration = 180)] // Cache for 3 minutes
    public async Task<ActionResult<ApiResponse<ChartData>>> GetOtacUsageData([FromQuery] int days = 7)
    {
        try
        {
            if (days < 1 || days > 365)
            {
                return BadRequest(ApiResponse<ChartData>.Error("Days must be between 1 and 365"));
            }

            var timeRange = days switch
            {
                <= 7 => "7d",
                <= 30 => "30d",
                _ => "custom"
            };

            var chartData = await _realtimeDataService.GetChartDataAsync("otac-usage", timeRange);

            chartData.Metadata.Title = "OTAC Code Usage Statistics";
            chartData.Metadata.XAxisLabel = "Date";
            chartData.Metadata.YAxisLabel = "OTAC Codes";
            chartData.Metadata.Unit = "codes";
            chartData.Metadata.Colors = new List<string> { "#3498db", "#2ecc71", "#e74c3c", "#f39c12" };

            return Ok(ApiResponse<ChartData>.Ok(chartData, "OTAC usage data retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OTAC usage data for {Days} days", days);
            return StatusCode(500, ApiResponse<ChartData>.Error("Failed to retrieve OTAC usage data"));
        }
    }

    /// <summary>
    /// Get success rate trends for analytics
    /// </summary>
    /// <param name="timeRange">Time range for success rate data (24h, 7d, 30d)</param>
    /// <returns>Success rate chart data</returns>
    [HttpGet("charts/success-rate")]
    [ResponseCache(Duration = 120)] // Cache for 2 minutes
    public async Task<ActionResult<ApiResponse<ChartData>>> GetSuccessRateData([FromQuery] string timeRange = "7d")
    {
        try
        {
            var validTimeRanges = new[] { "24h", "7d", "30d" };
            if (!validTimeRanges.Contains(timeRange.ToLower()))
            {
                return BadRequest(ApiResponse<ChartData>.Error($"Invalid time range. Supported ranges: {string.Join(", ", validTimeRanges)}"));
            }

            var chartData = await _realtimeDataService.GetChartDataAsync("success-rate", timeRange);

            chartData.Metadata.Title = "Registration Success Rate";
            chartData.Metadata.XAxisLabel = "Date";
            chartData.Metadata.YAxisLabel = "Success Rate (%)";
            chartData.Metadata.Unit = "%";
            chartData.Metadata.Colors = new List<string> { "#2ecc71" };

            return Ok(ApiResponse<ChartData>.Ok(chartData, "Success rate data retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving success rate data for range {TimeRange}", timeRange);
            return StatusCode(500, ApiResponse<ChartData>.Error("Failed to retrieve success rate data"));
        }
    }

    /// <summary>
    /// Get comprehensive analytics summary
    /// </summary>
    /// <param name="period">Analysis period (today, week, month)</param>
    /// <returns>Analytics summary with key metrics</returns>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<AnalyticsSummary>>> GetAnalyticsSummary([FromQuery] string period = "week")
    {
        try
        {
            var validPeriods = new[] { "today", "week", "month" };
            if (!validPeriods.Contains(period.ToLower()))
            {
                return BadRequest(ApiResponse<AnalyticsSummary>.Error($"Invalid period. Supported periods: {string.Join(", ", validPeriods)}"));
            }

            var kpiSummary = await _realtimeDataService.GetKpiSummaryAsync(period);
            var realTimeStats = await _realtimeDataService.GetLiveStatsAsync();

            var analyticsSummary = new AnalyticsSummary
            {
                Period = period,
                TotalRegistrations = kpiSummary.TotalRegistrations,
                SuccessfulRegistrations = kpiSummary.SuccessfulRegistrations,
                FailedRegistrations = kpiSummary.FailedRegistrations,
                SuccessRate = kpiSummary.SuccessRate,
                OtacGenerated = kpiSummary.OtacGenerated,
                OtacUsed = kpiSummary.OtacUsed,
                OtacUsageRate = kpiSummary.OtacUsageRate,
                AvgProcessingTimeMs = kpiSummary.AvgProcessingTimeMs,
                ComparisonToPreviousPeriod = kpiSummary.ComparisonToPreviousPeriod,
                Trends = kpiSummary.Trends,
                GeneratedAt = DateTime.UtcNow
            };

            return Ok(ApiResponse<AnalyticsSummary>.Ok(analyticsSummary, $"Analytics summary for {period} retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics summary for period {Period}", period);
            return StatusCode(500, ApiResponse<AnalyticsSummary>.Error("Failed to retrieve analytics summary"));
        }
    }

    /// <summary>
    /// Export analytics data in specified format
    /// </summary>
    /// <param name="format">Export format (csv, excel, pdf)</param>
    /// <param name="period">Data period to export (today, week, month)</param>
    /// <returns>File download with analytics data</returns>
    [HttpGet("export/{format}")]
    public async Task<IActionResult> ExportAnalytics(string format, [FromQuery] string period = "week")
    {
        try
        {
            var validFormats = new[] { "csv", "excel", "pdf" };
            var validPeriods = new[] { "today", "week", "month" };

            if (!validFormats.Contains(format.ToLower()))
            {
                return BadRequest(ApiResponse<object>.Error($"Invalid format. Supported formats: {string.Join(", ", validFormats)}"));
            }

            if (!validPeriods.Contains(period.ToLower()))
            {
                return BadRequest(ApiResponse<object>.Error($"Invalid period. Supported periods: {string.Join(", ", validPeriods)}"));
            }

            var kpiSummary = await _realtimeDataService.GetKpiSummaryAsync(period);
            var recentActivities = await _realtimeDataService.GetRecentActivitiesAsync(50);

            var fileName = $"analytics_export_{period}_{DateTime.Now:yyyyMMdd_HHmmss}";

            return format.ToLower() switch
            {
                "csv" => await ExportToCsv(kpiSummary, recentActivities, fileName),
                "excel" => await ExportToExcel(kpiSummary, recentActivities, fileName),
                "pdf" => await ExportToPdf(kpiSummary, recentActivities, fileName),
                _ => BadRequest(ApiResponse<object>.Error("Unsupported export format"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting analytics data in format {Format} for period {Period}", format, period);
            return StatusCode(500, ApiResponse<object>.Error("Failed to export analytics data"));
        }
    }

    /// <summary>
    /// Get trend analysis for specific metric
    /// </summary>
    /// <param name="metricType">Type of metric to analyze</param>
    /// <param name="timeWindow">Time window in hours for analysis</param>
    /// <returns>Trend analysis data</returns>
    [HttpGet("trends/{metricType}")]
    public async Task<ActionResult<ApiResponse<BizConnect.Services.DTOs.TrendAnalysis>>> GetTrendAnalysis(string metricType, [FromQuery] int timeWindow = 168)
    {
        try
        {
            var validMetrics = new[] { "registrations", "success-rate", "otac-usage", "response-time" };
            if (!validMetrics.Contains(metricType.ToLower()))
            {
                return BadRequest(ApiResponse<BizConnect.Services.DTOs.TrendAnalysis>.Error($"Invalid metric type. Supported metrics: {string.Join(", ", validMetrics)}"));
            }

            if (timeWindow < 1 || timeWindow > 8760) // Max 1 year
            {
                return BadRequest(ApiResponse<BizConnect.Services.DTOs.TrendAnalysis>.Error("Time window must be between 1 and 8760 hours"));
            }

            var trendAnalysis = await _realtimeDataService.GetTrendAnalysisAsync(metricType, TimeSpan.FromHours(timeWindow));

            return Ok(ApiResponse<BizConnect.Services.DTOs.TrendAnalysis>.Ok(trendAnalysis, $"Trend analysis for {metricType} retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trend analysis for metric {MetricType} with window {TimeWindow}h", metricType, timeWindow);
            return StatusCode(500, ApiResponse<BizConnect.Services.DTOs.TrendAnalysis>.Error("Failed to retrieve trend analysis"));
        }
    }

    #region Private Export Methods

    private async Task<IActionResult> ExportToCsv(KpiSummary kpiSummary, List<RecentActivityDto> activities, string fileName)
    {
        var csv = new StringBuilder();
        
        // Add KPI summary
        csv.AppendLine("KPI Summary");
        csv.AppendLine($"Period,{kpiSummary.Period}");
        csv.AppendLine($"Total Registrations,{kpiSummary.TotalRegistrations}");
        csv.AppendLine($"Successful Registrations,{kpiSummary.SuccessfulRegistrations}");
        csv.AppendLine($"Failed Registrations,{kpiSummary.FailedRegistrations}");
        csv.AppendLine($"Success Rate,{kpiSummary.SuccessRate:F2}%");
        csv.AppendLine($"OTAC Generated,{kpiSummary.OtacGenerated}");
        csv.AppendLine($"OTAC Used,{kpiSummary.OtacUsed}");
        csv.AppendLine($"OTAC Usage Rate,{kpiSummary.OtacUsageRate:F2}%");
        csv.AppendLine();

        // Add recent activities
        csv.AppendLine("Recent Activities");
        csv.AppendLine("Timestamp,Type,Description,User,Status");
        foreach (var activity in activities.Take(20))
        {
            csv.AppendLine($"{activity.Timestamp:yyyy-MM-dd HH:mm:ss},{activity.ActivityType},{activity.Description},{activity.UserName},{activity.Status}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"{fileName}.csv");
    }

    private async Task<IActionResult> ExportToExcel(KpiSummary kpiSummary, List<RecentActivityDto> activities, string fileName)
    {
        using var workbook = new XLWorkbook();
        
        // KPI Summary sheet
        var kpiSheet = workbook.Worksheets.Add("KPI Summary");
        kpiSheet.Cell(1, 1).Value = "Metric";
        kpiSheet.Cell(1, 2).Value = "Value";
        kpiSheet.Cell(2, 1).Value = "Period";
        kpiSheet.Cell(2, 2).Value = kpiSummary.Period;
        kpiSheet.Cell(3, 1).Value = "Total Registrations";
        kpiSheet.Cell(3, 2).Value = kpiSummary.TotalRegistrations;
        kpiSheet.Cell(4, 1).Value = "Successful Registrations";
        kpiSheet.Cell(4, 2).Value = kpiSummary.SuccessfulRegistrations;
        kpiSheet.Cell(5, 1).Value = "Failed Registrations";
        kpiSheet.Cell(5, 2).Value = kpiSummary.FailedRegistrations;
        kpiSheet.Cell(6, 1).Value = "Success Rate (%)";
        kpiSheet.Cell(6, 2).Value = kpiSummary.SuccessRate;

        // Activities sheet
        var activitiesSheet = workbook.Worksheets.Add("Recent Activities");
        activitiesSheet.Cell(1, 1).Value = "Timestamp";
        activitiesSheet.Cell(1, 2).Value = "Type";
        activitiesSheet.Cell(1, 3).Value = "Description";
        activitiesSheet.Cell(1, 4).Value = "User";
        activitiesSheet.Cell(1, 5).Value = "Status";

        var row = 2;
        foreach (var activity in activities.Take(100))
        {
            activitiesSheet.Cell(row, 1).Value = activity.Timestamp;
            activitiesSheet.Cell(row, 2).Value = activity.ActivityType;
            activitiesSheet.Cell(row, 3).Value = activity.Description;
            activitiesSheet.Cell(row, 4).Value = activity.UserName;
            activitiesSheet.Cell(row, 5).Value = activity.Status;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
    }

    private async Task<IActionResult> ExportToPdf(KpiSummary kpiSummary, List<RecentActivityDto> activities, string fileName)
    {
        using var stream = new MemoryStream();
        var document = new Document();
        PdfWriter.GetInstance(document, stream);
        document.Open();

        // Title
        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
        document.Add(new Paragraph($"Analytics Export - {kpiSummary.Period}", titleFont));
        document.Add(new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
        document.Add(new Paragraph(" ")); // Empty line

        // KPI Summary
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
        document.Add(new Paragraph("KPI Summary", headerFont));
        
        var table = new PdfPTable(2);
        table.AddCell("Metric");
        table.AddCell("Value");
        table.AddCell("Total Registrations");
        table.AddCell(kpiSummary.TotalRegistrations.ToString());
        table.AddCell("Successful Registrations");
        table.AddCell(kpiSummary.SuccessfulRegistrations.ToString());
        table.AddCell("Success Rate");
        table.AddCell($"{kpiSummary.SuccessRate:F2}%");
        table.AddCell("OTAC Generated");
        table.AddCell(kpiSummary.OtacGenerated.ToString());
        table.AddCell("OTAC Used");
        table.AddCell(kpiSummary.OtacUsed.ToString());
        
        document.Add(table);
        document.Add(new Paragraph(" ")); // Empty line

        // Recent Activities (limited for PDF)
        document.Add(new Paragraph("Recent Activities (Last 10)", headerFont));
        var activitiesTable = new PdfPTable(4);
        activitiesTable.AddCell("Timestamp");
        activitiesTable.AddCell("Type");
        activitiesTable.AddCell("Description");
        activitiesTable.AddCell("Status");

        foreach (var activity in activities.Take(10))
        {
            activitiesTable.AddCell(activity.Timestamp.ToString("yyyy-MM-dd HH:mm"));
            activitiesTable.AddCell(activity.ActivityType);
            activitiesTable.AddCell(activity.Description.Length > 50 ? 
                activity.Description.Substring(0, 47) + "..." : activity.Description);
            activitiesTable.AddCell(activity.Status);
        }

        document.Add(activitiesTable);
        document.Close();

        var bytes = stream.ToArray();
        return File(bytes, "application/pdf", $"{fileName}.pdf");
    }

    #endregion
}

/// <summary>
/// Analytics summary model for API response
/// </summary>
public class AnalyticsSummary
{
    public string Period { get; set; } = string.Empty;
    public int TotalRegistrations { get; set; }
    public int SuccessfulRegistrations { get; set; }
    public int FailedRegistrations { get; set; }
    public decimal SuccessRate { get; set; }
    public int OtacGenerated { get; set; }
    public int OtacUsed { get; set; }
    public decimal OtacUsageRate { get; set; }
    public int AvgProcessingTimeMs { get; set; }
    public decimal ComparisonToPreviousPeriod { get; set; }
    public List<TrendDataPoint> Trends { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Trend analysis model for API response
/// </summary>
public class TrendAnalysis
{
    public string MetricType { get; set; } = string.Empty;
    public TimeSpan TimeWindow { get; set; }
    public List<TrendDataPoint> DataPoints { get; set; } = new();
    public string TrendDirection { get; set; } = "Stable"; // Up, Down, Stable
    public decimal TrendPercentage { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public bool IsSignificant { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}