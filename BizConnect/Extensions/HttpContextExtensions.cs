using System.Diagnostics;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for HttpContext to simplify request information access
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Gets the client IP address with proxy header support
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Client IP address or "Unknown" if not found</returns>
    public static string GetClientIpAddress(this HttpContext context)
    {
        if (context?.Request == null)
            return "Unknown";

        // Check for forwarded IP headers (for reverse proxies, load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            var firstIp = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp))
                return firstIp;
        }

        // Check for real IP header (nginx)
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp;

        // Check for client IP header (Azure Application Gateway)
        var clientIp = context.Request.Headers["X-Client-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(clientIp))
            return clientIp;

        // Fall back to remote IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Gets the user agent string
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>User agent string or "Unknown" if not found</returns>
    public static string GetUserAgent(this HttpContext context)
    {
        return context?.Request?.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
    }

    /// <summary>
    /// Gets the trace ID for logging correlation
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Trace ID or random identifier if not found</returns>
    public static string GetTraceId(this HttpContext context)
    {
        // Try to get from Activity (ASP.NET Core tracing)
        var activityId = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(activityId))
            return activityId;

        // Fall back to HttpContext trace identifier
        return context?.TraceIdentifier ?? Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Gets the request ID for tracking
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Request ID</returns>
    public static string GetRequestId(this HttpContext context)
    {
        return context?.TraceIdentifier ?? Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// Checks if the request is from a mobile device (basic detection)
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>True if request appears to be from mobile device</returns>
    public static bool IsMobileRequest(this HttpContext context)
    {
        var userAgent = context.GetUserAgent().ToLowerInvariant();
        return userAgent.Contains("mobile") || 
               userAgent.Contains("android") || 
               userAgent.Contains("iphone") || 
               userAgent.Contains("ipad") ||
               userAgent.Contains("tablet");
    }

    /// <summary>
    /// Gets the referrer URL
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Referrer URL or empty string if not found</returns>
    public static string GetReferrer(this HttpContext context)
    {
        return context?.Request?.Headers["Referer"].FirstOrDefault() ?? string.Empty;
    }

    #region OTAC Session Management Helpers

    /// <summary>
    /// Sets OTAC verification in session
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="otacCode">The OTAC code</param>
    public static void SetOtacVerified(this HttpContext context, string otacCode)
    {
        if (context?.Session == null) return;

        context.Session.SetString("otac_verified", "true");
        context.Session.SetString("otac_code", otacCode);
        context.Session.SetString("otac_verified_at", DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Checks if OTAC is verified in session
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>True if OTAC is verified</returns>
    public static bool IsOtacVerified(this HttpContext context)
    {
        return context?.Session?.GetString("otac_verified") == "true";
    }

    /// <summary>
    /// Gets the verified OTAC code from session
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>OTAC code or null if not found</returns>
    public static string? GetVerifiedOtacCode(this HttpContext context)
    {
        return context?.Session?.GetString("otac_code");
    }

    /// <summary>
    /// Clears OTAC verification from session
    /// </summary>
    /// <param name="context">The HTTP context</param>
    public static void ClearOtacVerification(this HttpContext context)
    {
        if (context?.Session == null) return;

        context.Session.Remove("otac_verified");
        context.Session.Remove("otac_code");
        context.Session.Remove("otac_verified_at");
        context.Session.Remove("validated_otac");
        context.Session.Remove("otac_validated_at");
    }

    /// <summary>
    /// Sets validated OTAC for guest registration flow
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="otacCode">The OTAC code</param>
    public static void SetValidatedOtac(this HttpContext context, string otacCode)
    {
        if (context?.Session == null) return;

        context.Session.SetString("validated_otac", otacCode);
        context.Session.SetString("otac_validated_at", DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Gets the validated OTAC for guest registration flow
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Validated OTAC code or null if not found</returns>
    public static string? GetValidatedOtac(this HttpContext context)
    {
        return context?.Session?.GetString("validated_otac");
    }

    /// <summary>
    /// Gets when OTAC was validated
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>Validation timestamp or null if not found</returns>
    public static DateTime? GetOtacValidatedAt(this HttpContext context)
    {
        var timestampStr = context?.Session?.GetString("otac_validated_at");
        if (DateTime.TryParse(timestampStr, out var timestamp))
            return timestamp;
        return null;
    }

    #endregion
}