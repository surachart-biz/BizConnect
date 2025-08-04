using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Analytics;

namespace BizConnect.Areas.Admin.Controllers
{
    /// <summary>
    /// Controller for analytics dashboard and real-time metrics
    /// Provides executive-level insights and system performance monitoring
    /// </summary>
    [Area("Admin")]
    [Authorize(Policy = "AdminOrEmployee")]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            ILogger<AnalyticsController> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Main analytics dashboard view
        /// </summary>
        /// <returns>Dashboard view with comprehensive metrics</returns>
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                ViewData["Title"] = "Analytics Dashboard";
                ViewBag.Subtitle = "Real-time system insights and performance metrics";
                ViewBag.BreadcrumbSection = "Analytics";

                _logger.LogInformation("Loading analytics dashboard for user {User}", User.Identity?.Name);

                var metrics = await _analyticsService.GetDashboardMetricsAsync();
                
                return View(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load analytics dashboard");
                
                // Return view with empty metrics on error
                var emptyMetrics = new DashboardMetrics
                {
                    ActiveOtacCount = 0,
                    Registrations = new RegistrationStats(),
                    SuccessRate = 0,
                    AverageResponseTime = 0,
                    KBankStatus = new KBankIntegrationStatus { Status = "Error" },
                    Security = new SecuritySummary { ThreatLevel = "Unknown" },
                    Cache = new CachePerformance(),
                    SystemHealthScore = 0,
                    LastUpdated = DateTime.UtcNow
                };

                TempData["Error"] = "Unable to load analytics data. Please try again later.";
                return View(emptyMetrics);
            }
        }

        /// <summary>
        /// API endpoint for real-time metrics updates
        /// Called every 30 seconds by the dashboard JavaScript
        /// </summary>
        /// <returns>JSON with real-time metrics</returns>
        [HttpGet]
        public async Task<IActionResult> GetRealTimeMetrics()
        {
            try
            {
                _logger.LogDebug("Fetching real-time metrics for dashboard update");

                var metrics = await _analyticsService.GetRealTimeMetricsAsync();
                
                return Json(new
                {
                    success = true,
                    data = metrics,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch real-time metrics");
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve real-time metrics",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for OTAC trend data
        /// Used for chart visualization
        /// </summary>
        /// <param name="days">Number of days to analyze (default: 7)</param>
        /// <returns>JSON with trend data for charts</returns>
        [HttpGet]
        public async Task<IActionResult> GetOtacTrends(int days = 7)
        {
            try
            {
                if (days < 1 || days > 90)
                {
                    return BadRequest(new { success = false, error = "Days parameter must be between 1 and 90" });
                }

                _logger.LogDebug("Fetching OTAC trends for {Days} days", days);

                var trends = await _analyticsService.GetOtacTrendsAsync(days);
                
                return Json(new
                {
                    success = true,
                    data = trends,
                    period = $"{days} days",
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch OTAC trends for {Days} days", days);
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve OTAC trends",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for security metrics
        /// </summary>
        /// <param name="hours">Time range in hours (default: 24)</param>
        /// <returns>JSON with security metrics</returns>
        [HttpGet]
        public async Task<IActionResult> GetSecurityMetrics(int hours = 24)
        {
            try
            {
                if (hours < 1 || hours > 168) // Max 1 week
                {
                    return BadRequest(new { success = false, error = "Hours parameter must be between 1 and 168" });
                }

                _logger.LogDebug("Fetching security metrics for {Hours} hours", hours);

                var metrics = await _analyticsService.GetSecurityMetricsAsync(TimeSpan.FromHours(hours));
                
                return Json(new
                {
                    success = true,
                    data = metrics,
                    time_range = $"{hours} hours",
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch security metrics for {Hours} hours", hours);
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve security metrics",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for performance metrics
        /// </summary>
        /// <returns>JSON with system performance data</returns>
        [HttpGet]
        public async Task<IActionResult> GetPerformanceMetrics()
        {
            try
            {
                _logger.LogDebug("Fetching system performance metrics");

                var metrics = await _analyticsService.GetPerformanceMetricsAsync();
                
                return Json(new
                {
                    success = true,
                    data = metrics,
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch performance metrics");
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve performance metrics",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for top performing branches
        /// </summary>
        /// <param name="limit">Number of top branches to return (default: 10)</param>
        /// <returns>JSON with top branch performance data</returns>
        [HttpGet]
        public async Task<IActionResult> GetTopBranches(int limit = 10)
        {
            try
            {
                if (limit < 1 || limit > 50)
                {
                    return BadRequest(new { success = false, error = "Limit parameter must be between 1 and 50" });
                }

                _logger.LogDebug("Fetching top {Limit} branches", limit);

                var branches = await _analyticsService.GetTopBranchesAsync(limit);
                
                return Json(new
                {
                    success = true,
                    data = branches,
                    count = branches.Count,
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch top branches");
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve branch performance data",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for system alerts
        /// </summary>
        /// <returns>JSON with current system alerts</returns>
        [HttpGet]
        public async Task<IActionResult> GetSystemAlerts()
        {
            try
            {
                _logger.LogDebug("Fetching system alerts");

                var alerts = await _analyticsService.GetSystemAlertsAsync();
                
                return Json(new
                {
                    success = true,
                    data = alerts,
                    count = alerts.Count,
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch system alerts");
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve system alerts",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for detailed analytics with custom date range
        /// </summary>
        /// <param name="fromDate">Start date (YYYY-MM-DD)</param>
        /// <param name="toDate">End date (YYYY-MM-DD)</param>
        /// <returns>JSON with detailed analytics data</returns>
        [HttpGet]
        public async Task<IActionResult> GetDetailedAnalytics(string fromDate, string toDate)
        {
            try
            {
                if (!DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
                {
                    return BadRequest(new { success = false, error = "Invalid date format. Use YYYY-MM-DD" });
                }

                if (from > to)
                {
                    return BadRequest(new { success = false, error = "From date must be before to date" });
                }

                if ((to - from).TotalDays > 365)
                {
                    return BadRequest(new { success = false, error = "Date range cannot exceed 365 days" });
                }

                _logger.LogDebug("Fetching detailed analytics from {FromDate} to {ToDate}", from, to);

                var analytics = await _analyticsService.GetDetailedAnalyticsAsync(from, to);
                
                return Json(new
                {
                    success = true,
                    data = analytics,
                    date_range = new { from, to },
                    generated_at = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch detailed analytics from {FromDate} to {ToDate}", fromDate, toDate);
                
                return Json(new
                {
                    success = false,
                    error = "Failed to retrieve detailed analytics",
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// API endpoint for system health score calculation
        /// </summary>
        /// <returns>JSON with system health score</returns>
        [HttpGet]
        public async Task<IActionResult> GetSystemHealthScore()
        {
            try
            {
                _logger.LogDebug("Calculating system health score");

                var healthScore = await _analyticsService.CalculateSystemHealthScoreAsync();
                
                var healthStatus = healthScore switch
                {
                    >= 90 => "Excellent",
                    >= 80 => "Good",
                    >= 70 => "Fair",
                    >= 60 => "Poor",
                    _ => "Critical"
                };

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        score = healthScore,
                        status = healthStatus,
                        calculated_at = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate system health score");
                
                return Json(new
                {
                    success = false,
                    error = "Failed to calculate system health score",
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}