using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BizConnect.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Requests;
using BizConnect.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.Controllers;

public class HomeController : BaseController
{
    private readonly ILogger<HomeController> _logger;
    private readonly IOtacManagementService _otacService;
    private readonly IRegistrationManagementService _registrationService;
    private readonly IBranchService _branchService;

    public HomeController(ILogger<HomeController> logger, IOtacManagementService otacService, IRegistrationManagementService registrationService, IBranchService branchService)
    {
        _logger = logger;
        _otacService = otacService;
        _registrationService = registrationService;
        _branchService = branchService;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    #region OTAC Verification and Registration Flow

    /// <summary>
    /// Display OTAC verification form for guests
    /// </summary>
    [HttpGet("verify")]
    public IActionResult Verify()
    {
        return View(new VerifyOtacViewModel());
    }

    /// <summary>
    /// Process OTAC verification
    /// </summary>
    [HttpPost("verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(VerifyOtacViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var clientIp = HttpContext.GetClientIpAddress();
        var language = GetCurrentLanguage();
        var result = await _otacService.ValidateAsync(model.OtacCode, clientIp, language);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Guest OTAC verification successful: {OtacCode} from IP: {ClientIp}", 
                model.OtacCode, clientIp);

            // Store validated OTAC in session
            HttpContext.SetValidatedOtac(model.OtacCode);

            TempData["SuccessMessage"] = "รหัส OTAC ถูกต้อง กรุณากรอกข้อมูลสำหรับการลงทะเบียน";
            return RedirectToAction("Register");
        }
        else
        {
            _logger.LogWarning("Guest OTAC verification failed: {OtacCode} from IP: {ClientIp}, Error: {ErrorMessage}", 
                model.OtacCode, clientIp, result.ErrorMessage);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "รหัส OTAC ไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง");
        }

        return View(model);
    }

    /// <summary>
    /// Display registration form (requires validated OTAC)
    /// </summary>
    [HttpGet("register")]
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

        var model = new GuestRegistrationViewModel
        {
            OtacCode = validatedOtac,
            Branches = branches
        };

        return View(model);
    }

    /// <summary>
    /// Process registration form submission
    /// </summary>
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(GuestRegistrationViewModel model)
    {
        // Validate OTAC session
        var validatedOtac = HttpContext.GetValidatedOtac();
        if (string.IsNullOrEmpty(validatedOtac) || validatedOtac != model.OtacCode)
        {
            TempData["ErrorMessage"] = "Session หมดอายุ กรุณายืนยันรหัส OTAC ใหม่";
            return RedirectToAction("Verify");
        }

        if (!ModelState.IsValid)
        {
            // Reload branches with language support
            var languageForError = GetCurrentLanguage();
            var branchDataForError = await _branchService.GetActiveBranchesForDropdownAsync(languageForError);
            model.Branches = branchDataForError.Select(b => new SelectListItem 
            { 
                Value = b.BranchId.ToString(), 
                Text = b.Name 
            }).ToList();
            return View(model);
        }

        // Create registration request (excluding OTAC code - passed separately)
        var registrationRequest = new RegistrationRequest
        {
            FullName = model.FullName,
            IdType = "National ID", // Always National ID as per requirements
            IdValue = model.IdValue,
            MobileNo = model.MobileNo,
            AccountNo = model.AccountNo,
            BranchId = model.BranchId
        };

        // Submit registration using 3-phase flow (Phase 3: Submit validated OTAC with registration data)
        var result = await _registrationService.SubmitAsync(model.OtacCode, registrationRequest);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Guest registration started successfully for OTAC: {OtacCode}, External Reference: {ExternalReference}", 
                model.OtacCode, result.ExternalReference);

            // Clear session
            HttpContext.ClearOtacVerification();

            // Redirect to KBank registration page
            return Redirect(result.RedirectUrl ?? "/KBank/Pending");
        }
        else
        {
            _logger.LogWarning("Guest registration failed for OTAC: {OtacCode}, Error: {ErrorMessage}", 
                model.OtacCode, result.ErrorMessage);
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "เกิดข้อผิดพลาดในการลงทะเบียน กรุณาลองใหม่อีกครั้ง");
        }

        // Reload branches on error with language support
        var languageForReload = GetCurrentLanguage();
        var branchData = await _branchService.GetActiveBranchesForDropdownAsync(languageForReload);
        model.Branches = branchData.Select(b => new SelectListItem 
        { 
            Value = b.BranchId.ToString(), 
            Text = b.Name 
        }).ToList();

        return View(model);
    }

    #endregion

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View(new ErrorViewModel 
        { 
            RequestId = requestId,
            ShowRequestId = !string.IsNullOrEmpty(requestId) 
        });
    }





    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public new IActionResult NotFound()
    {
        Response.StatusCode = 404;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ServerError()
    {
        Response.StatusCode = 500;
        return View();
    }
}

/// <summary>
/// ViewModel for OTAC verification by guests
/// </summary>
public class VerifyOtacViewModel
{
    [Required(ErrorMessage = "กรุณากรอกรหัส OTAC")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "รหัส OTAC ต้องมี 8 ตัวอักษรเท่านั้น")]
    [Display(Name = "รหัส OTAC")]
    public string OtacCode { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for guest registration form
/// </summary>
public class GuestRegistrationViewModel
{
    [Required]
    public string OtacCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล")]
    [StringLength(200, ErrorMessage = "ชื่อ-นามสกุลต้องไม่เกิน 200 ตัวอักษร")]
    [Display(Name = "ชื่อ-นามสกุล")]
    public string FullName { get; set; } = string.Empty;

    // IdType is always "National ID" - no dropdown needed as per requirements
    public string IdType { get; set; } = "National ID";

    [Required(ErrorMessage = "กรุณากรอกเลขที่เอกสาร")]
    [StringLength(50, ErrorMessage = "เลขที่เอกสารต้องไม่เกิน 50 ตัวอักษร")]
    [Display(Name = "เลขที่เอกสาร")]
    public string IdValue { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเบอร์มือถือ")]
    [RegularExpression(@"^(08|09|\+66)[0-9]{8,9}$", ErrorMessage = "รูปแบบเบอร์มือถือไม่ถูกต้อง")]
    [Display(Name = "เบอร์มือถือ")]
    public string MobileNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเลขที่บัญชี")]
    [RegularExpression(@"^[0-9]{10,15}$", ErrorMessage = "เลขที่บัญชีต้องเป็นตัวเลข 10-15 หลัก")]
    [Display(Name = "เลขที่บัญชี")]
    public string AccountNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณาเลือกสาขา")]
    [Display(Name = "สาขา")]
    public int BranchId { get; set; }

    public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();

    // No IdTypes dropdown - always "National ID" as per requirements
}
