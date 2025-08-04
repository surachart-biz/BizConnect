using BizConnect.Services.Models.Results;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for specialized Result types (OTAC, Registration, etc.)
/// Provides specific response formatting for different business operations
/// </summary>
public static class ActionResultExtensions
{
    #region OTAC Result Extensions

    /// <summary>
    /// Convert OtacResult to JSON response for OTAC generation/validation modals
    /// </summary>
    /// <param name="result">The OTAC result to convert</param>
    /// <returns>JsonResult formatted for OTAC operations</returns>
    public static JsonResult ToOtacJsonResult(this OtacResult result)
    {
        if (result.IsSuccess && result.Data != null)
        {
            var response = new
            {
                success = true,
                message = GetOtacSuccessMessage(result.Data.Purpose),
                code = result.Data.Code,
                registrationId = result.Data.RegistrationId,
                expiresAt = result.Data.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                purpose = result.Data.Purpose,
                remainingAttempts = result.Data.RemainingAttempts,
                deliveryMethod = result.Data.DeliveryMethod,
                deliveryDestination = result.Data.DeliveryDestination,
                traceId = result.TraceId,
                timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            return new JsonResult(response)
            {
                StatusCode = 200,
                ContentType = "application/json; charset=utf-8"
            };
        }

        // Handle error cases with Thai language support
        var errorResponse = new
        {
            success = false,
            message = GetOtacErrorMessage(result.ErrorMessage),
            errors = result.Errors.Where(e => !string.IsNullOrEmpty(e)).ToList(),
            remainingAttempts = result.RemainingAttempts,
            traceId = result.TraceId,
            timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        return new JsonResult(errorResponse)
        {
            StatusCode = 400,
            ContentType = "application/json; charset=utf-8"
        };
    }

    /// <summary>
    /// Convert OtacResult to redirect response for page-based operations
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The OTAC result</param>
    /// <param name="successAction">Action to redirect to on success</param>
    /// <param name="errorAction">Action to redirect to on error</param>
    /// <returns>Appropriate redirect or view result</returns>
    public static IActionResult HandleOtacResult(this Controller controller, OtacResult result, string successAction = "Index", string? errorAction = null)
    {
        if (result.IsSuccess && result.Data != null)
        {
            // Set success data in TempData
            controller.TempData["SuccessMessage"] = GetOtacSuccessMessage(result.Data.Purpose);
            controller.TempData["GeneratedCode"] = result.Data.Code;
            controller.TempData["RegistrationId"] = result.Data.RegistrationId;
            controller.TempData["ExpiresAt"] = result.Data.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC");

            return controller.RedirectToAction(successAction);
        }

        // Handle error
        controller.ModelState.AddModelError(string.Empty, GetOtacErrorMessage(result.ErrorMessage));
        
        if (!string.IsNullOrEmpty(errorAction))
        {
            return controller.RedirectToAction(errorAction);
        }

        return controller.View();
    }

    #endregion

    #region Registration Result Extensions

    /// <summary>
    /// Convert RegistrationResult to JSON response for AJAX registration calls
    /// </summary>
    /// <param name="result">The registration result to convert</param>
    /// <returns>JsonResult formatted for registration operations</returns>
    public static JsonResult ToRegistrationJsonResult(this RegistrationResult result)
    {
        if (result.IsSuccess && result.Data != null)
        {
            var response = new
            {
                success = true,
                message = "การลงทะเบียนเริ่มต้นสำเร็จ กำลังเปลี่ยนเส้นทางไปยังระบบของธนาคาร",
                redirectUrl = result.Data.RedirectUrl,
                externalReference = result.Data.ExternalReference,
                regId = result.Data.RegId,
                registrationId = result.Data.RegistrationId,
                status = result.Data.Status,
                createdAt = result.Data.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                traceId = result.TraceId,
                timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            return new JsonResult(response)
            {
                StatusCode = 200,
                ContentType = "application/json; charset=utf-8"
            };
        }

        // Handle error with Thai language support
        var errorResponse = new
        {
            success = false,
            message = GetRegistrationErrorMessage(result.ErrorMessage),
            errors = result.Errors.Where(e => !string.IsNullOrEmpty(e)).ToList(),
            traceId = result.TraceId,
            timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        return new JsonResult(errorResponse)
        {
            StatusCode = 400,
            ContentType = "application/json; charset=utf-8"
        };
    }

    /// <summary>
    /// Handle RegistrationResult for page-based registration flow
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The registration result</param>
    /// <returns>Redirect to KBank URL or error view</returns>
    public static IActionResult HandleRegistrationResult(this Controller controller, RegistrationResult result)
    {
        if (result.IsSuccess && result.Data != null)
        {
            // Clear any OTAC verification session
            controller.HttpContext.ClearOtacVerification();

            // Set success information in TempData for potential use
            controller.TempData["RegistrationId"] = result.Data.RegistrationId;
            controller.TempData["ExternalReference"] = result.Data.ExternalReference;

            // Redirect to KBank registration page
            return controller.Redirect(result.Data.RedirectUrl);
        }

        // Handle error - add to ModelState
        var errorMessage = GetRegistrationErrorMessage(result.ErrorMessage);
        controller.ModelState.AddModelError(string.Empty, errorMessage);

        // Add all errors to ModelState
        foreach (var error in result.Errors)
        {
            if (!string.IsNullOrEmpty(error) && error != result.ErrorMessage)
            {
                controller.ModelState.AddModelError(string.Empty, error);
            }
        }

        return controller.View();
    }

    #endregion

    #region Specialized Response Generators

    /// <summary>
    /// Generate OTAC modal response with validation state
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The OTAC result</param>
    /// <param name="modelState">Include ModelState validation in response</param>
    /// <returns>JsonResult for modal operations</returns>
    public static JsonResult ToOtacModalResult(this Controller controller, OtacResult result, bool includeValidation = true)
    {
        var baseResult = result.ToOtacJsonResult();
        
        if (!result.IsSuccess && includeValidation && !controller.ModelState.IsValid)
        {
            // Include validation errors in response
            var validationErrors = new Dictionary<string, List<string>>();
            foreach (var kvp in controller.ModelState)
            {
                if (kvp.Value.Errors.Count > 0)
                {
                    var fieldErrors = kvp.Value.Errors.Select(e => e.ErrorMessage).Where(e => !string.IsNullOrEmpty(e)).ToList();
                    if (fieldErrors.Any())
                    {
                        validationErrors[kvp.Key] = fieldErrors;
                    }
                }
            }

            // Merge validation errors into response
            var originalValue = baseResult.Value as dynamic;
            var enhancedResponse = new
            {
                success = false,
                message = originalValue?.message ?? "Please check your input and try again",
                errors = originalValue?.errors ?? new List<string>(),
                validationErrors,
                remainingAttempts = originalValue?.remainingAttempts,
                traceId = originalValue?.traceId,
                timestamp = originalValue?.timestamp
            };

            return new JsonResult(enhancedResponse)
            {
                StatusCode = 400,
                ContentType = "application/json; charset=utf-8"
            };
        }

        return baseResult;
    }

    /// <summary>
    /// Generate KBank-specific OTAC response for guest registration
    /// </summary>
    /// <param name="result">The OTAC result</param>
    /// <returns>JsonResult formatted for KBank ODD integration</returns>
    public static JsonResult ToKBankOtacResult(this OtacResult result)
    {
        if (result.IsSuccess && result.Data != null)
        {
            var response = new
            {
                success = true,
                message = "สร้างรหัส OTAC สำหรับการลงทะเบียน KBank ODD สำเร็จ",
                code = result.Data.Code,
                registrationId = result.Data.RegistrationId,
                expiresAt = result.Data.ExpiresAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                purpose = "KBank ODD Guest Registration",
                validationUrl = "/verify", // URL for guest to validate OTAC
                traceId = result.TraceId,
                timestamp = result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            return new JsonResult(response)
            {
                StatusCode = 200,
                ContentType = "application/json; charset=utf-8"
            };
        }

        return result.ToOtacJsonResult(); // Use standard OTAC error response
    }

    #endregion

    #region Message Helpers

    /// <summary>
    /// Get localized success message for OTAC operations
    /// </summary>
    /// <param name="purpose">The purpose of the OTAC</param>
    /// <returns>Localized success message</returns>
    private static string GetOtacSuccessMessage(string? purpose)
    {
        return purpose?.Contains("KBank", StringComparison.OrdinalIgnoreCase) == true
            ? "สร้างรหัส OTAC สำหรับการลงทะเบียน KBank ODD สำเร็จ!"
            : "สร้างรหัส OTAC สำเร็จ!";
    }

    /// <summary>
    /// Get localized error message for OTAC operations
    /// </summary>
    /// <param name="errorMessage">The original error message</param>
    /// <returns>Localized error message</returns>
    private static string GetOtacErrorMessage(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "เกิดข้อผิดพลาดในการสร้างรหัส OTAC กรุณาลองใหม่อีกครั้ง";

        // Map common error patterns to Thai messages
        if (errorMessage.Contains("expired", StringComparison.OrdinalIgnoreCase))
            return "รหัส OTAC หมดอายุแล้ว กรุณาขอรหัสใหม่";
        
        if (errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            return "รหัส OTAC ไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง";
        
        if (errorMessage.Contains("locked", StringComparison.OrdinalIgnoreCase))
            return "รหัส OTAC ถูกล็อกเนื่องจากใส่ผิดหลายครั้ง กรุณาขอรหัสใหม่";
        
        if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "ไม่พบรหัส OTAC หรือถูกใช้งานแล้ว";

        // Return original message with fallback
        return $"{errorMessage} กรุณาลองใหม่อีกครั้ง";
    }

    /// <summary>
    /// Get localized error message for registration operations
    /// </summary>
    /// <param name="errorMessage">The original error message</param>
    /// <returns>Localized error message</returns>
    private static string GetRegistrationErrorMessage(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return "เกิดข้อผิดพลาดในการลงทะเบียน กรุณาลองใหม่อีกครั้ง";

        // Map common error patterns to Thai messages
        if (errorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return "มีการลงทะเบียนข้อมูลนี้แล้ว กรุณาตรวจสอบข้อมูล";
        
        if (errorMessage.Contains("validation", StringComparison.OrdinalIgnoreCase))
            return "ข้อมูลที่กรอกไม่ถูกต้อง กรุณาตรวจสอบและลองใหม่";
        
        if (errorMessage.Contains("external service", StringComparison.OrdinalIgnoreCase))
            return "ระบบของธนาคารไม่สามารถเชื่อมต่อได้ กรุณาลองใหม่ภายหลัง";

        if (errorMessage.Contains("expired", StringComparison.OrdinalIgnoreCase))
            return "เซสชันหมดอายุ กรุณาเริ่มกระบวนการใหม่";

        // Return original message with fallback
        return $"{errorMessage} กรุณาลองใหม่อีกครั้ง";
    }

    #endregion

    #region Pagination Result Extensions

    /// <summary>
    /// Handle paginated results for admin data tables
    /// </summary>
    /// <typeparam name="T">The data type</typeparam>
    /// <param name="controller">The controller instance</param>
    /// <param name="result">The paginated result</param>
    /// <param name="viewName">Optional view name</param>
    /// <returns>View with pagination data or error</returns>
    public static IActionResult HandlePaginatedResult<T>(this Controller controller, Result<PagedResult<T>> result, string? viewName = null) where T : class
    {
        if (result.IsSuccess && result.Data != null)
        {
            return viewName != null ? controller.View(viewName, result.Data) : controller.View(result.Data);
        }

        // Handle error
        controller.TempData["ErrorMessage"] = result.ErrorMessage ?? "Unable to load data";
        
        // Create empty paged result for error display
        var emptyResult = new PagedResult<T>
        {
            Items = new List<T>(),
            CurrentPage = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0,
            HasPreviousPage = false,
            HasNextPage = false
        };

        return viewName != null ? controller.View(viewName, emptyResult) : controller.View(emptyResult);
    }

    #endregion
}

/// <summary>
/// Helper class for paginated results (matches existing service patterns)
/// </summary>
public class PagedResult<T> where T : class
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}