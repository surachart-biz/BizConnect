using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Controllers
{
    [AllowAnonymous] // Allow culture switching without authentication
    public class CultureController : Controller
    {
        private readonly ILogger<CultureController> _logger;

        public CultureController(ILogger<CultureController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCulture(string culture, string returnUrl)
        {
            try
            {
                // Validate culture
                var supportedCultures = new[] { "en-US", "th-TH" };
                if (!supportedCultures.Contains(culture))
                {
                    _logger.LogWarning("Unsupported culture requested: {Culture}", culture);
                    culture = "en-US"; // Default to English
                }

                // Set culture cookie
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = false, // Allow JavaScript access for enhanced UX
                        Secure = !HttpContext.Request.IsHttps ? false : true,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true
                    }
                );

                _logger.LogInformation("Culture changed to {Culture} for user {User}", 
                    culture, User.Identity?.Name ?? "Anonymous");

                // Validate and sanitize return URL
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                // Default redirect based on user authentication
                if (User.Identity?.IsAuthenticated == true)
                {
                    if (User.IsInRole("Admin"))
                    {
                        return RedirectToAction("Dashboard", "Home", new { area = "Admin" });
                    }
                    return RedirectToAction("Index", "Home");
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting culture to {Culture}", culture);
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
        /// Helper method for AJAX culture switching (for future enhancement)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCultureAjax(string culture)
        {
            try
            {
                // Validate culture
                var supportedCultures = new[] { "en-US", "th-TH" };
                if (!supportedCultures.Contains(culture))
                {
                    return Json(new { success = false, message = "Unsupported culture" });
                }

                // Set culture cookie
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = false,
                        Secure = !HttpContext.Request.IsHttps ? false : true,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true
                    }
                );

                _logger.LogInformation("Culture changed to {Culture} via AJAX for user {User}", 
                    culture, User.Identity?.Name ?? "Anonymous");

                return Json(new { success = true, culture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting culture to {Culture} via AJAX", culture);
                return Json(new { success = false, message = "Error setting culture" });
            }
        }
    }
}