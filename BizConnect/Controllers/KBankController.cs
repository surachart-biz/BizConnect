using BizConnect.Dal;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Controllers;

/// <summary>
/// Controller for KBank Online Direct Debit (ODD) operations
/// </summary>
[Route("kbank")]
public class KBankController : Controller
{
    private readonly IKbankOddService _kbankOddService;
    private readonly ILogger<KBankController> _logger;
    private readonly BizConnectContext _context;

    public KBankController(IKbankOddService kbankOddService, ILogger<KBankController> logger, BizConnectContext context)
    {
        _kbankOddService = kbankOddService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Displays the KBank ODD registration form
    /// </summary>
    /// <returns>Registration form view</returns>
    [HttpGet("odd/register")]
    [Authorize] // Require authentication to start registration
    public IActionResult Register()
    {
        _logger.LogInformation("User {UserId} accessed KBank ODD registration form", User.Identity?.Name);

        var viewModel = new KBankOddRegisterViewModel();
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
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("User {UserId} submitted invalid KBank ODD registration form", User.Identity?.Name);
                return View(viewModel);
            }

            _logger.LogInformation("User {UserId} submitted valid KBank ODD registration form", User.Identity?.Name);

            // Map ViewModel to service request DTO
            var request = new OddRegistrationRequest
            {
                Email = viewModel.Email,
                MobileNo = viewModel.MobileNo,
                IdType = viewModel.IdType,
                IdValue = viewModel.IdValue
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
            return View(viewModel);
        }
    }

    /// <summary>
    /// Handles status update callback from KBank
    /// </summary>
    /// <param name="dto">Status update data from KBank</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP status code based on processing result</returns>
    [HttpPost("odd/status-update")]
    [IgnoreAntiforgeryToken] // KBank callback doesn't include antiforgery token
    public async Task<IActionResult> StatusUpdate([FromForm] StatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received KBank ODD status update for external reference: {ExternalReference}", 
                dto.ExternalReference);

            var result = await _kbankOddService.ProcessStatusUpdateAsync(dto, cancellationToken);

            return result switch
            {
                StatusProcessResult.Success => Ok("Status updated successfully"),
                StatusProcessResult.Fail => Ok("Registration failed - status updated"),
                StatusProcessResult.Unauthorized => Unauthorized("Invalid authentication"),
                StatusProcessResult.NotFound => NotFound("Registration record not found"),
                _ => BadRequest("Unknown processing result")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD status update for external reference: {ExternalReference}", 
                dto.ExternalReference);
            
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

            // Look up the registration status in the database
            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.ExternalReference == @ref, cancellationToken);

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
