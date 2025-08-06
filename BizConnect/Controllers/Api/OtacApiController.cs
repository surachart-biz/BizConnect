using BizConnect.Services.DTOs;
using BizConnect.Models.Api;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// Rate limiting will be handled by middleware in .NET 8
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BizConnect.Controllers.Api;

/// <summary>
/// RESTful API controller for OTAC (One-Time Access Code) operations.
/// Provides endpoints for generating, validating, retrieving, and managing OTAC codes.
/// Includes comprehensive rate limiting, authentication, and security monitoring.
/// </summary>
[Route("api/v1/otac")]
[ApiController]
[Authorize]
[ProducesResponseType(typeof(ValidationErrorResponse), 400)]
[ProducesResponseType(typeof(ApiResponse), 401)]
[ProducesResponseType(typeof(ApiResponse), 403)]
[ProducesResponseType(typeof(ApiResponse), 500)]
public class OtacApiController : ControllerBase
{
    private readonly IOtacManagementService _otacManagementService;
    private readonly ISecurityMonitoringService _securityMonitoringService;
    private readonly ILogger<OtacApiController> _logger;

    public OtacApiController(
        IOtacManagementService otacManagementService,
        ISecurityMonitoringService securityMonitoringService,
        ILogger<OtacApiController> logger)
    {
        _otacManagementService = otacManagementService ?? throw new ArgumentNullException(nameof(otacManagementService));
        _securityMonitoringService = securityMonitoringService ?? throw new ArgumentNullException(nameof(securityMonitoringService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a new OTAC code for registration purposes.
    /// Rate limited to prevent abuse and includes comprehensive security monitoring.
    /// </summary>
    /// <param name="request">OTAC generation request parameters</param>
    /// <returns>Generated OTAC details including code, expiration, and validation rules</returns>
    [HttpPost("generate")]
    // Rate limiting configured in Program.cs middleware
    [ProducesResponseType(typeof(ApiResponse<OtacDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 429)]
    public async Task<IActionResult> GenerateOtac([FromBody] GenerateOtacRequest request)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("OTAC generation requested. TraceId: {TraceId}, RegistrationId: {RegistrationId}, IP: {IP}",
                traceId, request.RegistrationId, GetClientIpAddress());

            // Validate request
            if (request == null)
            {
                return BadRequest(ApiResponse<OtacDto>.Error("Request body is required"));
            }

            // Get client information for security monitoring
            var clientInfo = GetClientInfo(request.ClientInfo);

            // Check rate limiting at service level (additional to attribute-based limiting)
            var rateLimitResult = await _securityMonitoringService.CheckRateLimitAsync(
                "otac_generate", clientInfo.IpAddress!, TimeSpan.FromMinutes(5), 3);

            if (!rateLimitResult.IsAllowed)
            {
                await _securityMonitoringService.LogSecurityEventAsync("OTAC_RATE_LIMIT_EXCEEDED", 
                    clientInfo.IpAddress!, "OTAC generation rate limit exceeded");

                return StatusCode(429, ApiResponse<OtacDto>.Error("Rate limit exceeded. Please try again later."));
            }

            // Generate OTAC
            var result = await _otacManagementService.GenerateOtacAsync(request.RegistrationId ?? 0);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("OTAC generation failed. TraceId: {TraceId}, Error: {Error}",
                    traceId, result.ErrorMessage);

                return BadRequest(ApiResponse<OtacDto>.Error(result.ErrorMessage ?? "Failed to generate OTAC"));
            }

            // Create response DTO
            var otacDto = OtacDto.FromRegistration(
                result.Data.RegistrationId,
                result.Data.OtacCode!,
                result.Data.GeneratedAt,
                result.Data.ExpiresAt);

            _logger.LogInformation("OTAC generated successfully. TraceId: {TraceId}, RegistrationId: {RegistrationId}",
                traceId, result.Data.RegistrationId);

            // Log security event
            await _securityMonitoringService.LogSecurityEventAsync("OTAC_GENERATED", 
                clientInfo.IpAddress!, $"OTAC generated for registration {result.Data.RegistrationId}");

            var response = ApiResponse<OtacDto>.Ok(otacDto, "OTAC generated successfully");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating OTAC. TraceId: {TraceId}", traceId);
            
            var response = ApiResponse<OtacDto>.Error("An error occurred while generating OTAC");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Validates an OTAC code and returns the associated registration information.
    /// Includes attempt tracking and automatic locking after failed attempts.
    /// </summary>
    /// <param name="request">OTAC validation request containing the code to validate</param>
    /// <returns>Validation result with registration details or failure reason</returns>
    [HttpPost("validate")]
    // Rate limiting handled by middleware
    [AllowAnonymous] // Allow anonymous access for registration flow
    [ProducesResponseType(typeof(ApiResponse<ValidateOtacResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 429)]
    public async Task<IActionResult> ValidateOtac([FromBody] ValidateOtacRequest request)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("OTAC validation requested. TraceId: {TraceId}, Code: {Code}, IP: {IP}",
                traceId, request.Code?.Substring(0, Math.Min(4, request.Code?.Length ?? 0)) + "****", GetClientIpAddress());

            // Validate request
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<ValidateOtacResponse>.Error("OTAC code is required"));
            }

            if (request.Code.Length != 8)
            {
                return BadRequest(ApiResponse<ValidateOtacResponse>.Error("OTAC code must be 8 characters"));
            }

            // Get client information
            var clientInfo = GetClientInfo(request.ClientInfo);

            // Enhanced rate limiting for validation attempts
            var rateLimitResult = await _securityMonitoringService.CheckRateLimitAsync(
                "otac_validate", clientInfo.IpAddress!, TimeSpan.FromMinutes(1), 10);

            if (!rateLimitResult.IsAllowed)
            {
                await _securityMonitoringService.LogSecurityEventAsync("OTAC_VALIDATION_RATE_LIMIT", 
                    clientInfo.IpAddress!, "OTAC validation rate limit exceeded");

                return StatusCode(429, ApiResponse<ValidateOtacResponse>.Error("Too many validation attempts. Please try again later."));
            }

            // Validate OTAC
            var result = await _otacManagementService.ValidateOtacAsync(request.Code);

            var response = new ValidateOtacResponse();

            if (result.IsSuccess)
            {
                response = ValidateOtacResponse.Success(result.Data.RegistrationId, result.Data.TimeRemainingSeconds);
                
                _logger.LogInformation("OTAC validation successful. TraceId: {TraceId}, RegistrationId: {RegistrationId}",
                    traceId, result.Data.RegistrationId);

                await _securityMonitoringService.LogSecurityEventAsync("OTAC_VALIDATED_SUCCESS", 
                    clientInfo.IpAddress!, $"OTAC validated successfully for registration {result.Data.RegistrationId}");
            }
            else
            {
                response = ValidateOtacResponse.Failure(
                    result.ErrorMessage ?? "Invalid OTAC code",
                    result.Data?.RemainingAttempts ?? 0,
                    result.Data?.TimeRemainingSeconds ?? 0);

                _logger.LogWarning("OTAC validation failed. TraceId: {TraceId}, Reason: {Reason}, Remaining: {Remaining}",
                    traceId, result.ErrorMessage, result.Data?.RemainingAttempts ?? 0);

                await _securityMonitoringService.LogSecurityEventAsync("OTAC_VALIDATED_FAILED", 
                    clientInfo.IpAddress!, $"OTAC validation failed: {result.ErrorMessage}");
            }

            var apiResponse = ApiResponse<ValidateOtacResponse>.Ok(response, 
                result.IsSuccess ? "OTAC validated successfully" : "OTAC validation failed");
            apiResponse.TraceId = traceId;
            
            return Ok(apiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating OTAC. TraceId: {TraceId}", traceId);
            
            var response = ApiResponse<ValidateOtacResponse>.Error("An error occurred while validating OTAC");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Retrieves OTAC information by code (without validating it).
    /// Useful for checking status, expiration, and attempt counts.
    /// </summary>
    /// <param name="code">The OTAC code to retrieve information for</param>
    /// <returns>OTAC details including status and metadata</returns>
    [HttpGet("{code}")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse<OtacDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> GetOtac([FromRoute] [Required] string code)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogDebug("OTAC retrieval requested. TraceId: {TraceId}, Code: {Code}, IP: {IP}",
                traceId, code?.Substring(0, Math.Min(4, code?.Length ?? 0)) + "****", GetClientIpAddress());

            // Validate code format
            if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
            {
                return BadRequest(ApiResponse<OtacDto>.Error("Invalid OTAC code format"));
            }

            // Get OTAC information
            var result = await _otacManagementService.GetOtacInfoAsync(code);

            if (!result.IsSuccess)
            {
                _logger.LogDebug("OTAC not found. TraceId: {TraceId}, Code: {Code}", traceId, code.Substring(0, 4) + "****");
                return NotFound(ApiResponse<OtacDto>.Error("OTAC not found"));
            }

            // Create response DTO (without exposing the actual code for security)
            var otacDto = new OtacDto
            {
                RegistrationId = result.Data.RegistrationId,
                Code = code.Substring(0, 2) + "******", // Partially masked for security
                GeneratedAt = result.Data.GeneratedAt,
                ExpiresAt = result.Data.ExpiresAt,
                Status = result.Data.Status,
                AttemptCount = result.Data.AttemptCount
            };

            var response = ApiResponse<OtacDto>.Ok(otacDto, "OTAC information retrieved");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OTAC. TraceId: {TraceId}", traceId);
            
            var response = ApiResponse<OtacDto>.Error("An error occurred while retrieving OTAC");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Invalidates/deletes an OTAC code before its natural expiration.
    /// Requires admin privileges and is used for security purposes or manual cleanup.
    /// </summary>
    /// <param name="code">The OTAC code to invalidate</param>
    /// <returns>Confirmation of invalidation</returns>
    [HttpDelete("{code}")]
    [Authorize(Roles = "Admin")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse), 200)]
    [ProducesResponseType(typeof(ApiResponse), 404)]
    public async Task<IActionResult> InvalidateOtac([FromRoute] [Required] string code)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogInformation("OTAC invalidation requested. TraceId: {TraceId}, Code: {Code}, User: {User}, IP: {IP}",
                traceId, code?.Substring(0, Math.Min(4, code?.Length ?? 0)) + "****", username, GetClientIpAddress());

            // Validate code format
            if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
            {
                return BadRequest(ApiResponse.Error("Invalid OTAC code format"));
            }

            // Invalidate OTAC
            var result = await _otacManagementService.InvalidateOtacAsync(code);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("OTAC invalidation failed. TraceId: {TraceId}, Error: {Error}", traceId, result.ErrorMessage);
                return NotFound(ApiResponse.Error(result.ErrorMessage ?? "OTAC not found or already invalid"));
            }

            _logger.LogInformation("OTAC invalidated successfully. TraceId: {TraceId}, User: {User}", traceId, username);

            // Log security event
            await _securityMonitoringService.LogSecurityEventAsync("OTAC_INVALIDATED_ADMIN", 
                GetClientIpAddress(), $"OTAC manually invalidated by admin {username}");

            var response = ApiResponse.Ok("OTAC invalidated successfully");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating OTAC. TraceId: {TraceId}, User: {User}", traceId, username);
            
            var response = ApiResponse.Error("An error occurred while invalidating OTAC");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Gets OTAC system statistics and health information.
    /// Requires admin privileges and provides monitoring data for operations teams.
    /// </summary>
    /// <returns>OTAC system statistics including generation rates, validation success rates, etc.</returns>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    // Rate limiting handled by middleware
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> GetOtacStats()
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        
        try
        {
            _logger.LogDebug("OTAC stats requested. TraceId: {TraceId}, User: {User}", traceId, username);

            // TODO: Implement GetOtacStatisticsAsync in IOtacManagementService
            var stats = new { 
                TotalGenerated = 0, 
                TotalValidated = 0, 
                TotalExpired = 0, 
                SuccessRate = 0.0,
                Message = "Statistics implementation pending"
            };

            var response = ApiResponse<object>.Ok(stats, "OTAC statistics retrieved");
            response.TraceId = traceId;
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OTAC stats. TraceId: {TraceId}", traceId);
            
            var response = ApiResponse<object>.Error("An error occurred while retrieving OTAC statistics");
            response.TraceId = traceId;
            return StatusCode(500, response);
        }
    }

    /// <summary>
    /// Gets client information from request context and provided data
    /// </summary>
    private ClientInfo GetClientInfo(ClientInfo? providedInfo = null)
    {
        return new ClientInfo
        {
            IpAddress = providedInfo?.IpAddress ?? GetClientIpAddress(),
            UserAgent = providedInfo?.UserAgent ?? Request.Headers.UserAgent.ToString(),
            SessionId = providedInfo?.SessionId ?? HttpContext.Session.Id,
            Metadata = providedInfo?.Metadata ?? new Dictionary<string, string>()
        };
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