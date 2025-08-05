using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.Controllers;

/// <summary>
/// Controller for KBank Online Direct Debit (ODD) operations
/// </summary>
[Route("kbank")]
public class KBankController : BaseController
{
    private readonly IKbankOddService _kbankOddService;
    private readonly IOddRegistrationService _oddRegistrationService;
    private readonly IValidationService _validationService;
    private readonly IBranchService _branchService;
    private readonly ILogger<KBankController> _logger;

    public KBankController(
        IKbankOddService kbankOddService, 
        IOddRegistrationService oddRegistrationService,
        IValidationService validationService,
        IBranchService branchService,
        ILogger<KBankController> logger)
    {
        _kbankOddService = kbankOddService;
        _oddRegistrationService = oddRegistrationService;
        _validationService = validationService;
        _branchService = branchService;
        _logger = logger;
    }

    // DEPRECATED ENDPOINTS REMOVED - Guest registration now handled by HomeController
    // - /kbank/register/start -> /verify (HomeController)
    // - /kbank/register/form -> /register (HomeController)

    /// <summary>
    /// Displays the KBank ODD registration form (authenticated users)
    /// </summary>
    /// <returns>Registration form view</returns>
    [HttpGet("odd/register")]
    [Authorize] // Require authentication to start registration
    public async Task<IActionResult> Register()
    {
        _logger.LogInformation("User {UserId} accessed KBank ODD registration form", User.Identity?.Name);

        // Load active branches for dropdown
        var language = GetCurrentLanguage();
        var branchData = await _branchService.GetActiveBranchesForDropdownAsync(language);
        var branches = branchData.Select(b => new SelectListItem 
        { 
            Value = b.BranchId.ToString(), 
            Text = b.Name 
        }).ToList();

        var viewModel = new KBankOddRegisterViewModel
        {
            Branches = branches
        };

        return View(viewModel);
    }

    /// <summary>
    /// Processes the KBank ODD registration form submission and redirects user to KBank's registration page
    /// </summary>
    /// <param name="viewModel">Registration form data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirect to KBank registration page or form with validation errors</returns>
    [HttpPost("odd/register")]
    [Authorize] // Require authentication to start registration
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
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP status code based on processing result</returns>
    [HttpPost("status-update")]
    [IgnoreAntiforgeryToken] // KBank callback doesn't include antiforgery token
    public async Task<IActionResult> StatusUpdate([FromForm] StatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received KBank ODD status update: ExternalReference={ExternalReference}, Status={Status}, ReturnCode={ReturnCode}", 
                dto.ExternalReference, dto.ReturnStatus, dto.ReturnCode);

            // Find registration by external reference using new service method
            var registration = await _oddRegistrationService.GetRegistrationByExternalRefAsync(dto.ExternalReference);

            if (registration == null)
            {
                _logger.LogWarning("Registration not found for external reference: {ExternalReference}", dto.ExternalReference);
                return NotFound("Registration record not found");
            }

            // Map KBank status to our status
            var mappedStatus = dto.ReturnStatus == "0" ? "Success" : "Fail";

            // Use the new service to update registration status
            await _oddRegistrationService.UpdateRegistrationStatusAsync(
                registration.RegId, 
                mappedStatus, 
                dto.ReturnCode, 
                dto.EspaId);

            _logger.LogInformation("Successfully processed KBank status update for ExternalReference: {ExternalReference}", dto.ExternalReference);
            
            return Ok("Status updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD status update for ExternalReference: {ExternalReference}", dto.ExternalReference);
            
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Handles callback redirect from KBank after user completes registration
    /// </summary>
    /// <param name="ref">External reference to identify the registration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirect to success or failure page based on registration status</returns>
    [HttpGet("odd/callback")]
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
    [HttpGet("odd/success")]
    public IActionResult Success()
    {
        ViewData["Title"] = "Registration Successful";
        return View();
    }

    /// <summary>
    /// Registration failure page (optional - for user-friendly feedback)
    /// </summary>
    /// <returns>Failure view</returns>
    [HttpGet("odd/failure")]
    public IActionResult Failure()
    {
        ViewData["Title"] = "Registration Failed";
        return View();
    }

    /// <summary>
    /// Registration pending page - shown when registration is still being processed
    /// </summary>
    /// <returns>Pending view</returns>
    [HttpGet("odd/pending")]
    public IActionResult Pending()
    {
        ViewData["Title"] = "Registration Processing";
        return View();
    }
}
