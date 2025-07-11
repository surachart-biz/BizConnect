using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class HomeController : Controller
{
    private readonly IUserService _userService;

    public HomeController(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var users = await _userService.GetAllUsersAsync();
        var userList = users.ToList();

        var model = new DashboardViewModel
        {
            TotalUsers = userList.Count,
            ActiveUsers = userList.Count(u => u.IsActive),
            AdminUsers = userList.Count(u => u.Role == "Admin"),
            RegularUsers = userList.Count(u => u.Role == "User")
        };

        return View(model);
    }
}

public class DashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int AdminUsers { get; set; }
    public int RegularUsers { get; set; }
}
