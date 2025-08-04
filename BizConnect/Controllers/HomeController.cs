using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BizConnect.Models;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IOddRegistrationService _oddRegistrationService;
    private readonly IBranchService _branchService;

    public HomeController(ILogger<HomeController> logger, IOddRegistrationService oddRegistrationService, IBranchService branchService)
    {
        _logger = logger;
        _oddRegistrationService = oddRegistrationService;
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

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var (isValid, errorMessage) = await _oddRegistrationService.ValidateOtacAsync(model.OtacCode, clientIp);

            if (isValid)
            {
                _logger.LogInformation("Guest OTAC verification successful: {OtacCode} from IP: {ClientIp}", 
                    model.OtacCode, clientIp);

                // Store validated OTAC in session
                HttpContext.Session.SetString("validated_otac", model.OtacCode);
                HttpContext.Session.SetString("otac_validated_at", DateTime.UtcNow.ToString());

                TempData["SuccessMessage"] = "รหัส OTAC ถูกต้อง กรุณากรอกข้อมูลสำหรับการลงทะเบียน";
                return RedirectToAction("Register");
            }
            else
            {
                _logger.LogWarning("Guest OTAC verification failed: {OtacCode} from IP: {ClientIp}, Error: {ErrorMessage}", 
                    model.OtacCode, clientIp, errorMessage);
                ModelState.AddModelError(string.Empty, errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during guest OTAC verification: {OtacCode}", model.OtacCode);
            ModelState.AddModelError(string.Empty, "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง");
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
        var validatedOtac = HttpContext.Session.GetString("validated_otac");
        if (string.IsNullOrEmpty(validatedOtac))
        {
            TempData["ErrorMessage"] = "กรุณายืนยันรหัส OTAC ก่อนการลงทะเบียน";
            return RedirectToAction("Verify");
        }

        // Check if OTAC is still valid
        if (!await _oddRegistrationService.IsOtacValidAsync(validatedOtac))
        {
            HttpContext.Session.Remove("validated_otac");
            HttpContext.Session.Remove("otac_validated_at");
            TempData["ErrorMessage"] = "รหัส OTAC หมดอายุแล้ว กรุณาขอรหัสใหม่";
            return RedirectToAction("Verify");
        }

        // Load branches for dropdown
        var branchData = await _branchService.GetActiveBranchesForDropdownAsync();
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
        var validatedOtac = HttpContext.Session.GetString("validated_otac");
        if (string.IsNullOrEmpty(validatedOtac) || validatedOtac != model.OtacCode)
        {
            TempData["ErrorMessage"] = "Session หมดอายุ กรุณายืนยันรหัส OTAC ใหม่";
            return RedirectToAction("Verify");
        }

        if (!ModelState.IsValid)
        {
            // Reload branches
            var branchDataForError = await _branchService.GetActiveBranchesForDropdownAsync();
            model.Branches = branchDataForError.Select(b => new SelectListItem 
            { 
                Value = b.BranchId.ToString(), 
                Text = b.Name 
            }).ToList();
            return View(model);
        }

        try
        {
            // Create registration form data (IdType is always "National ID")
            var formData = new RegistrationFormData
            {
                FullName = model.FullName,
                IdType = "National ID", // Always National ID as per requirements
                IdValue = model.IdValue,
                MobileNo = model.MobileNo,
                AccountNo = model.AccountNo,
                BranchId = model.BranchId
            };

            // Start registration process
            var (isSuccess, result) = await _oddRegistrationService.StartRegistrationAsync(model.OtacCode, formData);

            if (isSuccess)
            {
                _logger.LogInformation("Guest registration started successfully for OTAC: {OtacCode}", model.OtacCode);

                // Clear session
                HttpContext.Session.Remove("validated_otac");
                HttpContext.Session.Remove("otac_validated_at");

                // Redirect to KBank registration page
                return Redirect(result);
            }
            else
            {
                _logger.LogWarning("Guest registration failed for OTAC: {OtacCode}, Error: {ErrorMessage}", 
                    model.OtacCode, result);
                ModelState.AddModelError(string.Empty, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during guest registration for OTAC: {OtacCode}", model.OtacCode);
            ModelState.AddModelError(string.Empty, "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง");
        }

        // Reload branches on error
        var branchData = await _branchService.GetActiveBranchesForDropdownAsync();
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
