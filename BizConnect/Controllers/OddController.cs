using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Controllers;

/// <summary>
/// Controller for KBank Online Direct Debit (ODD) operations
/// </summary>
[Route("odd")]
public class OddController : Controller
{
    private readonly IKbankOddService _kbankOddService;
    private readonly ILogger<OddController> _logger;

    public OddController(IKbankOddService kbankOddService, ILogger<OddController> logger)
    {
        _kbankOddService = kbankOddService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates KBank ODD registration and redirects user to KBank's registration page
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirect to KBank registration page</returns>
    [HttpGet("register")]
    [Authorize] // Require authentication to start registration
    public async Task<IActionResult> Register(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("User {UserId} initiated KBank ODD registration", User.Identity?.Name);

            var redirectUrl = await _kbankOddService.StartRegistrationRedirectUrlAsync(cancellationToken);
            
            _logger.LogInformation("Redirecting user {UserId} to KBank registration page: {RedirectUrl}", 
                User.Identity?.Name, redirectUrl);

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate KBank ODD registration for user {UserId}", User.Identity?.Name);
            
            TempData["ErrorMessage"] = "Unable to initiate bank registration. Please try again later.";
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// Handles status update callback from KBank
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
}
