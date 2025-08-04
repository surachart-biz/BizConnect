# Controller Extensions for Result Pattern Integration

This directory contains extension methods that seamlessly integrate the new Result Pattern services with the existing controller structure.

## Files

- **ControllerExtensions.cs** - Core extension methods for handling Result types
- **ActionResultExtensions.cs** - Specialized extensions for OTAC and Registration results
- **HttpContextExtensions.cs** - HTTP context utilities (existing)
- **ClaimsPrincipalExtensions.cs** - User claims utilities (existing)
- **ServiceCollectionExtensions.cs** - DI registration utilities (existing)

## Usage Examples

### Basic Result Handling

```csharp
// Handle Result<T> with automatic view/error handling
public async Task<IActionResult> Index()
{
    var result = await _service.GetDataAsync();
    return this.HandleResult(result); // Automatically shows view or errors
}

// Handle Result without data
public async Task<IActionResult> Delete(int id)
{
    var result = await _service.DeleteAsync(id);
    return this.HandleResult(result, "Index"); // Redirects to Index on success
}
```

### AJAX JSON Responses

```csharp
// Convert Result<T> to standardized JSON
[HttpPost]
public async Task<IActionResult> CreateData(CreateDataModel model)
{
    var result = await _service.CreateAsync(model);
    return result.ToJsonResult(); // Consistent JSON format
}

// OTAC-specific JSON responses
[HttpPost]
public async Task<IActionResult> GenerateOtac(GenerateOtacViewModel model)
{
    var result = await _otacService.GenerateAsync(userId, model.Purpose);
    return result.ToOtacJsonResult(); // OTAC-specific formatting with Thai messages
}
```

### Specialized Result Handling

```csharp
// Registration flow with automatic redirect
[HttpPost]
public async Task<IActionResult> Register(GuestRegistrationViewModel model)
{
    var result = await _registrationService.StartAsync(request);
    return this.HandleRegistrationResult(result); // Redirects to KBank or shows errors
}

// Modal responses with validation
[HttpPost]
public async Task<IActionResult> GenerateAjax(GenerateOtacViewModel model)
{
    if (!ModelState.IsValid)
    {
        return this.HandleValidationErrors(); // Standardized validation response
    }
    
    var result = await _otacService.GenerateAsync(userId, model.Purpose);
    return this.ToOtacModalResult(result); // Includes validation state
}
```

## Key Features

### 1. Consistent Response Format

All JSON responses follow this standard format:

```json
{
  "success": true|false,
  "data": {...},           // For successful operations with data
  "message": "string",     // User-friendly message
  "errors": ["..."],       // List of errors
  "traceId": "string",     // For debugging
  "timestamp": "string",   // ISO format timestamp
  "validationErrors": {    // For form validation
    "field": ["error1", "error2"]
  }
}
```

### 2. Thai Language Support

Error messages are automatically localized:

```csharp
// Automatic Thai localization for common error patterns
"Invalid OTAC code" → "รหัส OTAC ไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง"
"OTAC expired" → "รหัส OTAC หมดอายุแล้ว กรุณาขอรหัสใหม่"
"Registration failed" → "เกิดข้อผิดพลาดในการลงทะเบียน กรุณาลองใหม่อีกครั้ง"
```

### 3. ModelState Integration

Extension methods automatically handle validation errors:

```csharp
// Automatic ModelState integration
public async Task<IActionResult> Create(CreateModel model)
{
    var result = await _service.CreateAsync(model);
    
    if (this.TryHandleResult(result, out var data))
    {
        // Success - data is available
        return View(data);
    }
    
    // Error - ModelState already populated with errors
    return View(model);
}
```

### 4. TempData Helpers

Simplified success/error message handling:

```csharp
// Set messages in TempData
this.SetSuccessMessage("Operation completed successfully");
this.SetErrorMessageFromResult(result);
```

## Migration Examples

### Before (Current Pattern)

```csharp
[HttpPost]
public async Task<IActionResult> GenerateAjax(GenerateOtacViewModel model)
{
    if (!ModelState.IsValid)
    {
        return Json(new { 
            success = false, 
            message = "Please check your input and try again.",
            errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
        });
    }

    var result = await _otacService.GenerateAsync(userId, model.Purpose);
    
    if (result.IsSuccess)
    {
        return Json(new { 
            success = true, 
            message = "OTAC code generated successfully!",
            code = result.Code,
            registrationId = result.RegistrationId,
            expiresAt = result.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss UTC")
        });
    }
    else
    {
        return Json(new { 
            success = false, 
            message = result.ErrorMessage ?? "An error occurred..."
        });
    }
}
```

### After (With Extensions)

```csharp
[HttpPost]
public async Task<IActionResult> GenerateAjax(GenerateOtacViewModel model)
{
    if (!ModelState.IsValid)
    {
        return this.HandleValidationErrors();
    }

    var result = await _otacService.GenerateAsync(userId, model.Purpose);
    return result.ToOtacJsonResult();
}
```

## Integration with Existing Controllers

### Admin/OtacController Integration

```csharp
// Existing GenerateAjax method can be simplified
[HttpPost]
public async Task<IActionResult> GenerateAjax(GenerateOtacViewModel model)
{
    if (!ModelState.IsValid)
        return this.HandleValidationErrors();

    var userId = User.GetUserId();
    if (userId == 0)
        return this.CreateErrorResponse("Unable to identify user. Please log in again.");

    var result = await _otacService.GenerateAsync(userId, model.Purpose);
    return result.ToOtacJsonResult();
}

// KBank-specific generation
[HttpPost]
public async Task<IActionResult> GenerateForKBankOdd()
{
    var userId = User.GetUserId();
    if (userId == 0)
        return this.CreateErrorResponse("Unable to identify user. Please log in again.");

    var result = await _otacService.GenerateAsync(userId, "KBank ODD Guest Registration");
    return result.ToKBankOtacResult(); // KBank-specific formatting
}
```

### HomeController Integration

```csharp
// Registration flow simplified
[HttpPost("register")]
public async Task<IActionResult> Register(GuestRegistrationViewModel model)
{
    var validatedOtac = HttpContext.GetValidatedOtac();
    if (string.IsNullOrEmpty(validatedOtac) || validatedOtac != model.OtacCode)
    {
        TempData["ErrorMessage"] = "Session หมดอายุ กรุณายืนยันรหัส OTAC ใหม่";
        return RedirectToAction("Verify");
    }

    if (!ModelState.IsValid)
    {
        // Reload branches and return view
        await LoadBranches(model);
        return View(model);
    }

    var registrationRequest = new RegistrationRequest { /* ... */ };
    var result = await _registrationService.StartAsync(registrationRequest);
    
    return this.HandleRegistrationResult(result); // Automatic redirect or error handling
}
```

### Admin/OddRegistrationController Integration

```csharp
// Simplified index with pagination
public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string status = "", string search = "")
{
    var result = await _registrationQuery.GetPagedAsync(page, pageSize, status, search);
    return this.HandlePaginatedResult(result); // Handles pagination and errors
}
```

## Best Practices

1. **Always use extension methods** for Result handling instead of manual conversion
2. **Leverage Thai localization** for user-facing messages
3. **Include validation state** in modal responses
4. **Use consistent JSON format** across all AJAX endpoints
5. **Handle ModelState automatically** with TryHandleResult pattern
6. **Set appropriate HTTP status codes** (200 for success, 400 for client errors)
7. **Include trace IDs** for debugging and error correlation

## Error Handling Integration

The extensions work seamlessly with the Global Exception Middleware:

1. **Infrastructure errors** are caught by the middleware
2. **Business logic errors** are handled by Result Pattern
3. **Validation errors** are managed by ModelState integration
4. **User-friendly messages** are provided in Thai language
5. **Trace IDs** are maintained throughout the request chain