using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IUserService userService, ILogger<AccountController> logger)
    {
        _userService = userService;
        _logger = logger;
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

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [Route("Account/Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var user = await _userService.AuthenticateAsync(model.Username, model.Password);
            
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                _logger.LogInformation("User {Username} logged in.", user.Username);

                // Redirect based on role
                if (user.Role == "Admin")
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
                _logger.LogWarning("Failed login attempt for username: {Username}", model.Username);
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password. Please check your credentials and try again.");
        }

        return View(model);
    }

    [HttpGet]
    [Route("Account/Logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var username = User.Identity?.Name;
            _logger.LogInformation("Starting logout process for user {Username}", username);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            _logger.LogInformation("User {Username} logged out successfully.", username);

            // Use absolute URL to ensure proper redirect
            return Redirect("/Account/Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout process");
            return Redirect("/Account/Login");
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        var username = User.Identity?.Name;

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("User {Username} logged out.", username);

        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}

public class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}
