using BizConnect.Extensions;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.ViewModels;
using BizConnect.ViewModels.Modern;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.Controllers;

/// <summary>
/// Controller for KBank Online Direct Debit (ODD) operations
/// </summary>
[AllowAnonymous]
[Route("kbank/odd")]
public class KBankController : BaseController
{
    private readonly IKbankOddService _kbankOddService;
    private readonly IOddRegistrationService _oddRegistrationService;
    private readonly IValidationService _validationService;
    private readonly IBranchService _branchService;
    private readonly IOtacManagementService _otacService;
    private readonly ILogger<KBankController> _logger;

    public KBankController(
        IKbankOddService kbankOddService, 
        IOddRegistrationService oddRegistrationService,
        IValidationService validationService,
        IBranchService branchService,
        IOtacManagementService otacService,
        ILogger<KBankController> logger)
    {
        _kbankOddService = kbankOddService;
        _otacService = otacService;
        _oddRegistrationService = oddRegistrationService;
        _validationService = validationService;
        _branchService = branchService;
        _logger = logger;
    }

    // DEPRECATED ENDPOINTS REMOVED - Guest registration now handled by HomeController
    // - /kbank/register/start -> /verify (HomeController)
    // - /kbank/register/form -> /register (HomeController)
    // Current authenticated endpoints: /kbank/odd/register, /kbank/odd/callback, /kbank/odd/status-update

    /// <summary>
    /// Display registration form (requires validated OTAC)
    /// </summary>
    [HttpGet("register")]
    /// <summary>
    /// Display enhanced registration form with modern UI features
    /// </summary>
    public async Task<IActionResult> Register()
    {
        // Check if OTAC is validated
        var validatedOtac = HttpContext.GetValidatedOtac();
        if (string.IsNullOrEmpty(validatedOtac))
        {
            TempData["ErrorMessage"] = "กรุณายืนยันรหัส OTAC ก่อนการลงทะเบียน";
            return RedirectToAction("Verify");
        }

        // Check if OTAC is still valid
        var language = GetCurrentLanguage();
        var validationResult = await _otacService.IsValidAsync(validatedOtac, language);
        if (!validationResult.IsValid)
        {
            HttpContext.ClearOtacVerification();
            TempData["ErrorMessage"] = "รหัส OTAC หมดอายุแล้ว กรุณาขอรหัสใหม่";
            return RedirectToAction("Verify");
        }

        // Load branches for dropdown with language support
        var branchData = await _branchService.GetActiveBranchesForDropdownAsync(language);
        var branches = branchData.Select(b => new SelectListItem
        {
            Value = b.BranchId.ToString(),
            Text = b.Name
        }).ToList();

        var model = new ModernRegistrationViewModel
        {
            OtacCode = validatedOtac,
            Branches = branches,
            Progress = new RegistrationProgress
            {
                CurrentStep = 2,
                TotalSteps = 3,
                PercentComplete = 67,
                Steps = new List<ProgressStep>
                {
                    new ProgressStep { StepNumber = 1, Title = "OTAC Verification", Status = "completed", Description = "รหัสยืนยัน" },
                    new ProgressStep { StepNumber = 2, Title = "Information Entry", Status = "active", Description = "กรอกข้อมูล" },
                    new ProgressStep { StepNumber = 3, Title = "Processing", Status = "pending", Description = "ประมวลผล" }
                }
            },
            SecurityInfo = new FormSecurityInfo
            {
                SecurityLevel = "High",
                IsEncrypted = true,
                SecurityIndicators = new List<SecurityIndicator>
                {
                    new SecurityIndicator { Type = "SSL", Status = "Active", Description = "Secure Connection", IconClass = "fas fa-shield-alt" },
                    new SecurityIndicator { Type = "Encryption", Status = "Active", Description = "256-bit Encryption", IconClass = "fas fa-lock" }
                }
            },
            EstimatedProcessingTime = "2-3 minutes"
        };

        return View(model);
    }

    /// <summary>
    /// Processes the KBank ODD registration form submission and redirects user to KBank's registration page
    /// </summary>
    /// <param name="viewModel">Registration form data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirect to KBank registration page or form with validation errors</returns>
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(KBankOddRegisterViewModel viewModel, CancellationToken cancellationToken = default)
    {
        try
        {
            // Additional custom validation using validation service (business logic in service layer)
            if (!string.IsNullOrEmpty(viewModel.IdType) && !string.IsNullOrEmpty(viewModel.IdValue))
            {
                var idValidationResult = _validationService.ValidateIdValue(viewModel.IdType, viewModel.IdValue);
                if (!idValidationResult.IsValid)
                {
                    ModelState.AddModelError(nameof(viewModel.IdValue), idValidationResult.ErrorMessage);
                }
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User {UserId} submitted invalid KBank ODD registration form", User.Identity?.Name);
                
                // Reload branches for dropdown
                var language = GetCurrentLanguage();
                var branchData = await _branchService.GetActiveBranchesForDropdownAsync(language);
                viewModel.Branches = branchData.Select(b => new SelectListItem 
                { 
                    Value = b.BranchId.ToString(), 
                    Text = b.Name 
                }).ToList();
                
                return View(viewModel);
            }

            _logger.LogInformation("User {UserId} submitted valid KBank ODD registration form", User.Identity?.Name);

            // Map ViewModel to service request DTO (V1.9.7 - no email)
            var request = new OddRegistrationRequest
            {
                FullName = viewModel.FullName,
                MobileNo = viewModel.MobileNo,
                IdType = viewModel.IdType,
                IdValue = viewModel.IdValue,
                AccountNo = viewModel.AccountNo,
                BranchId = viewModel.BranchId
            };

            var redirectUrl = await _kbankOddService.StartRegistrationAsync(request, cancellationToken);

            _logger.LogInformation("Redirecting user {UserId} to KBank registration page: {RedirectUrl}",
                User.Identity?.Name, redirectUrl);

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD registration for user {UserId}", User.Identity?.Name);
            ModelState.AddModelError(string.Empty, "Unable to process registration. Please try again later.");
            
            // Reload branches for dropdown
            var languageForError = GetCurrentLanguage();
            var branchData = await _branchService.GetActiveBranchesForDropdownAsync(languageForError);
            viewModel.Branches = branchData.Select(b => new SelectListItem 
            { 
                Value = b.BranchId.ToString(), 
                Text = b.Name 
            }).ToList();
            
            return View(viewModel);
        }
    }

    /// <summary>
    /// Handles status update callback from KBank (V1.9.7)
    /// Supports both JSON and form data for flexibility
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP status code based on processing result</returns>
    [HttpPost("status-update")]
    [IgnoreAntiforgeryToken] // KBank callback doesn't include antiforgery token
    public async Task<IActionResult> StatusUpdate([FromBody] StatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _kbankOddService.ProcessStatusUpdateAsync(dto, cancellationToken);
            
            return result switch
            {
                StatusProcessResult.Success => Ok(new { success = true, message = "Status updated successfully", timestamp = DateTime.UtcNow }),
                StatusProcessResult.NotFound => NotFound(new { success = false, message = "Registration record not found" }),
                StatusProcessResult.Unauthorized => Unauthorized(new { success = false, message = "Invalid authentication" }),
                StatusProcessResult.Fail => BadRequest(new { success = false, message = "Status update failed" }),
                _ => StatusCode(500, new { success = false, message = "Unknown processing result" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD status update for ExternalReference: {ExternalReference}", dto.ExternalReference);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Handles status update callback from KBank using form data format
    /// Alternative endpoint for form-based callbacks
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP status code based on processing result</returns>
    [HttpPost("status-update-form")]
    [IgnoreAntiforgeryToken] // KBank callback doesn't include antiforgery token
    public async Task<IActionResult> StatusUpdateForm([FromForm] StatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _kbankOddService.ProcessStatusUpdateAsync(dto, cancellationToken);
            
            return result switch
            {
                StatusProcessResult.Success => Ok(new { success = true, message = "Status updated successfully", timestamp = DateTime.UtcNow }),
                StatusProcessResult.NotFound => NotFound(new { success = false, message = "Registration record not found" }),
                StatusProcessResult.Unauthorized => Unauthorized(new { success = false, message = "Invalid authentication" }),
                StatusProcessResult.Fail => BadRequest(new { success = false, message = "Status update failed" }),
                _ => StatusCode(500, new { success = false, message = "Unknown processing result" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD status update (form) for ExternalReference: {ExternalReference}", dto.ExternalReference);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Handles callback redirect from KBank after user completes registration
    /// </summary>
    /// <param name="ref">External reference to identify the registration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirect to success or failure page based on registration status</returns>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? @ref, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(@ref))
            {
                _logger.LogWarning("KBank callback received without external reference");
                return RedirectToAction("Failure");
            }

            _logger.LogInformation("Processing KBank callback for external reference: {ExternalReference}", @ref);

            // Look up the registration status using the service
            var registration = await _oddRegistrationService.GetRegistrationByExternalRefAsync(@ref);

            if (registration == null)
            {
                _logger.LogWarning("Registration record not found for external reference: {ExternalReference}", @ref);
                return RedirectToAction("Failure");
            }

            // Redirect based on registration status
            return registration.Status switch
            {
                "Success" => RedirectToAction("Success"),
                "Fail" => RedirectToAction("Failure"),
                _ => RedirectToAction("Pending") // For pending status, show a "processing" page
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank callback for external reference: {ExternalReference}", @ref);
            return RedirectToAction("Failure");
        }
    }

    /// <summary>
    /// Registration success page (optional - for user-friendly feedback)
    /// </summary>
    /// <returns>Success view</returns>
    [HttpGet("success")]
    public IActionResult Success()
    {
        ViewData["Title"] = "Registration Successful";
        return View();
    }

    /// <summary>
    /// Registration failure page (optional - for user-friendly feedback)
    /// </summary>
    /// <returns>Failure view</returns>
    [HttpGet("failure")]
    public IActionResult Failure()
    {
        ViewData["Title"] = "Registration Failed";
        return View();
    }

    /// <summary>
    /// Registration pending page - shown when registration is still being processed
    /// </summary>
    /// <returns>Pending view</returns>
    [HttpGet("pending")]
    public IActionResult Pending()
    {
        ViewData["Title"] = "Registration Processing";
        return View();
    }
}
