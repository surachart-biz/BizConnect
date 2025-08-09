using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace BizConnect.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AccountController> _logger;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ISecurityAuditService _securityAuditService;

    public AccountController(
        IUserService userService, 
        ILogger<AccountController> logger,
        IRateLimitingService rateLimitingService,
        ISecurityAuditService securityAuditService)
    {
        _userService = userService;
        _logger = logger;
        _rateLimitingService = rateLimitingService;
        _securityAuditService = securityAuditService;
    }

    [HttpGet]
    [Route("Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // User is already logged in, redirect based on role
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Home", new { area = "Admin" });
            }
            else
            {
                return RedirectToAction("Register", "KBank");
            }
        }

        // Clear any existing model state errors for fresh login page
        ModelState.Clear();

        // Mark this as a GET request (not a POST back)
        ViewData["IsPostBack"] = false;
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [Route("Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["IsPostBack"] = true;

        var clientIp = GetClientIpAddress();
        var userAgent = Request.Headers["User-Agent"].ToString();

        // Check rate limiting
        var rateLimitStatus = await _rateLimitingService.CheckRateLimitAsync(clientIp, "login");
        if (rateLimitStatus.IsLocked)
        {
            await _securityAuditService.LogSuspiciousActivityAsync(
                "Login attempt from locked IP", 
                $"IP {clientIp} attempted login while locked", 
                clientIp);
            
            ModelState.AddModelError(string.Empty, rateLimitStatus.Message);
            return View(model);
        }

        if (ModelState.IsValid)
        {
            // Check user-specific lockout
            var userLockout = await _rateLimitingService.CheckUserLockoutAsync(model.Username);
            if (userLockout.IsLocked)
            {
                await _securityAuditService.LogFailedLoginAsync(
                    model.Username, 
                    clientIp, 
                    "Account locked due to multiple failed attempts", 
                    userAgent);
                
                ModelState.AddModelError(string.Empty, "This account has been temporarily locked due to multiple failed login attempts.");
                return View(model);
            }

            var user = await _userService.AuthenticateAsync(model.Username, model.Password);
            
            if (user != null && user.IsActive)
            {
                // Clear failed attempts on successful login
                await _rateLimitingService.ClearFailedAttemptsAsync(clientIp, "login");
                await _rateLimitingService.ClearUserLockoutAsync(model.Username);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("ip_address", clientIp),
                    new Claim("login_time", DateTimeOffset.UtcNow.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddMinutes(30),
                    AllowRefresh = true
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                // Log successful authentication
                await _securityAuditService.LogSuccessfulLoginAsync(user.Username, clientIp, userAgent);
                _logger.LogInformation("User {Username} logged in from IP {IP}.", user.Username, clientIp);

                // Store session information
                HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString());
                HttpContext.Session.SetString("UserRole", user.Role);

                // Redirect based on role
                if (user.Role == "Admin")
                {
                    return LocalRedirect(returnUrl ?? Url.Action("Dashboard", "Home", new { area = "Admin" })!);
                }
                else if (user.Role == "Employee")
                {
                    return LocalRedirect(returnUrl ?? Url.Action("Dashboard", "Home", new { area = "Admin" })!);
                }
                else
                {
                    // Redirect regular users to KBank ODD registration form
                    return LocalRedirect(returnUrl ?? Url.Action("Register", "KBank", new { area = "" })!);
                }
            }
            else
            {
                // Record failed attempt
                var reason = user == null ? "Invalid credentials" : "Account is inactive";
                await _rateLimitingService.RecordFailedAttemptAsync(clientIp, "login", model.Username);
                await _securityAuditService.LogFailedLoginAsync(model.Username, clientIp, reason, userAgent);
                
                _logger.LogWarning("Failed login attempt for username: {Username} from IP {IP} - {Reason}", 
                    model.Username, clientIp, reason);
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password, or account is inactive. Please check your credentials and try again.");
        }

        return View(model);
    }

    [HttpGet]
    [Route("Account/Logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var username = User.Identity?.Name;
            var clientIp = GetClientIpAddress();
            
            _logger.LogInformation("Starting logout process for user {Username} from IP {IP}", username, clientIp);

            // Clear session data
            HttpContext.Session.Clear();

            // Sign out and clear authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear any additional cookies
            Response.Cookies.Delete("BizConnect.Auth");
            Response.Cookies.Delete("BizConnect.Session");
            Response.Cookies.Delete("BizConnect.Antiforgery");

            // Log the logout event
            await _securityAuditService.LogLogoutAsync(username ?? "Unknown", clientIp);
            _logger.LogInformation("User {Username} logged out successfully from IP {IP}.", username, clientIp);

            // Redirect to landing page after successful logout
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout process");
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        var username = User.Identity?.Name;
        var clientIp = GetClientIpAddress();

        // Clear session data
        HttpContext.Session.Clear();

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Clear cookies
        Response.Cookies.Delete("BizConnect.Auth");
        Response.Cookies.Delete("BizConnect.Session");
        Response.Cookies.Delete("BizConnect.Antiforgery");

        // Log the logout event
        await _securityAuditService.LogLogoutAsync(username ?? "Unknown", clientIp);
        _logger.LogInformation("User {Username} logged out from IP {IP}.", username, clientIp);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    #region Helper Methods

    private string GetClientIpAddress()
    {
        // Check for forwarded headers first (load balancer/proxy scenarios)
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to direct connection IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    #endregion
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(255, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me for 7 days?")]
    public bool RememberMe { get; set; }
}
