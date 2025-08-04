using System;
using System.Net;
using System.Threading.Tasks;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Security;
using BizConnect.Services.Security.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BizConnect.Attributes;

/// <summary>
/// Rate limiting attribute for controllers and actions with advanced threat detection.
/// Integrates with the AdvancedRateLimitingService for comprehensive protection.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RateLimitAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Operation context for rate limiting (e.g., "login", "otac_validate", "api_call", "registration").
    /// </summary>
    public string Operation { get; set; } = "default";
    
    /// <summary>
    /// Custom rate limit override (requests per time window).
    /// If not specified, uses the default rule for the operation.
    /// </summary>
    public int? MaxRequests { get; set; }
    
    /// <summary>
    /// Custom time window override in minutes.
    /// If not specified, uses the default rule for the operation.
    /// </summary>
    public int? TimeWindowMinutes { get; set; }
    
    /// <summary>
    /// Whether to include username in rate limiting key for user-specific limits.
    /// </summary>
    public bool IncludeUsername { get; set; } = false;
    
    /// <summary>
    /// Whether to log security events when rate limits are exceeded.
    /// </summary>
    public bool LogSecurityEvents { get; set; } = true;
    
    /// <summary>
    /// Custom error message when rate limit is exceeded.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// HTTP status code to return when rate limited (default: 429 Too Many Requests).
    /// </summary>
    public int StatusCode { get; set; } = 429;
    
    /// <summary>
    /// Whether to include detailed rate limit information in response headers.
    /// </summary>
    public bool IncludeHeaders { get; set; } = true;
    
    /// <summary>
    /// Initializes a new instance of the RateLimitAttribute.
    /// </summary>
    /// <param name="operation">The operation context for rate limiting</param>
    public RateLimitAttribute(string operation = "default")
    {
        Operation = operation;
    }
    
    /// <summary>
    /// Executes rate limiting check before the action is executed.
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        try
        {
            var rateLimitingService = context.HttpContext.RequestServices.GetService<IRateLimitingService>();
            var securityAuditService = context.HttpContext.RequestServices.GetService<ISecurityAuditService>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<RateLimitAttribute>>();
            
            if (rateLimitingService == null)
            {
                logger?.LogWarning("Rate limiting service not available, skipping rate limit check");
                await next();
                return;
            }
            
            // Get client information
            var clientInfo = GetClientInfo(context);
            
            // Build rate limit request
            var rateLimitRequest = new RateLimitRequest
            {
                IpAddress = clientInfo.IpAddress,
                Context = Operation,
                Username = IncludeUsername ? clientInfo.Username : null,
                Endpoint = clientInfo.Endpoint,
                Timestamp = DateTime.UtcNow,
                IsFailedAttempt = false,
                Metadata = new Dictionary<string, string>
                {
                    ["UserAgent"] = clientInfo.UserAgent ?? "",
                    ["RequestPath"] = clientInfo.RequestPath ?? "",
                    ["Method"] = clientInfo.HttpMethod ?? "",
                    ["SessionId"] = clientInfo.SessionId ?? ""
                }
            };
            
            // Check rate limit
            RateLimitStatus rateLimitStatus;
            
            if (rateLimitingService is AdvancedRateLimitingService advancedService)
            {
                // Use advanced rate limiting with threat detection
                var advancedRequest = new RateLimitRequest
                {
                    IpAddress = clientInfo.IpAddress,
                    Context = Operation,
                    Username = IncludeUsername ? clientInfo.Username : null,
                    Endpoint = clientInfo.Endpoint,
                    Timestamp = DateTime.UtcNow,
                    IsFailedAttempt = false,
                    Metadata = rateLimitRequest.Metadata
                };
                
                var advancedResult = await advancedService.CheckAdvancedRateLimitAsync(advancedRequest);
                
                rateLimitStatus = new RateLimitStatus
                {
                    IsLocked = advancedResult.IsBlocked,
                    Message = advancedResult.IsBlocked ? 
                        $"Rate limit exceeded. Threat score: {advancedResult.ThreatScore?.Score:F1}" : 
                        "Request allowed"
                };
                
                if (advancedResult.Checks?.Any() == true)
                {
                    var primaryCheck = advancedResult.Checks.First();
                    rateLimitStatus.TotalAttempts = primaryCheck.CurrentCount;
                    rateLimitStatus.RemainingAttempts = Math.Max(0, primaryCheck.Rule.MaxRequests - primaryCheck.CurrentCount);
                    
                    if (primaryCheck.BlockedUntil.HasValue)
                    {
                        rateLimitStatus.LockoutEndTime = primaryCheck.BlockedUntil.Value;
                        rateLimitStatus.TimeUntilUnlock = primaryCheck.BlockedUntil.Value - DateTime.UtcNow;
                    }
                }
            }
            else
            {
                // Use basic rate limiting
                rateLimitStatus = await rateLimitingService.CheckRateLimitAsync(clientInfo.IpAddress, Operation);
            }
            
            // Record the attempt
            await rateLimitingService.RecordFailedAttemptAsync(clientInfo.IpAddress, Operation, clientInfo.Username);
            
            // Check if rate limited
            if (rateLimitStatus.IsLocked)
            {
                // Log security event
                if (LogSecurityEvents && securityAuditService != null)
                {
                    await securityAuditService.LogSecurityEventAsync("RateLimit", "ExceededLimit", new
                    {
                        Operation = Operation,
                        IpAddress = clientInfo.IpAddress,
                        Username = clientInfo.Username,
                        UserAgent = clientInfo.UserAgent,
                        Endpoint = clientInfo.Endpoint,
                        TotalAttempts = rateLimitStatus.TotalAttempts,
                        RemainingAttempts = rateLimitStatus.RemainingAttempts,
                        LockoutEndTime = rateLimitStatus.LockoutEndTime,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                logger?.LogWarning("Rate limit exceeded for {Operation} from IP {IpAddress}, User {Username}. " +
                    "Attempts: {TotalAttempts}, Remaining: {RemainingAttempts}",
                    Operation, clientInfo.IpAddress, clientInfo.Username ?? "Anonymous", 
                    rateLimitStatus.TotalAttempts, rateLimitStatus.RemainingAttempts);
                
                // Set response headers if enabled
                if (IncludeHeaders)
                {
                    SetRateLimitHeaders(context, rateLimitStatus);
                }
                
                // Return rate limit exceeded response
                context.Result = CreateRateLimitResponse(rateLimitStatus);
                return;
            }
            
            // Set success headers if enabled
            if (IncludeHeaders)
            {
                SetRateLimitHeaders(context, rateLimitStatus);
            }
            
            // Continue with the action
            var executedContext = await next();
            
            // Check if the action resulted in a failure that should be recorded
            if (ShouldRecordAsFailedAttempt(executedContext))
            {
                await rateLimitingService.RecordFailedAttemptAsync(clientInfo.IpAddress, Operation, clientInfo.Username);
                
                if (LogSecurityEvents && securityAuditService != null)
                {
                    await securityAuditService.LogSecurityEventAsync("RateLimit", "FailedAttempt", new
                    {
                        Operation = Operation,
                        IpAddress = clientInfo.IpAddress,
                        Username = clientInfo.Username,
                        StatusCode = context.HttpContext.Response.StatusCode,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<RateLimitAttribute>>();
            logger?.LogError(ex, "Error in rate limiting filter for operation {Operation}", Operation);
            
            // Continue with the action on error to avoid blocking legitimate requests
            await next();
        }
    }
    
    /// <summary>
    /// Extracts client information from the request context.
    /// </summary>
    private ClientInfo GetClientInfo(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        var user = context.HttpContext.User;
        
        return new ClientInfo
        {
            IpAddress = GetClientIpAddress(context.HttpContext),
            Username = user?.Identity?.Name,
            UserAgent = request.Headers["User-Agent"].FirstOrDefault(),
            Endpoint = $"{request.Method} {request.Path}",
            RequestPath = request.Path,
            HttpMethod = request.Method,
            SessionId = context.HttpContext.Session?.Id
        };
    }
    
    /// <summary>
    /// Gets the client IP address, considering proxy headers.
    /// </summary>
    private string GetClientIpAddress(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        // Check for forwarded IP headers (in order of preference)
        var forwardedHeaders = new[]
        {
            "CF-Connecting-IP",     // Cloudflare
            "X-Forwarded-For",      // Standard proxy header
            "X-Real-IP",            // Nginx proxy
            "X-Client-IP",          // Apache proxy
            "X-Cluster-Client-IP"   // Cluster proxy
        };
        
        foreach (var header in forwardedHeaders)
        {
            var value = httpContext.Request.Headers[header].FirstOrDefault();
            if (!string.IsNullOrEmpty(value))
            {
                // Take the first IP if multiple are present (comma-separated)
                var ip = value.Split(',')[0].Trim();
                if (IsValidIpAddress(ip))
                {
                    return ip;
                }
            }
        }
        
        // Fall back to connection remote IP
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        return !string.IsNullOrEmpty(remoteIp) ? remoteIp : "127.0.0.1";
    }
    
    /// <summary>
    /// Validates if the provided string is a valid IP address.
    /// </summary>
    private bool IsValidIpAddress(string ip)
    {
        return IPAddress.TryParse(ip, out var address) && 
               !IPAddress.IsLoopback(address) && 
               address != IPAddress.Any && 
               address != IPAddress.IPv6Any;
    }
    
    /// <summary>
    /// Sets rate limit headers in the response.
    /// </summary>
    private void SetRateLimitHeaders(ActionExecutingContext context, RateLimitStatus status)
    {
        var response = context.HttpContext.Response;
        
        // Standard rate limit headers
        if (status.TotalAttempts > 0)
        {
            response.Headers["X-RateLimit-Limit"] = status.TotalAttempts.ToString();
        }
        
        if (status.RemainingAttempts >= 0)
        {
            response.Headers["X-RateLimit-Remaining"] = status.RemainingAttempts.ToString();
        }
        
        if (status.TimeUntilUnlock.HasValue && status.TimeUntilUnlock.Value.TotalSeconds > 0)
        {
            response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(status.TimeUntilUnlock.Value).ToUnixTimeSeconds().ToString();
            response.Headers["X-RateLimit-Reset-After"] = ((int)status.TimeUntilUnlock.Value.TotalSeconds).ToString();
        }
        
        // Custom headers
        response.Headers["X-RateLimit-Context"] = Operation;
        
        if (status.IsLocked)
        {
            response.Headers["X-RateLimit-Status"] = "Limited";
        }
        else
        {
            response.Headers["X-RateLimit-Status"] = "OK";
        }
    }
    
    /// <summary>
    /// Creates a rate limit exceeded response.
    /// </summary>
    private IActionResult CreateRateLimitResponse(RateLimitStatus status)
    {
        var message = ErrorMessage ?? status.Message ?? "Rate limit exceeded. Please try again later.";
        
        var responseObject = new
        {
            error = "rate_limit_exceeded",
            message = message,
            operation = Operation,
            details = new
            {
                totalAttempts = status.TotalAttempts,
                remainingAttempts = status.RemainingAttempts,
                lockoutEndTime = status.LockoutEndTime,
                timeUntilUnlock = status.TimeUntilUnlock?.TotalSeconds
            }
        };
        
        return new ObjectResult(responseObject)
        {
            StatusCode = StatusCode
        };
    }
    
    /// <summary>
    /// Determines if the action execution should be recorded as a failed attempt.
    /// </summary>
    private bool ShouldRecordAsFailedAttempt(ActionExecutedContext context)
    {
        // Record as failed attempt for authentication/authorization failures
        var statusCode = context.HttpContext.Response.StatusCode;
        
        return statusCode == 401 || // Unauthorized 
               statusCode == 403 || // Forbidden
               statusCode == 400 || // Bad Request (for validation failures)
               (Operation == "login" && statusCode >= 400) ||
               (Operation == "otac_validate" && statusCode >= 400);
    }
}

/// <summary>
/// Client information extracted from the request.
/// </summary>
internal class ClientInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? UserAgent { get; set; }
    public string? Endpoint { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public string? SessionId { get; set; }
}

// RateLimitRequest is now defined in BizConnect.Services.Security.Models.SecurityModels

/// <summary>
/// Extension methods for easy rate limiting setup.
/// </summary>
public static class RateLimitExtensions
{
    /// <summary>
    /// Applies OTAC-specific rate limiting (3/min, 10/15min, 50/hour per IP).
    /// </summary>
    public static RateLimitAttribute ForOtacValidation()
    {
        return new RateLimitAttribute("OTAC_VALIDATE")
        {
            LogSecurityEvents = true,
            IncludeHeaders = true,
            ErrorMessage = "Too many OTAC validation attempts. Please wait before trying again."
        };
    }
    
    /// <summary>
    /// Applies login-specific rate limiting (5/15min per user, 20/hour per IP).
    /// </summary>
    public static RateLimitAttribute ForLogin()
    {
        return new RateLimitAttribute("LOGIN_ATTEMPTS")
        {
            IncludeUsername = true,
            LogSecurityEvents = true,
            IncludeHeaders = true,
            ErrorMessage = "Too many login attempts. Please wait before trying again."
        };
    }
    
    /// <summary>
    /// Applies API-specific rate limiting (100/min per authenticated user).
    /// </summary>
    public static RateLimitAttribute ForApiCalls()
    {
        return new RateLimitAttribute("API_CALLS")
        {
            IncludeUsername = true,
            LogSecurityEvents = false,
            IncludeHeaders = true,
            ErrorMessage = "API rate limit exceeded. Please reduce request frequency."
        };
    }
    
    /// <summary>
    /// Applies registration-specific rate limiting (5/hour per IP).
    /// </summary>
    public static RateLimitAttribute ForRegistration()
    {
        return new RateLimitAttribute("REGISTRATION")
        {
            LogSecurityEvents = true,
            IncludeHeaders = true,
            ErrorMessage = "Registration rate limit exceeded. Please try again later."
        };
    }
}

// RateLimitStatus is now defined in BizConnect.Services.Security.Models.SecurityModels