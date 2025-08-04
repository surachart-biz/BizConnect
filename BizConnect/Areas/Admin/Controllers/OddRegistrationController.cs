using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOrEmployee")]
public class OddRegistrationController : Controller
{
    private readonly IRegistrationQueryService _registrationQuery;
    private readonly IRegistrationManagementService _registrationManagement;
    private readonly ILogger<OddRegistrationController> _logger;

    public OddRegistrationController(IRegistrationQueryService registrationQuery, IRegistrationManagementService registrationManagement, ILogger<OddRegistrationController> logger)
    {
        _registrationQuery = registrationQuery;
        _registrationManagement = registrationManagement;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string status = "", string search = "")
    {
        ViewBag.BreadcrumbSection = "ODD Management";
        
        var result = await _registrationQuery.GetPagedAsync(page, pageSize, status, search);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to load registrations: {ErrorMessage}", result.ErrorMessage);
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Unable to load registrations";
            
            // Return empty model on error
            var emptyModel = new OddRegistrationListViewModel
            {
                Registrations = new List<KbankOddRegistration>(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = 0,
                TotalRecords = 0,
                StatusFilter = status,
                SearchQuery = search,
                HasPreviousPage = false,
                HasNextPage = false
            };
            return View(emptyModel);
        }

        var pagedData = result.Data!;
        var model = new OddRegistrationListViewModel
        {
            Registrations = pagedData.Items.ToList(),
            CurrentPage = pagedData.CurrentPage,
            PageSize = pagedData.PageSize,
            TotalPages = pagedData.TotalPages,
            TotalRecords = pagedData.TotalCount,
            StatusFilter = status,
            SearchQuery = search,
            HasPreviousPage = pagedData.HasPreviousPage,
            HasNextPage = pagedData.HasNextPage
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewBag.BreadcrumbSection = "ODD Management";
        
        var result = await _registrationQuery.GetByIdAsync(id);

        if (!result.IsSuccess || result.Data == null)
        {
            _logger.LogWarning("Registration not found: ID {Id}, Error: {ErrorMessage}", id, result.ErrorMessage);
            return NotFound();
        }

        return View(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        // For now, we don't have a direct update by ID method in the new service
        // This would need to be implemented or handled differently
        _logger.LogWarning("UpdateStatus called but not implemented with new services: ID {Id}, Status {Status}", id, status);
        
        return Json(new { success = false, message = "Status update functionality needs to be implemented with new service architecture" });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
    {
        // Delete functionality would need to be implemented in the new service architecture
        _logger.LogWarning("Delete called but not implemented with new services: ID {Id}", id);
        
        return Json(new { success = false, message = "Delete functionality needs to be implemented with new service architecture" });
    }

    public async Task<IActionResult> Export(string format = "excel")
    {
        var result = await _registrationQuery.GetForExportAsync();
        
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to export registrations: {ErrorMessage}", result.ErrorMessage);
            return Json(new { 
                success = false, 
                message = result.ErrorMessage ?? "Export failed" 
            });
        }

        var registrations = result.Data!.ToList();
        
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