using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BizConnect.Services.DTOs;
using BizConnect.Models.Api;
using System.Security.Claims;

namespace BizConnect.Controllers.Api;

/// <summary>
/// API controller for session management and security operations
/// </summary>
[Authorize]
[ApiController]
[Route("api/session")]
[Produces("application/json")]
public class SessionApiController : ControllerBase
{
    private readonly ILogger<SessionApiController> _logger;

    public SessionApiController(ILogger<SessionApiController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extend the current user session
    /// </summary>
    /// <returns>Session extension result</returns>
    [HttpPost("extend")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ApiResponse<SessionExtensionResult>>> ExtendSession()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Session extension attempt with invalid user ID");
                return BadRequest(ApiResponse<SessionExtensionResult>.Error("Invalid session"));
            }

            // Check if user is still valid and active
            // In a real implementation, you would check the database
            
            // Log the session extension
            _logger.LogInformation("Session extended for user {Username} (ID: {UserId}) from IP {IPAddress}", 
                username, userId, HttpContext.Connection.RemoteIpAddress);

            // Update session timeout (if using in-memory sessions)
            HttpContext.Session.SetString("LastExtended", DateTimeOffset.UtcNow.ToString("O"));
            HttpContext.Session.SetInt32("ExtensionCount", 
                HttpContext.Session.GetInt32("ExtensionCount") ?? 0 + 1);

            var result = new SessionExtensionResult
            {
                ExtendedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30), // 30-minute extension
                ExtensionCount = HttpContext.Session.GetInt32("ExtensionCount") ?? 1,
                MaxExtensions = 5,
                Success = true
            };

            return Ok(ApiResponse<SessionExtensionResult>.Ok(result, "Session extended successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending session for user {Username}", User.Identity?.Name);
            return StatusCode(500, ApiResponse<SessionExtensionResult>.Error("Failed to extend session"));
        }
    }

    /// <summary>
    /// Send a heartbeat to maintain session
    /// </summary>
    /// <returns>Heartbeat response</returns>
    [HttpPost("heartbeat")]
    public async Task<ActionResult<ApiResponse<object>>> Heartbeat()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.Error("Session invalid"));
            }

            // Update last activity timestamp
            HttpContext.Session.SetString("LastHeartbeat", DateTimeOffset.UtcNow.ToString("O"));

            _logger.LogDebug("Heartbeat received from user {Username} (ID: {UserId})", 
                User.Identity?.Name, userId);

            return Ok(ApiResponse<object>.Ok(new { timestamp = DateTimeOffset.UtcNow }, "Heartbeat received"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat for user {Username}", User.Identity?.Name);
            return StatusCode(500, ApiResponse<object>.Error("Heartbeat failed"));
        }
    }

    /// <summary>
    /// Get current session information
    /// </summary>
    /// <returns>Session information</returns>
    [HttpGet("info")]
    public async Task<ActionResult<ApiResponse<SessionInfo>>> GetSessionInfo()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.Identity?.Name;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<SessionInfo>.Error("Session invalid"));
            }

            var lastExtendedStr = HttpContext.Session.GetString("LastExtended");
            var lastExtended = !string.IsNullOrEmpty(lastExtendedStr) 
                ? DateTimeOffset.Parse(lastExtendedStr) 
                : (DateTimeOffset?)null;

            var sessionInfo = new SessionInfo
            {
                UserId = userId,
                Username = username,
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                Roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList(),
                LastExtended = lastExtended,
                ExtensionCount = HttpContext.Session.GetInt32("ExtensionCount") ?? 0,
                MaxExtensions = 5,
                SessionTimeout = 30, // minutes
                RemainingTime = CalculateRemainingTime(lastExtended),
                ClientIP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                SessionStart = GetSessionStartTime()
            };

            return Ok(ApiResponse<SessionInfo>.Ok(sessionInfo, "Session information retrieved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session info for user {Username}", User.Identity?.Name);
            return StatusCode(500, ApiResponse<SessionInfo>.Error("Failed to get session information"));
        }
    }

    /// <summary>
    /// Refresh CSRF token
    /// </summary>
    /// <returns>New CSRF token</returns>
    [HttpGet("csrf-token")]
    public ActionResult<ApiResponse<CsrfTokenResult>> GetCsrfToken()
    {
        try
        {
            // Generate new anti-forgery token
            var tokens = HttpContext.RequestServices
                .GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>()
                .GetAndStoreTokens(HttpContext);

            var result = new CsrfTokenResult
            {
                Token = tokens.RequestToken ?? string.Empty,
                HeaderName = tokens.HeaderName ?? "X-CSRF-TOKEN",
                GeneratedAt = DateTimeOffset.UtcNow
            };

            _logger.LogDebug("CSRF token generated for user {Username}", User.Identity?.Name);

            return Ok(ApiResponse<CsrfTokenResult>.Ok(result, "CSRF token generated"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSRF token for user {Username}", User.Identity?.Name);
            return StatusCode(500, ApiResponse<CsrfTokenResult>.Error("Failed to generate CSRF token"));
        }
    }

    /// <summary>
    /// Log security event (for client-side reporting)
    /// </summary>
    /// <param name="securityEvent">Security event to log</param>
    /// <returns>Logging result</returns>
    [HttpPost("security-event")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ApiResponse<object>>> LogSecurityEvent([FromBody] SecurityEventRequest securityEvent)
    {
        try
        {
            if (securityEvent == null)
            {
                return BadRequest(ApiResponse<object>.Error("Invalid security event data"));
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.Identity?.Name;

            // Log the security event with proper structure
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["UserId"] = userId ?? "unknown",
                ["Username"] = username ?? "unknown",
                ["SecurityEventType"] = securityEvent.Type ?? "unknown",
                ["ClientIP"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                ["UserAgent"] = Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown"
            });

            // Determine log level based on event type
            var logLevel = GetLogLevelForSecurityEvent(securityEvent.Type);
            
            _logger.Log(logLevel, "Security event reported: {EventType} - {EventDetails}", 
                securityEvent.Type, 
                System.Text.Json.JsonSerializer.Serialize(securityEvent.Details));

            // In a production system, you might want to:
            // 1. Store security events in a dedicated security log database
            // 2. Send alerts for critical security events
            // 3. Update user security scores or risk assessments
            // 4. Trigger automated security responses

            return Ok(ApiResponse<object>.Ok(new { logged = true }, "Security event logged"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging security event for user {Username}: {EventType}", 
                User.Identity?.Name, securityEvent?.Type);
            return StatusCode(500, ApiResponse<object>.Error("Failed to log security event"));
        }
    }

    #region Private Methods

    /// <summary>
    /// Calculate remaining session time in minutes
    /// </summary>
    private int CalculateRemainingTime(DateTimeOffset? lastExtended)
    {
        var sessionTimeout = 30; // 30 minutes default
        var sessionStart = lastExtended ?? GetSessionStartTime();
        var elapsed = DateTimeOffset.UtcNow - sessionStart;
        var remaining = sessionTimeout - (int)elapsed.TotalMinutes;
        
        return Math.Max(0, remaining);
    }

    /// <summary>
    /// Get session start time from claims or estimate
    /// </summary>
    private DateTimeOffset GetSessionStartTime()
    {
        // Try to get from authentication timestamp claim
        var authTimeClaim = User.FindFirst("auth_time");
        if (authTimeClaim != null && long.TryParse(authTimeClaim.Value, out var timestamp))
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }

        // Fallback: estimate based on issued at claim
        var issuedAtClaim = User.FindFirst("iat");
        if (issuedAtClaim != null && long.TryParse(issuedAtClaim.Value, out var issuedAt))
        {
            return DateTimeOffset.FromUnixTimeSeconds(issuedAt);
        }

        // Final fallback: assume current session started recently
        return DateTimeOffset.UtcNow.AddMinutes(-5);
    }

    /// <summary>
    /// Determine appropriate log level for security event
    /// </summary>
    private LogLevel GetLogLevelForSecurityEvent(string? eventType)
    {
        return eventType switch
        {
            "anomaly_detected" => LogLevel.Warning,
            "failed_login" => LogLevel.Warning,
            "rate_limit_exceeded" => LogLevel.Warning,
            "session_timeout" => LogLevel.Information,
            "invalid_otac" => LogLevel.Warning,
            "security_level_change" => LogLevel.Information,
            "developer_tools_access" => LogLevel.Debug,
            _ => LogLevel.Information
        };
    }

    #endregion
}

#region DTOs

/// <summary>
/// Session extension result
/// </summary>
public class SessionExtensionResult
{
    public bool Extended { get; set; }
    public DateTime NewExpiryTime { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ExtendedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int ExtensionCount { get; set; }
    public int MaxExtensions { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Session information
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public bool IsAuthenticated { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTimeOffset? LastExtended { get; set; }
    public int ExtensionCount { get; set; }
    public int MaxExtensions { get; set; }
    public int SessionTimeout { get; set; } // minutes
    public int RemainingTime { get; set; } // minutes
    public string? ClientIP { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset SessionStart { get; set; }
}

/// <summary>
/// CSRF token result
/// </summary>
public class CsrfTokenResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string HeaderName { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}

/// <summary>
/// Security event request from client
/// </summary>
public class SecurityEventRequest
{
    public string? Type { get; set; }
    public Dictionary<string, object>? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? SecurityLevel { get; set; }
    public bool IsHighPriority { get; set; }
}

#endregion