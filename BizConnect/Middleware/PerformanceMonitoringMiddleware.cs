using BizConnect.Services.Interfaces;
using System.Diagnostics;

namespace BizConnect.Middleware;

/// <summary>
/// Middleware to monitor API endpoint performance and track metrics
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Monitor request performance and record metrics
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Task</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = GetEndpointName(context);
        Exception? caughtException = null;
        
        try
        {
            await _next(context);
            stopwatch.Stop();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            caughtException = ex;
        }

        var responseTimeMs = (int)stopwatch.ElapsedMilliseconds;
        var isSuccess = caughtException == null && context.Response.StatusCode < 400;

        // Record performance metric if service is available
        var performanceService = context.RequestServices.GetService<IPerformanceMonitorService>();
        if (performanceService != null)
        {
            try
            {
                await performanceService.RecordMetricAsync(endpoint, responseTimeMs, isSuccess);
            }
            catch (Exception metricEx)
            {
                _logger.LogWarning(metricEx, "Failed to record performance metric for {Endpoint}", endpoint);
            }
        }

        // Log performance information (always safe)
        if (caughtException != null)
        {
            _logger.LogError(caughtException, "Request failed for {Endpoint} after {ResponseTime}ms", 
                endpoint, responseTimeMs);
        }
        else if (responseTimeMs > 1000)
        {
            _logger.LogWarning("Slow request detected: {Endpoint} took {ResponseTime}ms", 
                endpoint, responseTimeMs);
        }
        else
        {
            _logger.LogDebug("Request {Endpoint} completed in {ResponseTime}ms", 
                endpoint, responseTimeMs);
        }

        // CRITICAL FIX: Only add headers if response hasn't started
        try
        {
            if (!context.Response.HasStarted)
            {
                // Remove existing header if present
                if (context.Response.Headers.ContainsKey("X-Response-Time"))
                {
                    context.Response.Headers.Remove("X-Response-Time");
                }
                context.Response.Headers.TryAdd("X-Response-Time", $"{responseTimeMs}ms");
                context.Response.Headers.TryAdd("X-Server-Name", Environment.MachineName);
            }
        }
        catch (InvalidOperationException)
        {
            // Headers already sent - log but continue
            _logger.LogDebug("Could not add performance headers for {Endpoint} - response already started", endpoint);
        }

        // Re-throw any caught exception
        if (caughtException != null)
        {
            throw caughtException;
        }
    }

    /// <summary>
    /// Extract endpoint name from HTTP context
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Endpoint name</returns>
    private string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var actionDescriptor = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            if (actionDescriptor != null)
            {
                return $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}";
            }
        }

        // Fallback to path for non-MVC endpoints
        var path = context.Request.Path.Value ?? "Unknown";
        var method = context.Request.Method;
        
        return $"{method} {path}";
    }
}