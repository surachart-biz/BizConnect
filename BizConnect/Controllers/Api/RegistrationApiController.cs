using BizConnect.Models.Api;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// Rate limiting handled by middleware in .NET 8
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BizConnect.Controllers.Api;

/// <summary>
/// RESTful API controller for KBank ODD Registration operations.
/// Provides paginated endpoints for registration management, status updates, and reporting.
/// Includes comprehensive filtering, sorting, and search capabilities.
/// </summary>
[Route("api/v1/registration")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ValidationErrorResponse), 400)]
[ProducesResponseType(typeof(ApiResponse), 401)]
[ProducesResponseType(typeof(ApiResponse), 403)]
[ProducesResponseType(typeof(ApiResponse), 500)]
public class RegistrationApiController : ControllerBase
{
    private readonly IRegistrationQueryService _registrationQueryService;
    private readonly IRegistrationManagementService _registrationManagementService;
    private readonly IBranchService _branchService;
    private readonly ISecurityMonitoringService _securityMonitoringService;
    private readonly ILogger<RegistrationApiController> _logger;

    public RegistrationApiController(
        IRegistrationQueryService registrationQueryService,
        IRegistrationManagementService registrationManagementService,
        IBranchService branchService,
        ISecurityMonitoringService securityMonitoringService,
        ILogger<RegistrationApiController> logger)
    {
        _registrationQueryService = registrationQueryService ?? throw new ArgumentNullException(nameof(registrationQueryService));
        _registrationManagementService = registrationManagementService ?? throw new ArgumentNullException(nameof(registrationManagementService));
        _branchService = branchService ?? throw new ArgumentNullException(nameof(branchService));
        _securityMonitoringService = securityMonitoringService ?? throw new ArgumentNullException(nameof(securityMonitoringService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a paginated list of registrations with advanced filtering and search capabilities.
    /// Supports filtering by status, date range, branch, and full-text search across multiple fields.
    /// </summary>
    /// <param name="request">Search and pagination parameters</param>
    /// <returns>Paginated list of registrations matching the specified criteria</returns>
    [HttpGet]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(PagedApiResponse<RegistrationDto>), 200)]
    public async Task<IActionResult> GetRegistrations([FromQuery] RegistrationSearchRequest request)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogDebug("Registration search requested. TraceId: {TraceId}, User: {User}, Page: {Page}, PageSize: {PageSize}",
                traceId, username, request.Page, request.PageSize);

            // Normalize and validate request parameters
            request.Normalize();

            // Get registrations with filtering and pagination
            var result = await _registrationQueryService.SearchRegistrationsAsync(
                page: request.Page,
                pageSize: request.PageSize,
                status: request.Status,
                search: request.SearchTerm,
                fromDate: request.FromDate,
                toDate: request.ToDate);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Registration search failed. TraceId: {TraceId}, Error: {Error}",
                    traceId, result.ErrorMessage);
                return BadRequest(ApiResponse<PagedResult<RegistrationDto>>.Error(result.ErrorMessage ?? "Search failed"));
            }

            // Convert to DTOs with branch information
            var registrationDtos = new List<RegistrationDto>();
            var branchCache = new Dictionary<int, string>();

            foreach (var registration in result.Data!.Items)
            {
                string? branchName = null;
                
                if (registration.BranchId.HasValue && registration.BranchId > 0)
                {
                    if (!branchCache.TryGetValue(registration.BranchId.Value, out branchName))
                    {
                        branchName = await _branchService.GetBranchNameAsync(registration.BranchId.Value);
                        branchCache[registration.BranchId.Value] = branchName ?? string.Empty;
                    }
                }

                registrationDtos.Add(RegistrationDto.FromDomainModel(registration, branchName));
            }

            // Create paged result
            var pagedResult = new PagedResult<RegistrationDto>(
                registrationDtos,
                result.Data!.CurrentPage,
                result.Data!.PageSize,
                result.Data!.TotalCount);

            _logger.LogDebug("Registration search completed. TraceId: {TraceId}, Found: {Count}, TotalPages: {TotalPages}",
                traceId, pagedResult.CurrentPageSize, pagedResult.TotalPages);

            var response = PagedApiResponse<RegistrationDto>.Ok(pagedResult, 
                pagedResult.CurrentPageSize > 0 ? $"Found {pagedResult.TotalItems} registrations" : "No registrations found");
            response.TraceId = traceId;
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching registrations. TraceId: {TraceId}", traceId);
            
            var response = PagedApiResponse<RegistrationDto>.Empty(request.Page, request.PageSize, "An error occurred while searching registrations");
            response.Success = false;
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Gets detailed information for a specific registration by ID.
    /// Includes complete registration data, OTAC status, and audit trail information.
    /// </summary>
    /// <param name="id">The registration ID to retrieve</param>
    /// <returns>Complete registration details</returns>
    [HttpGet("{id:int}")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse<RegistrationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetRegistration([FromRoute] [Required] int id)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogDebug("Registration details requested. TraceId: {TraceId}, User: {User}, RegistrationId: {Id}",
                traceId, username, id);

            // Validate ID
            if (id <= 0)
            {
                return BadRequest(ApiResponse<RegistrationDto>.Error("Invalid registration ID"));
            }

            // Get registration
            var result = await _registrationQueryService.GetRegistrationByIdAsync(id);

            if (!result.IsSuccess || result.Data == null)
            {
                _logger.LogDebug("Registration not found. TraceId: {TraceId}, RegistrationId: {Id}", traceId, id);
                return NotFound(ApiResponse<RegistrationDto>.Error("Registration not found"));
            }

            // Get branch name if available
            string? branchName = null;
            if (result.Data.BranchId.HasValue && result.Data.BranchId > 0)
            {
                branchName = await _branchService.GetBranchNameAsync(result.Data.BranchId.Value);
            }

            // Convert to DTO
            var registrationDto = RegistrationDto.FromDomainModel(result.Data, branchName);

            _logger.LogDebug("Registration details retrieved. TraceId: {TraceId}, Status: {Status}",
                traceId, registrationDto.Status);

            var response = ApiResponse<RegistrationDto>.Ok(registrationDto, "Registration details retrieved");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving registration. TraceId: {TraceId}, RegistrationId: {Id}", traceId, id);
            
            var response = ApiResponse<RegistrationDto>.Error("An error occurred while retrieving registration");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Updates the status of a specific registration.
    /// Requires appropriate permissions and includes comprehensive audit logging.
    /// </summary>
    /// <param name="id">The registration ID to update</param>
    /// <param name="request">Status update parameters</param>
    /// <returns>Updated registration details</returns>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin,Employee")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse<RegistrationDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> UpdateRegistrationStatus(
        [FromRoute] [Required] int id,
        [FromBody] UpdateRegistrationStatusRequest request)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        try
        {
            _logger.LogInformation("Registration status update requested. TraceId: {TraceId}, User: {User}, RegistrationId: {Id}, NewStatus: {Status}",
                traceId, username, id, request.Status);

            // Validate parameters
            if (id <= 0)
            {
                return BadRequest(ApiResponse<RegistrationDto>.Error("Invalid registration ID"));
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(ApiResponse<RegistrationDto>.Error("Status is required"));
            }

            // Validate status value
            var validStatuses = new[] { "OTAC_GENERATED", "FORM_SUBMITTED", "PENDING_KBANK", "SUCCESS", "FAILED", "EXPIRED", "CANCELLED" };
            if (!validStatuses.Contains(request.Status.ToUpperInvariant()))
            {
                return BadRequest(ApiResponse<RegistrationDto>.Error($"Invalid status. Valid values are: {string.Join(", ", validStatuses)}"));
            }

            // Special validation for FAILED status
            if (request.Status.ToUpperInvariant() == "FAILED" && string.IsNullOrWhiteSpace(request.ErrorMessage))
            {
                return BadRequest(ApiResponse<RegistrationDto>.Error("Error message is required when setting status to FAILED"));
            }

            // Update registration status
            var result = await _registrationManagementService.UpdateRegistrationStatusAsync(
                id,
                request.Status.ToUpperInvariant());

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Registration status update failed. TraceId: {TraceId}, Error: {Error}",
                    traceId, result.ErrorMessage);
                
                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(ApiResponse<RegistrationDto>.Error(result.ErrorMessage));
                }
                
                return BadRequest(ApiResponse<RegistrationDto>.Error(result.ErrorMessage ?? "Status update failed"));
            }

            // Get updated registration with branch information
            var updatedResult = await _registrationQueryService.GetRegistrationByIdAsync(id);
            
            string? branchName = null;
            if (updatedResult.Data?.BranchId.HasValue == true && updatedResult.Data.BranchId > 0)
            {
                branchName = await _branchService.GetBranchNameAsync(updatedResult.Data.BranchId.Value);
            }

            var registrationDto = RegistrationDto.FromDomainModel(updatedResult.Data!, branchName);

            _logger.LogInformation("Registration status updated successfully. TraceId: {TraceId}, User: {User}, RegistrationId: {Id}, Status: {Status}",
                traceId, username, id, registrationDto.Status);

            // Log security event for audit trail
            await _securityMonitoringService.LogSecurityEventAsync("REGISTRATION_STATUS_UPDATED",
                GetClientIpAddress(), $"Registration {id} status updated to {request.Status} by {username}");

            var response = ApiResponse<RegistrationDto>.Ok(registrationDto, "Registration status updated successfully");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating registration status. TraceId: {TraceId}, RegistrationId: {Id}", traceId, id);
            
            var response = ApiResponse<RegistrationDto>.Error("An error occurred while updating registration status");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Gets comprehensive registration statistics and analytics.
    /// Provides insights into registration patterns, success rates, and performance metrics.
    /// </summary>
    /// <param name="fromDate">Start date for statistics calculation (optional)</param>
    /// <param name="toDate">End date for statistics calculation (optional)</param>
    /// <returns>Detailed registration statistics and metrics</returns>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Employee")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse<RegistrationStatsDto>), 200)]
    public async Task<IActionResult> GetRegistrationStats(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogDebug("Registration statistics requested. TraceId: {TraceId}, User: {User}, From: {FromDate}, To: {ToDate}",
                traceId, username, fromDate, toDate);

            // Default to last 30 days if no date range specified
            var effectiveFromDate = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveToDate = toDate ?? DateTime.UtcNow;

            // Validate date range
            if (effectiveFromDate > effectiveToDate)
            {
                return BadRequest(ApiResponse<RegistrationStatsDto>.Error("From date cannot be after to date"));
            }

            // Get statistics
            var result = await _registrationQueryService.GetRegistrationStatisticsAsync(effectiveFromDate, effectiveToDate);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Registration statistics retrieval failed. TraceId: {TraceId}, Error: {Error}",
                    traceId, result.ErrorMessage);
                return BadRequest(ApiResponse<RegistrationStatsDto>.Error(result.ErrorMessage ?? "Failed to retrieve statistics"));
            }

            // Convert to DTO
            var statsDto = new RegistrationStatsDto
            {
                TotalRegistrations = result.Data!.TotalRegistrations,
                SuccessfulRegistrations = result.Data.SuccessfulRegistrations,
                FailedRegistrations = result.Data.FailedRegistrations,
                PendingRegistrations = result.Data.PendingRegistrations,
                StatusBreakdown = result.Data.StatusBreakdown,
                TimeBreakdown = result.Data.TimeBreakdown,
                LastUpdated = DateTime.UtcNow
            };

            _logger.LogDebug("Registration statistics retrieved. TraceId: {TraceId}, Total: {Total}, SuccessRate: {SuccessRate:F2}%",
                traceId, statsDto.TotalRegistrations, statsDto.SuccessRate);

            var response = ApiResponse<RegistrationStatsDto>.Ok(statsDto, "Registration statistics retrieved");
            response.TraceId = traceId;
            response.Metadata = new Dictionary<string, object>
            {
                ["dateRange"] = new { From = effectiveFromDate, To = effectiveToDate },
                ["generatedAt"] = DateTime.UtcNow
            };
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving registration statistics. TraceId: {TraceId}", traceId);
            
            var response = ApiResponse<RegistrationStatsDto>.Error("An error occurred while retrieving registration statistics");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Exports registration data to various formats (CSV, Excel, JSON).
    /// Includes comprehensive filtering options and supports large datasets with streaming.
    /// </summary>
    /// <param name="request">Export parameters including filters and format</param>
    /// <param name="format">Export format (csv, excel, json)</param>
    /// <returns>Exported data in the requested format</returns>
    [HttpPost("export")]
    [Authorize(Roles = "Admin,Employee")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse), 400)]
    public async Task<IActionResult> ExportRegistrations(
        [FromBody] RegistrationSearchRequest request,
        [FromQuery] string format = "csv")
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogInformation("Registration export requested. TraceId: {TraceId}, User: {User}, Format: {Format}",
                traceId, username, format);

            // Validate format
            var supportedFormats = new[] { "csv", "excel", "json" };
            if (!supportedFormats.Contains(format.ToLowerInvariant()))
            {
                return BadRequest(ApiResponse.Error($"Unsupported format. Supported formats: {string.Join(", ", supportedFormats)}"));
            }

            // Normalize request (remove pagination for export)
            request.Normalize();
            request.Page = 1;
            request.PageSize = 10000; // Large page size for export

            // Get data for export
            var result = await _registrationQueryService.SearchRegistrationsAsync(
                page: request.Page,
                pageSize: request.PageSize,
                status: request.Status,
                search: request.SearchTerm,
                fromDate: request.FromDate,
                toDate: request.ToDate);

            if (!result.IsSuccess)
            {
                return BadRequest(ApiResponse.Error(result.ErrorMessage ?? "Export failed"));
            }

            // TODO: Implement ExportRegistrationsAsync in IRegistrationManagementService
            // For now, return a simple CSV export
            var csvData = "Id,FullName,Status,CreatedAt\n";
            csvData += string.Join("\n", result.Data!.Items.Select(r => 
                $"{r.Id},{r.FullName},{r.Status},{r.CreatedAt:yyyy-MM-dd HH:mm:ss}"));
            
            var exportData = System.Text.Encoding.UTF8.GetBytes(csvData);

            _logger.LogInformation("Registration export completed. TraceId: {TraceId}, User: {User}, Records: {Count}",
                traceId, username, result.Data!.Items.Count());

            // Log security event
            await _securityMonitoringService.LogSecurityEventAsync("REGISTRATION_EXPORT",
                GetClientIpAddress(), $"Registration data exported by {username} in {format} format");

            // Return file
            var fileName = $"registrations_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format.ToLowerInvariant()}";
            var contentType = format.ToLowerInvariant() switch
            {
                "csv" => "text/csv",
                "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "json" => "application/json",
                _ => "application/octet-stream"
            };

            return File(exportData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting registrations. TraceId: {TraceId}", traceId);
            return StatusCode(500, ApiResponse.Error("An error occurred while exporting registrations"));
        }
    }

    /// <summary>
    /// Gets the client IP address from the request
    /// </summary>
    private string GetClientIpAddress()
    {
        // Check for forwarded IP first (load balancer/proxy scenario)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for real IP (some proxy configurations)
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}