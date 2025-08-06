using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.RegularExpressions;
using BizConnect.Services.Interfaces;

namespace BizConnect.Controllers
{
    [AllowAnonymous] // Allow culture switching without authentication
    [Route("[controller]/[action]")]
    public class CultureController : Controller
    {
        private readonly ILogger<CultureController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IRateLimitingService _rateLimitingService;
        
        // Enterprise-grade culture validation whitelist
        private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
        {
            "en-US",
            "th-TH"
        };
        
        // Regex for additional culture validation (RFC 4646 compliant)
        private static readonly Regex CultureValidationRegex = new(@"^[a-z]{2}-[A-Z]{2}$", RegexOptions.Compiled);

        public CultureController(ILogger<CultureController> logger, IWebHostEnvironment environment, IRateLimitingService rateLimitingService)
        {
            _logger = logger;
            _environment = environment;
            _rateLimitingService = rateLimitingService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCulture(string culture, string returnUrl)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var userId = User.Identity?.Name ?? "Anonymous";
            
            try
            {
                // SECURITY: Rate limiting check for culture switching
                var rateLimitStatus = await _rateLimitingService.CheckRateLimitAsync(clientIp, "culture-switch");
                if (rateLimitStatus.IsLocked)
                {
                    _logger.LogWarning("SECURITY: Rate limit exceeded for culture switching - IP: {ClientIp}, User: {UserId}, LockoutEnd: {LockoutEnd}", 
                        clientIp, userId, rateLimitStatus.LockoutEndTime);
                    
                    // Return to current page with rate limit message
                    TempData["ErrorMessage"] = "Too many language change requests. Please wait before trying again.";
                    return Redirect(Request.Headers["Referer"].ToString() ?? Url.Action("Index", "Home"));
                }

                // Comprehensive culture validation
                var validatedCulture = ValidateCulture(culture);
                if (validatedCulture != culture)
                {
                    _logger.LogWarning("SECURITY: Invalid culture attempt - Original: {OriginalCulture}, IP: {ClientIp}, User: {UserId}, UserAgent: {UserAgent}", 
                        culture, clientIp, userId, userAgent);
                    
                    // Record failed attempt for invalid culture
                    await _rateLimitingService.RecordFailedAttemptAsync(clientIp, "culture-switch", userId);
                }

                // Enhanced cookie security settings
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true, // SECURITY: Prevent XSS attacks by blocking JavaScript access
                    Secure = _environment.IsDevelopment() ? HttpContext.Request.IsHttps : true, // Always secure in production
                    SameSite = SameSiteMode.Strict, // SECURITY: Strict SameSite for CSRF protection
                    IsEssential = true,
                    Path = "/", // Explicit path setting
                    Domain = null // Let framework handle domain
                };

                // Set culture cookie with enterprise-grade security
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(validatedCulture)),
                    cookieOptions
                );

                _logger.LogInformation("SECURITY: Culture changed successfully - Culture: {Culture}, IP: {ClientIp}, User: {UserId}", 
                    validatedCulture, clientIp, userId);

                // Enhanced return URL validation
                var safeReturnUrl = ValidateReturnUrl(returnUrl, clientIp, userId);
                if (!string.IsNullOrEmpty(safeReturnUrl))
                {
                    return LocalRedirect(safeReturnUrl);
                }

                // Secure default redirect with role-based routing
                return GetSecureDefaultRedirect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SECURITY: Critical error in culture setting - Culture: {Culture}, IP: {ClientIp}, User: {UserId}, UserAgent: {UserAgent}", 
                    culture, clientIp, userId, userAgent);
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public IActionResult GetCurrentCulture()
        {
            var culture = HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name ?? "en-US";
            return Json(new { culture });
        }

        /// <summary>
        /// AJAX endpoint for culture switching with enterprise security
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCultureAjax(string culture)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userId = User.Identity?.Name ?? "Anonymous";
            
            try
            {
                // SECURITY: Rate limiting check for AJAX culture switching
                var rateLimitStatus = await _rateLimitingService.CheckRateLimitAsync(clientIp, "culture-switch");
                if (rateLimitStatus.IsLocked)
                {
                    _logger.LogWarning("SECURITY: Rate limit exceeded for AJAX culture switching - IP: {ClientIp}, User: {UserId}, LockoutEnd: {LockoutEnd}", 
                        clientIp, userId, rateLimitStatus.LockoutEndTime);
                    
                    return Json(new { 
                        success = false, 
                        message = "Too many requests. Please wait before trying again.", 
                        error = "RATE_LIMIT_EXCEEDED",
                        retryAfter = rateLimitStatus.TimeUntilUnlock?.TotalSeconds
                    });
                }

                // Comprehensive culture validation
                var validatedCulture = ValidateCulture(culture);
                if (validatedCulture != culture)
                {
                    _logger.LogWarning("SECURITY: Invalid AJAX culture attempt - Original: {OriginalCulture}, IP: {ClientIp}, User: {UserId}", 
                        culture, clientIp, userId);
                    
                    // Record failed attempt for invalid culture
                    await _rateLimitingService.RecordFailedAttemptAsync(clientIp, "culture-switch", userId);
                    return Json(new { success = false, message = "Invalid culture specified", error = "CULTURE_INVALID" });
                }

                // Enhanced cookie security settings (same as regular method)
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true, // SECURITY: Prevent XSS attacks
                    Secure = _environment.IsDevelopment() ? HttpContext.Request.IsHttps : true,
                    SameSite = SameSiteMode.Strict, // SECURITY: Strict SameSite for CSRF protection
                    IsEssential = true,
                    Path = "/",
                    Domain = null
                };

                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(validatedCulture)),
                    cookieOptions
                );

                _logger.LogInformation("SECURITY: Culture changed via AJAX - Culture: {Culture}, IP: {ClientIp}, User: {UserId}", 
                    validatedCulture, clientIp, userId);

                return Json(new { success = true, culture = validatedCulture, message = "Culture updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SECURITY: Critical error in AJAX culture setting - Culture: {Culture}, IP: {ClientIp}, User: {UserId}", 
                    culture, clientIp, userId);
                return Json(new { success = false, message = "Internal error occurred", error = "INTERNAL_ERROR" });
            }
        }
        
        /// <summary>
        /// Enterprise-grade culture validation with comprehensive security checks
        /// </summary>
        private string ValidateCulture(string culture)
        {
            // Handle null/empty inputs
            if (string.IsNullOrWhiteSpace(culture))
            {
                return "en-US"; // Secure default
            }

            // Sanitize input to prevent injection attacks
            culture = culture.Trim();
            
            // Length validation (prevent buffer overflow attempts)
            if (culture.Length > 10)
            {
                return "en-US";
            }

            // Regex validation for format compliance
            if (!CultureValidationRegex.IsMatch(culture))
            {
                return "en-US";
            }

            // Whitelist validation (most secure approach)
            if (!SupportedCultures.Contains(culture))
            {
                return "en-US";
            }

            // Additional .NET culture validation
            try
            {
                var cultureInfo = new CultureInfo(culture);
                return cultureInfo.Name;
            }
            catch (CultureNotFoundException)
            {
                return "en-US";
            }
        }

        /// <summary>
        /// Enhanced return URL validation to prevent open redirect vulnerabilities
        /// </summary>
        private string? ValidateReturnUrl(string? returnUrl, string clientIp, string userId)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return null;
            }

            try
            {
                // Basic length validation
                if (returnUrl.Length > 2048)
                {
                    _logger.LogWarning("SECURITY: Oversized return URL rejected - Length: {Length}, IP: {ClientIp}, User: {UserId}", 
                        returnUrl.Length, clientIp, userId);
                    return null;
                }

                // URL decode to check for double encoding attacks
                var decodedUrl = Uri.UnescapeDataString(returnUrl);
                
                // Check for multiple redirections or suspicious patterns
                if (decodedUrl.Contains("://") && !Url.IsLocalUrl(decodedUrl))
                {
                    _logger.LogWarning("SECURITY: External redirect attempt blocked - URL: {ReturnUrl}, IP: {ClientIp}, User: {UserId}", 
                        returnUrl, clientIp, userId);
                    return null;
                }

                // Comprehensive local URL validation
                if (!Url.IsLocalUrl(returnUrl))
                {
                    _logger.LogWarning("SECURITY: Non-local return URL rejected - URL: {ReturnUrl}, IP: {ClientIp}, User: {UserId}", 
                        returnUrl, clientIp, userId);
                    return null;
                }

                // Additional security: prevent navigation to sensitive admin endpoints from anonymous users
                if (!User.Identity?.IsAuthenticated == true && 
                    (returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || 
                     returnUrl.Contains("/admin/", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("SECURITY: Unauthorized admin redirect attempt - URL: {ReturnUrl}, IP: {ClientIp}, User: {UserId}", 
                        returnUrl, clientIp, userId);
                    return null;
                }

                return returnUrl;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SECURITY: Return URL validation failed - URL: {ReturnUrl}, IP: {ClientIp}, User: {UserId}", 
                    returnUrl, clientIp, userId);
                return null;
            }
        }

        /// <summary>
        /// Secure default redirect based on user role and authentication status
        /// </summary>
        private IActionResult GetSecureDefaultRedirect()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Home", new { area = "Admin" });
                }
                if (User.IsInRole("Employee"))
                {
                    return RedirectToAction("Dashboard", "Home", new { area = "Admin" });
                }
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Index", "Home");
        }
    }
}