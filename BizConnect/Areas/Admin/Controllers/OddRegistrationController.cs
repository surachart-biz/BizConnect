using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOrEmployee")]
public class OddRegistrationController : Controller
{
    private readonly IOddRegistrationService _oddRegistrationService;

    public OddRegistrationController(IOddRegistrationService oddRegistrationService)
    {
        _oddRegistrationService = oddRegistrationService;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string status = "", string search = "")
    {
        ViewBag.BreadcrumbSection = "ODD Management";
        
        var pagedResult = await _oddRegistrationService.GetRegistrationsAsync(page, pageSize, status, search);

        var model = new OddRegistrationListViewModel
        {
            Registrations = pagedResult.Registrations,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalPages = pagedResult.TotalPages,
            TotalRecords = pagedResult.TotalRecords,
            StatusFilter = pagedResult.StatusFilter,
            SearchQuery = pagedResult.SearchQuery,
            HasPreviousPage = pagedResult.HasPreviousPage,
            HasNextPage = pagedResult.HasNextPage
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewBag.BreadcrumbSection = "ODD Management";
        
        var registration = await _oddRegistrationService.GetRegistrationByIdAsync(id);

        if (registration == null)
        {
            return NotFound();
        }

        return View(registration);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var success = await _oddRegistrationService.UpdateRegistrationStatusAsync(id, status);

        if (!success)
        {
            return Json(new { success = false, message = "Registration not found" });
        }

        return Json(new { success = true, message = "Status updated successfully" });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _oddRegistrationService.DeleteRegistrationAsync(id);

        if (!success)
        {
            return Json(new { success = false, message = "Registration not found" });
        }

        return Json(new { success = true, message = "Registration deleted successfully" });
    }

    public async Task<IActionResult> Export(string format = "excel")
    {
        var registrations = await _oddRegistrationService.GetAllRegistrationsForExportAsync();

        // In a real implementation, you would generate Excel/CSV files here
        // For now, return a placeholder response
        return Json(new { 
            success = true, 
            message = $"Export functionality for {format} format will be implemented", 
            count = registrations.Count 
        });
    }
}

public class OddRegistrationListViewModel
{
    public List<KbankOddRegistration> Registrations { get; set; } = new List<KbankOddRegistration>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public string StatusFilter { get; set; } = "";
    public string SearchQuery { get; set; } = "";
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    
    public int StartRecord => (CurrentPage - 1) * PageSize + 1;
    public int EndRecord => Math.Min(CurrentPage * PageSize, TotalRecords);
}