using System.ComponentModel.DataAnnotations;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // GET: Admin/Users
    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetAllUsersAsync();
        return View(users);
    }

    // GET: Admin/Users/Create
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    // POST: Admin/Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Check if username already exists
            if (await _userService.UsernameExistsAsync(model.Username))
            {
                ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");
                return View(model);
            }

            // Create the user
            var user = await _userService.CreateUserAsync(model.Username, model.Password, model.Role);
            
            // Update IsActive status if needed
            if (!model.IsActive)
            {
                user.IsActive = model.IsActive;
                await _userService.UpdateUserAsync(user);
            }

            _logger.LogInformation("User {Username} was created by {AdminUser}", model.Username, User.Identity?.Name);
            TempData["SuccessMessage"] = $"User '{model.Username}' created successfully.";
            
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "An error occurred while creating the user. Please try again.");
            return View(model);
        }
    }

    // GET: Admin/Users/ResetPassword/5
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        var model = new ResetPasswordViewModel
        {
            UserId = id,
            Username = user.Username
        };

        return View(model);
    }

    // POST: Admin/Users/ResetPassword/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        // Get user info for display
        var user = await _userService.GetByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        model.Username = user.Username;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var success = await _userService.ResetPasswordAsync(model.UserId, model.NewPassword);
            if (success)
            {
                _logger.LogInformation("Password reset for user {Username} by {AdminUser}", model.Username, User.Identity?.Name);
                TempData["SuccessMessage"] = $"Password reset successfully for user '{model.Username}'.";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Failed to reset password. User not found.");
                return View(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {Username}", model.Username);
            ModelState.AddModelError(string.Empty, "An error occurred while resetting the password. Please try again.");
            return View(model);
        }
    }
}

// View Models
public class CreateUserViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Is Active")]
    public bool IsActive { get; set; } = true;
}

public class ResetPasswordViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
