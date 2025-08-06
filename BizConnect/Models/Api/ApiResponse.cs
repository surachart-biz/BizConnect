using System.Text.Json.Serialization;

namespace BizConnect.Models.Api;

/// <summary>
/// Generic API response wrapper that provides consistent response structure for all API endpoints.
/// Includes success status, data payload, error messages, and metadata for debugging.
/// </summary>
/// <typeparam name="T">The type of data being returned in the response</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The data payload of the response (null if operation failed)
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// Human-readable message describing the result
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// List of error messages (populated when Success is false)
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Unique identifier for tracing the request (useful for debugging)
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    /// <summary>
    /// Additional metadata about the response
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static ApiResponse<T> Ok(string? message = null)
    {
        return new ApiResponse<T>
        {
            IsSuccess = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates an error response with a single error message
    /// </summary>
    public static ApiResponse<T> Error(string errorMessage)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            Message = "Operation failed",
            Errors = new List<string> { errorMessage }
        };
    }

    /// <summary>
    /// Creates an error response with multiple error messages
    /// </summary>
    public static ApiResponse<T> Error(IEnumerable<string> errors)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            Message = "Operation failed",
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Creates an error response from an exception
    /// </summary>
    public static ApiResponse<T> Error(Exception exception)
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            Message = "An error occurred",
            Errors = new List<string> { exception.Message }
        };
    }

}

/// <summary>
/// Non-generic API response for operations that don't return data
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static new ApiResponse Ok(string? message = null)
    {
        return new ApiResponse
        {
            IsSuccess = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates an error response with a single error message
    /// </summary>
    public static new ApiResponse Error(string errorMessage)
    {
        return new ApiResponse
        {
            IsSuccess = false,
            Message = "Operation failed",
            Errors = new List<string> { errorMessage }
        };
    }

    /// <summary>
    /// Creates an error response with multiple error messages
    /// </summary>
    public static new ApiResponse Error(IEnumerable<string> errors)
    {
        return new ApiResponse
        {
            IsSuccess = false,
            Message = "Operation failed",
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Creates an error response from an exception
    /// </summary>
    public static new ApiResponse Error(Exception exception)
    {
        return new ApiResponse
        {
            IsSuccess = false,
            Message = "An error occurred",
            Errors = new List<string> { exception.Message }
        };
    }

}

/// <summary>
/// API response for validation errors with detailed field-level error information
/// </summary>
public class ValidationErrorResponse : ApiResponse
{
    /// <summary>
    /// Dictionary mapping field names to their validation error messages
    /// </summary>
    [JsonPropertyName("validationErrors")]
    public Dictionary<string, List<string>>? ValidationErrors { get; set; }

    /// <summary>
    /// Creates a validation error response from model state errors
    /// </summary>
    public static ValidationErrorResponse FromModelState(Dictionary<string, List<string>> modelStateErrors)
    {
        return new ValidationErrorResponse
        {
            IsSuccess = false,
            Message = "Validation failed",
            ValidationErrors = modelStateErrors,
            Errors = modelStateErrors.SelectMany(kvp => kvp.Value).ToList()
        };
    }
}