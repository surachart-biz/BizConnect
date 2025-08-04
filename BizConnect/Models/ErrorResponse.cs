using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BizConnect.Models
{
    /// <summary>
    /// Standardized error response model for consistent API error handling across the application.
    /// Provides structured error information while maintaining security by not exposing sensitive details.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// HTTP status code of the error response
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// User-friendly error message safe for client display
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// Correlation ID for tracing and debugging purposes
        /// </summary>
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; }

        /// <summary>
        /// Technical error details (only included in development environment)
        /// </summary>
        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Details { get; set; }

        /// <summary>
        /// Field-specific validation errors for form validation feedback
        /// </summary>
        [JsonPropertyName("validationErrors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>> ValidationErrors { get; set; }

        /// <summary>
        /// Global validation errors not tied to specific fields
        /// </summary>
        [JsonPropertyName("globalErrors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> GlobalErrors { get; set; }

        /// <summary>
        /// Error code for programmatic error handling
        /// </summary>
        [JsonPropertyName("errorCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorCode { get; set; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Request path where the error occurred
        /// </summary>
        [JsonPropertyName("path")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Path { get; set; }

        /// <summary>
        /// Additional metadata for error context (development only)
        /// </summary>
        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object> Metadata { get; set; }

        public ErrorResponse()
        {
        }

        public ErrorResponse(int statusCode, string message, string traceId = null)
        {
            StatusCode = statusCode;
            Message = message;
            TraceId = traceId;
        }

        public ErrorResponse(int statusCode, string message, Dictionary<string, List<string>> validationErrors, 
            string traceId = null)
        {
            StatusCode = statusCode;
            Message = message;
            ValidationErrors = validationErrors;
            TraceId = traceId;
        }

        /// <summary>
        /// Create an error response for validation failures
        /// </summary>
        public static ErrorResponse ValidationError(Dictionary<string, List<string>> validationErrors, 
            string message = "Validation failed", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 400,
                Message = message,
                ValidationErrors = validationErrors,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for validation failures with global errors
        /// </summary>
        public static ErrorResponse ValidationError(Dictionary<string, List<string>> validationErrors, 
            List<string> globalErrors, string message = "Validation failed", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 400,
                Message = message,
                ValidationErrors = validationErrors,
                GlobalErrors = globalErrors,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for business logic violations
        /// </summary>
        public static ErrorResponse BusinessError(string message, string errorCode = null, string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 400,
                Message = message,
                ErrorCode = errorCode,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for unauthorized access
        /// </summary>
        public static ErrorResponse UnauthorizedError(string message = "Unauthorized access", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 401,
                Message = message,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for forbidden access
        /// </summary>
        public static ErrorResponse ForbiddenError(string message = "Access denied", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 403,
                Message = message,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for resource not found
        /// </summary>
        public static ErrorResponse NotFoundError(string message = "Resource not found", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 404,
                Message = message,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for rate limiting violations
        /// </summary>
        public static ErrorResponse RateLimitError(string message = "Too many requests", string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 429,
                Message = message,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for internal server errors
        /// </summary>
        public static ErrorResponse InternalServerError(string message = "An unexpected error occurred", 
            string traceId = null, string details = null, bool includeDetails = false)
        {
            return new ErrorResponse
            {
                StatusCode = 500,
                Message = message,
                Details = includeDetails ? details : null,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Create an error response for external service failures
        /// </summary>
        public static ErrorResponse ServiceUnavailableError(string message = "Service temporarily unavailable", 
            string traceId = null)
        {
            return new ErrorResponse
            {
                StatusCode = 503,
                Message = message,
                TraceId = traceId
            };
        }

        /// <summary>
        /// Add metadata to the error response (development only)
        /// </summary>
        public ErrorResponse WithMetadata(string key, object value)
        {
            Metadata ??= new Dictionary<string, object>();
            Metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple metadata entries
        /// </summary>
        public ErrorResponse WithMetadata(Dictionary<string, object> metadata)
        {
            if (metadata != null)
            {
                Metadata ??= new Dictionary<string, object>();
                foreach (var kvp in metadata)
                {
                    Metadata[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// Set the request path where the error occurred
        /// </summary>
        public ErrorResponse WithPath(string path)
        {
            Path = path;
            return this;
        }

        /// <summary>
        /// Set technical details (development only)
        /// </summary>
        public ErrorResponse WithDetails(string details, bool includeDetails = false)
        {
            if (includeDetails)
            {
                Details = details;
            }
            return this;
        }

        /// <summary>
        /// Check if this error response contains validation errors
        /// </summary>
        public bool HasValidationErrors => ValidationErrors?.Count > 0 || GlobalErrors?.Count > 0;

        /// <summary>
        /// Get total count of validation errors
        /// </summary>
        public int ValidationErrorCount
        {
            get
            {
                var count = 0;
                if (ValidationErrors != null)
                {
                    foreach (var errors in ValidationErrors.Values)
                    {
                        count += errors?.Count ?? 0;
                    }
                }
                count += GlobalErrors?.Count ?? 0;
                return count;
            }
        }
    }

    /// <summary>
    /// Specialized error response for API endpoints with additional API-specific metadata
    /// </summary>
    public class ApiErrorResponse : ErrorResponse
    {
        /// <summary>
        /// API version that generated this error
        /// </summary>
        [JsonPropertyName("apiVersion")]
        public string ApiVersion { get; set; }

        /// <summary>
        /// Documentation URL for this error type
        /// </summary>
        [JsonPropertyName("documentationUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DocumentationUrl { get; set; }

        /// <summary>
        /// Suggested retry strategy for transient errors
        /// </summary>
        [JsonPropertyName("retryAfter")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? RetryAfter { get; set; }

        public ApiErrorResponse() : base()
        {
            ApiVersion = "1.0";
        }

        public ApiErrorResponse(int statusCode, string message, string traceId = null, string apiVersion = "1.0") 
            : base(statusCode, message, traceId)
        {
            ApiVersion = apiVersion;
        }

        /// <summary>
        /// Set retry after duration for rate limit errors
        /// </summary>
        public ApiErrorResponse WithRetryAfter(int seconds)
        {
            RetryAfter = seconds;
            return this;
        }

        /// <summary>
        /// Set documentation URL for error reference
        /// </summary>
        public ApiErrorResponse WithDocumentation(string url)
        {
            DocumentationUrl = url;
            return this;
        }
    }
}