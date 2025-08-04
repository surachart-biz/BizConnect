using System.Text.Json;
using BizConnect.Services.Models.Results;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for Controller to handle Result Pattern integration
/// Provides seamless conversion from Result types to appropriate ActionResult responses
/// </summary>
public static class ControllerExtensions
{
    #region Basic Result Handling

    /// <summary>
    /// Handle Result&lt;T&gt; for data operations with automatic response selection
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The result to handle</param>
    /// <param name="successView">Optional view name for successful operations (defaults to current action)</param>
    /// <param name="errorView">Optional view name for error operations (defaults to current action)</param>
    /// <returns>Appropriate ActionResult based on result status</returns>
    public static IActionResult HandleResult<T>(this Controller controller, Result<T> result, string? successView = null, string? errorView = null) where T : class
    {
        if (result.IsSuccess && result.Data != null)
        {
            return successView != null ? controller.View(successView, result.Data) : controller.View(result.Data);
        }

        // Handle error - add to ModelState for form display
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            controller.ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        foreach (var error in result.Errors)
        {
            if (!string.IsNullOrEmpty(error) && error != result.ErrorMessage)
            {
                controller.ModelState.AddModelError(string.Empty, error);
            }
        }

        // Return error view with empty model or original model if available
        var model = result.Data ?? (T?)controller.ViewData.Model;
        return errorView != null ? controller.View(errorView, model) : controller.View(model);
    }

    /// <summary>
    /// Handle Result for operations without return data
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The result to handle</param>
    /// <param name="successAction">Action to redirect to on success (defaults to Index)</param>
    /// <param name="successController">Controller for success redirect (defaults to current)</param>
    /// <param name="errorView">View to show on error (defaults to current action)</param>
    /// <returns>Appropriate ActionResult based on result status</returns>
    public static IActionResult HandleResult(this Controller controller, Result result, string successAction = "Index", string? successController = null, string? errorView = null)
    {
        if (result.IsSuccess)
        {
            return successController != null 
                ? controller.RedirectToAction(successAction, successController)
                : controller.RedirectToAction(successAction);
        }

        // Handle error - add to ModelState
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            controller.ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        foreach (var error in result.Errors)
        {
            if (!string.IsNullOrEmpty(error) && error != result.ErrorMessage)
            {
                controller.ModelState.AddModelError(string.Empty, error);
            }
        }

        // Return error view with current model
        return errorView != null ? controller.View(errorView) : controller.View();
    }

    #endregion

    #region JSON Result Handling

    /// <summary>
    /// Convert Result&lt;T&gt; to standardized JSON response for AJAX calls
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    /// <param name="result">The result to convert</param>
    /// <returns>JsonResult with standardized response format</returns>
    public static JsonResult ToJsonResult<T>(this Result<T> result) where T : class
    {
        var response = new
        {
            success = result.IsSuccess,
            data = result.Data,
            message = result.ErrorMessage ?? (result.IsSuccess ? "Operation completed successfully" : "Operation failed"),
            errors = result.Errors.Where(e => !string.IsNullOrEmpty(e)).ToList(),
            traceId = result.TraceId,
            timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        return new JsonResult(response)
        {
            StatusCode = result.IsSuccess ? 200 : 400,
            ContentType = "application/json; charset=utf-8"
        };
    }

    /// <summary>
    /// Convert Result to standardized JSON response for AJAX calls
    /// </summary>
    /// <param name="result">The result to convert</param>
    /// <returns>JsonResult with standardized response format</returns>
    public static JsonResult ToJsonResult(this Result result)
    {
        var response = new
        {
            success = result.IsSuccess,
            message = result.ErrorMessage ?? (result.IsSuccess ? "Operation completed successfully" : "Operation failed"),
            errors = result.Errors.Where(e => !string.IsNullOrEmpty(e)).ToList(),
            traceId = result.TraceId,
            timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        return new JsonResult(response)
        {
            StatusCode = result.IsSuccess ? 200 : 400,
            ContentType = "application/json; charset=utf-8"
        };
    }

    #endregion

    #region ModelState Integration

    /// <summary>
    /// Add validation errors from Result to ModelState
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The result containing validation errors</param>
    public static void AddValidationErrors<T>(this Controller controller, Result<T> result) where T : class
    {
        if (!result.IsSuccess)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                controller.ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }

            foreach (var error in result.Errors)
            {
                if (!string.IsNullOrEmpty(error) && error != result.ErrorMessage)
                {
                    controller.ModelState.AddModelError(string.Empty, error);
                }
            }
        }
    }

    /// <summary>
    /// Add validation errors from Result to ModelState
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The result containing validation errors</param>
    public static void AddValidationErrors(this Controller controller, Result result)
    {
        if (!result.IsSuccess)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                controller.ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }

            foreach (var error in result.Errors)
            {
                if (!string.IsNullOrEmpty(error) && error != result.ErrorMessage)
                {
                    controller.ModelState.AddModelError(string.Empty, error);
                }
            }
        }
    }

    /// <summary>
    /// Try to handle result and extract data if successful
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The result to handle</param>
    /// <param name="data">The extracted data if successful</param>
    /// <returns>True if result was successful and data was extracted</returns>
    public static bool TryHandleResult<T>(this Controller controller, Result<T> result, out T? data) where T : class
    {
        data = null;

        if (result.IsSuccess && result.Data != null)
        {
            data = result.Data;
            return true;
        }

        // Add errors to ModelState
        controller.AddValidationErrors(result);
        return false;
    }

    #endregion

    #region TempData Helpers

    /// <summary>
    /// Set success message in TempData from successful result
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="message">Success message (optional, uses default if not provided)</param>
    public static void SetSuccessMessage(this Controller controller, string? message = null)
    {
        controller.TempData["SuccessMessage"] = message ?? "Operation completed successfully";
    }

    /// <summary>
    /// Set error message in TempData from failed result
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The failed result</param>
    public static void SetErrorMessageFromResult<T>(this Controller controller, Result<T> result) where T : class
    {
        if (!result.IsSuccess)
        {
            controller.TempData["ErrorMessage"] = result.ErrorMessage ?? "An error occurred";
        }
    }

    /// <summary>
    /// Set error message in TempData from failed result
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The failed result</param>
    public static void SetErrorMessageFromResult(this Controller controller, Result result)
    {
        if (!result.IsSuccess)
        {
            controller.TempData["ErrorMessage"] = result.ErrorMessage ?? "An error occurred";
        }
    }

    #endregion

    #region Response Helpers

    /// <summary>
    /// Create standardized response object for JSON serialization
    /// </summary>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="message">Response message</param>
    /// <param name="data">Response data (optional)</param>
    /// <param name="errors">Error list (optional)</param>
    /// <param name="traceId">Trace ID for debugging (optional)</param>
    /// <returns>Anonymous object suitable for JSON response</returns>
    public static object CreateStandardResponse(bool success, string message, object? data = null, List<string>? errors = null, string? traceId = null)
    {
        return new
        {
            success,
            message,
            data,
            errors = errors ?? new List<string>(),
            traceId = traceId ?? Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };
    }

    /// <summary>
    /// Create error response with Thai language support
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="statusCode">HTTP status code (defaults to 400)</param>
    /// <returns>JsonResult with error response</returns>
    public static JsonResult CreateErrorResponse(this Controller controller, string errorMessage, int statusCode = 400)
    {
        var response = CreateStandardResponse(
            success: false,
            message: errorMessage,
            errors: new List<string> { errorMessage }
        );

        return new JsonResult(response)
        {
            StatusCode = statusCode,
            ContentType = "application/json; charset=utf-8"
        };
    }

    /// <summary>
    /// Create success response with optional data
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="message">Success message</param>
    /// <param name="data">Response data (optional)</param>
    /// <returns>JsonResult with success response</returns>
    public static JsonResult CreateSuccessResponse(this Controller controller, string message, object? data = null)
    {
        var response = CreateStandardResponse(
            success: true,
            message: message,
            data: data
        );

        return new JsonResult(response)
        {
            StatusCode = 200,
            ContentType = "application/json; charset=utf-8"
        };
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Create JSON response for validation errors with ModelState integration
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="message">Main validation error message</param>
    /// <returns>JsonResult with validation error response</returns>
    public static JsonResult HandleValidationErrors(this Controller controller, string? message = null)
    {
        var validationErrors = new Dictionary<string, List<string>>();
        var allErrors = new List<string>();

        foreach (var kvp in controller.ModelState)
        {
            if (kvp.Value.Errors.Count > 0)
            {
                var fieldErrors = kvp.Value.Errors.Select(e => e.ErrorMessage).Where(e => !string.IsNullOrEmpty(e)).ToList();
                if (fieldErrors.Any())
                {
                    validationErrors[kvp.Key] = fieldErrors;
                    allErrors.AddRange(fieldErrors);
                }
            }
        }

        var response = new
        {
            success = false,
            message = message ?? "Please check your input and try again",
            errors = allErrors,
            validationErrors,
            traceId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        return new JsonResult(response)
        {
            StatusCode = 400,
            ContentType = "application/json; charset=utf-8"
        };
    }

    #endregion
}