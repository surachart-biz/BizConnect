using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using BizConnect.Models;
using BizConnect.Services.Exceptions;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BizConnect.Middleware
{
    /// <summary>
    /// Global exception handling middleware that catches all unhandled exceptions
    /// and converts them to appropriate HTTP responses with security considerations.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly JsonSerializerOptions _jsonOptions;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, 
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                await HandleExceptionAsync(context, exception);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var clientIp = GetClientIpAddress(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var username = context.User?.Identity?.Name;
            var requestPath = context.Request.Path.Value;

            // Log the exception with security context
            await LogExceptionAsync(exception, traceId, clientIp, userAgent, username, requestPath);

            // CRITICAL FIX: Check if response has started
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Cannot modify response - headers already sent for {Path}. Exception: {ExceptionType}", 
                    requestPath, exception.GetType().Name);
                return; // Cannot modify response
            }

            try
            {
                // Clear any partial content
                context.Response.Clear();
                
                // Generate appropriate error response
                var errorResponse = CreateErrorResponse(exception, traceId, requestPath);

                // Set response properties
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = errorResponse.StatusCode;

                // Add security headers (safe now since we checked HasStarted)
                AddSecurityHeaders(context.Response);

                // Serialize and write response
                var jsonResponse = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                await context.Response.WriteAsync(jsonResponse);
            }
            catch (InvalidOperationException ioEx)
            {
                _logger.LogWarning(ioEx, "Failed to write error response for {Path} - response may have started", requestPath);
            }
            catch (Exception writeEx)
            {
                _logger.LogError(writeEx, "Failed to write error response for {Path}", requestPath);
            }

            // Trigger additional security measures if needed
            await HandleSecurityViolationAsync(context, exception, clientIp, username);
        }

        private async Task LogExceptionAsync(Exception exception, string traceId, string clientIp, 
            string userAgent, string username, string requestPath)
        {
            var logLevel = GetLogLevel(exception);
            var logMessage = "Unhandled exception occurred";
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = traceId,
                ["ClientIP"] = clientIp,
                ["UserAgent"] = userAgent,
                ["Username"] = username ?? "Anonymous",
                ["RequestPath"] = requestPath,
                ["ExceptionType"] = exception.GetType().Name
            });

            switch (exception)
            {
                case SecurityException secEx:
                    _logger.Log(LogLevel.Warning, exception, 
                        "Security violation: {SecurityEventType} from {ClientIP} for user {Username}. Resource: {Resource}. Severity: {Severity}",
                        secEx.SecurityEventType, clientIp, username, secEx.AttemptedResource, secEx.Severity);
                    break;

                case ValidationException valEx:
                    _logger.Log(LogLevel.Information, exception,
                        "Validation failed for {EntityType} with {ErrorCount} errors. Context: {ValidationContext}",
                        valEx.EntityType, valEx.ErrorCount, valEx.ValidationContext);
                    break;

                case BusinessException bizEx:
                    _logger.Log(bizEx.IsWarning ? LogLevel.Warning : LogLevel.Error, exception,
                        "Business rule violation: {ErrorCode}. User message: {UserMessage}",
                        bizEx.ErrorCode, bizEx.UserMessage);
                    break;

                case UnauthorizedAccessException:
                    _logger.LogWarning(exception, 
                        "Unauthorized access attempt from {ClientIP} for user {Username} to path {RequestPath}",
                        clientIp, username, requestPath);
                    break;

                default:
                    _logger.Log(logLevel, exception, logMessage);
                    break;
            }
        }

        private ErrorResponse CreateErrorResponse(Exception exception, string traceId, string requestPath)
        {
            var includeDetails = _environment.IsDevelopment();

            return exception switch
            {
                ValidationException valEx => CreateValidationErrorResponse(valEx, traceId, requestPath),
                BusinessException bizEx => CreateBusinessErrorResponse(bizEx, traceId, requestPath, includeDetails),
                SecurityException secEx => CreateSecurityErrorResponse(secEx, traceId, requestPath, includeDetails),
                UnauthorizedAccessException => ErrorResponse.UnauthorizedError("Authentication required", traceId)
                    .WithPath(requestPath),
                ArgumentException argEx => ErrorResponse.ValidationError(
                    new Dictionary<string, List<string>> { { "argument", new List<string> { GetSafeMessage(argEx.Message) } } },
                    "Invalid request data", traceId).WithPath(requestPath),
                InvalidOperationException => ErrorResponse.BusinessError(
                    "The requested operation is not valid in the current state", "INVALID_OPERATION", traceId)
                    .WithPath(requestPath),
                TimeoutException => ErrorResponse.ServiceUnavailableError(
                    "The request timed out. Please try again later", traceId).WithPath(requestPath),
                NotImplementedException => ErrorResponse.InternalServerError(
                    "This feature is not yet implemented", traceId, exception.Message, includeDetails)
                    .WithPath(requestPath),
                _ => CreateGenericErrorResponse(exception, traceId, requestPath, includeDetails)
            };
        }

        private ErrorResponse CreateValidationErrorResponse(ValidationException valEx, string traceId, string requestPath)
        {
            var userFriendlyMessage = valEx.ErrorCount == 1 
                ? "Please correct the validation error and try again"
                : $"Please correct the {valEx.ErrorCount} validation errors and try again";

            return ErrorResponse.ValidationError(valEx.ValidationErrors, valEx.GlobalErrors, userFriendlyMessage, traceId)
                .WithPath(requestPath)
                .WithMetadata("entityType", valEx.EntityType)
                .WithMetadata("validationContext", valEx.ValidationContext);
        }

        private ErrorResponse CreateBusinessErrorResponse(BusinessException bizEx, string traceId, 
            string requestPath, bool includeDetails)
        {
            var userMessage = !string.IsNullOrEmpty(bizEx.UserMessage) 
                ? bizEx.UserMessage 
                : "A business rule violation occurred";

            return ErrorResponse.BusinessError(userMessage, bizEx.ErrorCode, traceId)
                .WithPath(requestPath)
                .WithDetails(includeDetails ? bizEx.Message : null, includeDetails)
                .WithMetadata(bizEx.Context);
        }

        private ErrorResponse CreateSecurityErrorResponse(SecurityException secEx, string traceId, 
            string requestPath, bool includeDetails)
        {
            // Always use sanitized message for security exceptions
            var userMessage = secEx.GetSanitizedMessage();

            var response = ErrorResponse.ForbiddenError(userMessage, traceId)
                .WithPath(requestPath);

            // Only include technical details in development
            if (includeDetails)
            {
                response.WithDetails(secEx.Message, true)
                       .WithMetadata("securityEventType", secEx.SecurityEventType)
                       .WithMetadata("severity", secEx.Severity.ToString())
                       .WithMetadata(secEx.SecurityContext);
            }

            return response;
        }

        private ErrorResponse CreateGenericErrorResponse(Exception exception, string traceId, 
            string requestPath, bool includeDetails)
        {
            var userMessage = "An unexpected error occurred. Please try again later.";
            var details = includeDetails ? $"{exception.GetType().Name}: {exception.Message}" : null;

            var response = ErrorResponse.InternalServerError(userMessage, traceId, details, includeDetails)
                .WithPath(requestPath);

            if (includeDetails)
            {
                response.WithMetadata("exceptionType", exception.GetType().FullName)
                       .WithMetadata("stackTrace", exception.StackTrace);

                if (exception.InnerException != null)
                {
                    response.WithMetadata("innerExceptionType", exception.InnerException.GetType().FullName)
                           .WithMetadata("innerExceptionMessage", exception.InnerException.Message);
                }
            }

            return response;
        }

        private async Task HandleSecurityViolationAsync(HttpContext context, Exception exception, 
            string clientIp, string username)
        {
            if (exception is not SecurityException secEx)
                return;

            try
            {
                // Get security audit service if available
                var auditService = context.RequestServices.GetService<ISecurityAuditService>();
                if (auditService != null)
                {
                    await auditService.LogSuspiciousActivityAsync(
                        secEx.SecurityEventType,
                        secEx.Message,
                        clientIp
                    );
                }

                // Additional security measures based on severity
                if (secEx.Severity >= SecuritySeverity.High || secEx.RequiresImmediateAction)
                {
                    // Log critical security event
                    _logger.LogCritical(
                        "High-severity security violation detected: {SecurityEventType} from {ClientIP} for user {Username}",
                        secEx.SecurityEventType, clientIp, username);

                    // Could trigger additional security measures here:
                    // - IP blocking
                    // - Account lockout
                    // - Security alerts
                    // - Rate limiting adjustments
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle security violation properly");
            }
        }

        private static LogLevel GetLogLevel(Exception exception)
        {
            return exception switch
            {
                ValidationException => LogLevel.Information,
                BusinessException bizEx => bizEx.IsWarning ? LogLevel.Warning : LogLevel.Error,
                SecurityException secEx => secEx.Severity switch
                {
                    SecuritySeverity.Critical => LogLevel.Critical,
                    SecuritySeverity.High => LogLevel.Error,
                    SecuritySeverity.Medium => LogLevel.Warning,
                    SecuritySeverity.Low => LogLevel.Information,
                    SecuritySeverity.Info => LogLevel.Information,
                    _ => LogLevel.Warning
                },
                UnauthorizedAccessException => LogLevel.Warning,
                ArgumentException => LogLevel.Information,
                InvalidOperationException => LogLevel.Warning,
                TimeoutException => LogLevel.Warning,
                NotImplementedException => LogLevel.Information,
                _ => LogLevel.Error
            };
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            try
            {
                // Check for forwarded IP first (reverse proxy scenarios)
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (ips.Length > 0)
                    {
                        return ips[0].Trim();
                    }
                }

                // Check for real IP header
                var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp))
                {
                    return realIp.Trim();
                }

                // Fall back to connection remote IP
                return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static void AddSecurityHeaders(HttpResponse response)
        {
            try
            {
                // Prevent MIME type sniffing
                if (!response.Headers.ContainsKey("X-Content-Type-Options"))
                {
                    response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
                }

                // Prevent caching of error responses
                if (!response.Headers.ContainsKey("Cache-Control"))
                {
                    response.Headers.TryAdd("Cache-Control", "no-cache, no-store, must-revalidate");
                    response.Headers.TryAdd("Pragma", "no-cache");
                    response.Headers.TryAdd("Expires", "0");
                }
            }
            catch (InvalidOperationException)
            {
                // Headers already sent - ignore silently
                // This is a defensive measure in case response.HasStarted changes between checks
            }
        }

        private static string GetSafeMessage(string message)
        {
            // Remove potentially sensitive information from error messages
            if (string.IsNullOrEmpty(message))
                return "Invalid data provided";

            // Basic sanitization - remove common sensitive patterns
            var safeMessage = message;
            
            // Remove connection strings
            if (safeMessage.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                safeMessage.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                safeMessage.Contains("server=", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid configuration detected";
            }

            // Remove file paths
            if (safeMessage.Contains(":\\\\") || safeMessage.Contains("/usr/") || safeMessage.Contains("/var/"))
            {
                return "Invalid file path specified";
            }

            // Truncate very long messages
            if (safeMessage.Length > 200)
            {
                safeMessage = safeMessage.Substring(0, 200) + "...";
            }

            return safeMessage;
        }
    }
}