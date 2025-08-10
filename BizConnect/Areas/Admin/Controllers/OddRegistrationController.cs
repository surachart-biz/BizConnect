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

    /// <summary>
    /// Real-time API endpoint for getting latest data updates without full page reload
    /// Uses cursor-based pagination from the database for optimal performance
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUpdates(string? lastCursor = null, int pageSize = 10, string status = "", string search = "")
    {
        try
        {
            _logger.LogInformation("GetUpdates called - lastCursor: {lastCursor}, pageSize: {pageSize}, status: {status}, search: {search}", 
                lastCursor, pageSize, status, search);

            // Always get the first page with current filters for real-time updates
            var result = await _registrationQuery.GetPagedAsync(1, pageSize, status, search);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to get real-time updates: {ErrorMessage}", result.ErrorMessage);
                return Json(new { 
                    success = false, 
                    message = result.ErrorMessage ?? "Failed to get updates",
                    hasNewData = false,
                    data = new object[0],
                    totalRecords = 0,
                    nextCursor = (string?)null
                });
            }

            var pagedData = result.Data!;
            var items = pagedData.Items.ToList();

            _logger.LogInformation("Retrieved {count} items from database", items.Count);

            // For real-time updates, determine if there's new data
            // If no lastCursor, it's the first load so always return data
            bool hasNewData = true;
            
            // Filter new items based on cursor if provided
            if (!string.IsNullOrEmpty(lastCursor))
            {
                try 
                {
                    var cursorDate = DateTime.Parse(lastCursor);
                    // Only include items updated after the cursor timestamp
                    var filteredItems = items.Where(r => 
                        r.UpdatedAt > cursorDate || 
                        (r.UpdatedAt == null && r.CreatedAt > cursorDate)
                    ).ToList();
                    
                    hasNewData = filteredItems.Any();
                    _logger.LogInformation("Filtered {filteredCount} new items since cursor {cursor}", 
                        filteredItems.Count, lastCursor);
                    
                    // Use filtered items for response when checking for updates
                    // But keep original items for stats calculation
                    if (hasNewData)
                    {
                        items = filteredItems;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse cursor {cursor}, treating as first load", lastCursor);
                    hasNewData = true; // Fallback to returning all data
                }
            }
            
            // Generate next cursor based on the most recent item's timestamp
            string? nextCursor = items.Any() 
                ? items.OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                       .First()
                       .UpdatedAt?.ToString("O") ?? items.First().CreatedAt.ToString("O")
                : lastCursor; // Keep the same cursor if no new items

            // Transform data for JSON response with minimal fields for performance
            var responseData = items.Select(r => new
            {
                id = r.Id,
                externalReference = r.ExternalReference,
                fullName = r.FullName,
                mobileNo = r.MobileNo,
                accountNo = r.AccountNo,
                idType = r.IdType,
                idValue = r.IdValue,
                status = r.Status,
                otacCode = r.OtacCode,
                otacState = r.OtacState,
                otacExpiresAt = r.OtacExpiresAt?.ToString("O"),
                returnCode = r.ReturnCode,
                regId = r.RegId,
                espaId = r.EspaId,
                attemptCount = r.AttemptCount,
                createdAt = r.CreatedAt.ToString("O"),
                updatedAt = r.UpdatedAt?.ToString("O"),
                generatedByUserId = r.GeneratedByUserId,
                branchId = r.BranchId,
                branch = r.Branch != null ? new
                {
                    id = r.Branch.BranchId,
                    nameEn = r.Branch.NameEn,
                    nameTh = r.Branch.NameTh,
                    code = r.Branch.Code,
                    addressEn = r.Branch.AddressEn,
                    addressTh = r.Branch.AddressTh
                } : null,
                generatedByUser = r.GeneratedByUser != null ? new
                {
                    id = r.GeneratedByUser.Id,
                    username = r.GeneratedByUser.Username,
                    role = r.GeneratedByUser.Role
                } : null
            }).ToList();

            // Get stats from the full dataset (not filtered by cursor)
            var fullResult = await _registrationQuery.GetPagedAsync(1, 100, status, search);
            var allItems = fullResult.IsSuccess ? fullResult.Data!.Items.ToList() : new List<KbankOddRegistration>();

            var response = new
            {
                success = true,
                hasNewData = hasNewData,
                data = responseData,
                totalRecords = pagedData.TotalCount,
                currentPage = pagedData.CurrentPage,
                totalPages = pagedData.TotalPages,
                nextCursor = nextCursor,
                timestamp = DateTime.UtcNow.ToString("O"),
                // Statistics for dashboard updates
                stats = new
                {
                    pending = allItems.Count(r => r.Status == "Pending"),
                    completed = allItems.Count(r => r.Status == "Completed" || r.Status == "Success"),
                    failed = allItems.Count(r => r.Status == "Failed")
                }
            };

            _logger.LogInformation("GetUpdates response - hasNewData: {hasNewData}, dataCount: {dataCount}, nextCursor: {nextCursor}", 
                hasNewData, responseData.Count, nextCursor);

            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUpdates API endpoint");
            return Json(new { 
                success = false, 
                message = "An error occurred while fetching updates",
                hasNewData = false,
                data = new object[0]
            });
        }
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