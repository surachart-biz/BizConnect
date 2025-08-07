using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using BizConnect.Models;
using BizConnect.Models.ViewModels;
using BizConnect.Services.DTOs;
using BizConnect.Models.Api;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Requests;
using BizConnect.Extensions;
using BizConnect.ViewModels.Modern;
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
    // Note: Trust and SystemHealth services to be implemented
    // private readonly ITrustService _trustService;
    // private readonly ISystemHealthService _systemHealthService;

    public HomeController(
        ILogger<HomeController> logger, 
        IOtacManagementService otacService, 
        IRegistrationManagementService registrationService, 
        IBranchService branchService)
    {
        _logger = logger;
        _otacService = otacService;
        _registrationService = registrationService;
        _branchService = branchService;
    }

    /// <summary>
    /// Modern landing page with enhanced UI features and trust indicators
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var model = new LandingPageViewModel
        {
            OtacCode = string.Empty
        };

        return View(model);
    }

    

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtac(LandingPageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        try
        {
            // Phase 1: Simple validation - จะเชื่อมต่อ service จริงใน Phase ถัดไป
            var otacCode = model.OtacCode.ToUpper().Trim();

            // Basic format validation
            if (string.IsNullOrEmpty(otacCode) || otacCode.Length < 6 || otacCode.Length > 8)
            {
                ModelState.AddModelError("OtacCode", "รหัส OTAC ต้องมีความยาว 6-8 ตัวอักษร");
                return View("Index", model);
            }

            // Check if OTAC contains only alphanumeric characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(otacCode, @"^[A-Z0-9]+$"))
            {
                ModelState.AddModelError("OtacCode", "รหัส OTAC ต้องเป็นตัวอักษรภาษาอังกฤษตัวใหญ่และตัวเลขเท่านั้น");
                return View("Index", model);
            }

            // Phase 1: Demo validation - accept certain test codes
            var validTestCodes = new[] { "ABC12345", "TEST1234", "DEMO5678", "OTAC9999" };

            if (validTestCodes.Contains(otacCode))
            {
                // Success - store OTAC for next step
                TempData["OtacCode"] = otacCode;
                TempData["SuccessMessage"] = $"รหัส OTAC {otacCode} ยืนยันสำเร็จ! กำลังเปลี่ยนเส้นทางไปยังฟอร์มลงทะเบียน...";

                // Phase 1: Redirect to a success page or registration form
                // In real implementation: return RedirectToAction("Register", "Registration", new { otac = otacCode });
                return RedirectToAction("RegistrationSuccess", new { otac = otacCode });
            }
            else
            {
                // Invalid OTAC
                ModelState.AddModelError("OtacCode", "รหัส OTAC ไม่ถูกต้องหรือหมดอายุแล้ว กรุณาติดต่อเจ้าหน้าที่");
                return View("Index", model);
            }
        }
        catch (Exception ex)
        {
            // Log error in real implementation
            ModelState.AddModelError("", "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง");
            return View("Index", model);
        }
    }

    // Temporary success page for Phase 1 demo
    public IActionResult RegistrationSuccess(string otac)
    {
        ViewBag.OtacCode = otac;
        ViewBag.Message = $"รหัส OTAC {otac} ยืนยันสำเร็จ!";
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
    /// Process OTAC verification with enhanced error handling and real-time feedback
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
    /// API endpoint for real-time OTAC verification (AJAX support)
    /// </summary>
    [HttpPost("api/verify-otac")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtacApi([FromBody] VerifyOtacRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.OtacCode))
            {
                return BadRequest(ApiResponse<BizConnect.ViewModels.Modern.VerifyOtacResponse>.Error("OTAC code is required"));
            }

            var clientIp = HttpContext.GetClientIpAddress();
            var language = request.Language ?? GetCurrentLanguage();
            
            var result = await _otacService.ValidateAsync(request.OtacCode, clientIp, language);

            if (result.IsSuccess)
            {
                _logger.LogInformation("API OTAC verification successful: {OtacCode} from IP: {ClientIp}", 
                    request.OtacCode, clientIp);

                // Store validated OTAC in session
                HttpContext.SetValidatedOtac(request.OtacCode);

                var response = new BizConnect.ViewModels.Modern.VerifyOtacResponse
                {
                    Success = true,
                    Message = "OTAC verified successfully",
                    RedirectUrl = Url.Action("Register"),
                    Metadata = new Dictionary<string, object>
                    {
                        ["validatedAt"] = DateTime.UtcNow,
                        ["sessionId"] = HttpContext.Session.Id
                    }
                };

                return Ok(ApiResponse<BizConnect.ViewModels.Modern.VerifyOtacResponse>.Ok(response, "OTAC verification successful"));
            }
            else
            {
                _logger.LogWarning("API OTAC verification failed: {OtacCode} from IP: {ClientIp}, Error: {ErrorMessage}", 
                    request.OtacCode, clientIp, result.ErrorMessage);

                var response = new BizConnect.ViewModels.Modern.VerifyOtacResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Invalid OTAC code",
                    AttemptsRemaining = result.AttemptsRemaining,
                    LockoutTimeRemaining = result.LockoutTimeRemaining > 0 
                        ? TimeSpan.FromMinutes(result.LockoutTimeRemaining) 
                        : null
                };

                return Ok(ApiResponse<BizConnect.ViewModels.Modern.VerifyOtacResponse>.Ok(response, "OTAC verification failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during API OTAC verification for code: {OtacCode}", request.OtacCode);
            return StatusCode(500, ApiResponse<BizConnect.ViewModels.Modern.VerifyOtacResponse>.Error("Internal server error during verification"));
        }
    }

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
    /// Process registration form submission
    /// </summary>
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ModernRegistrationViewModel model)
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

        // Update validation status
        model.ValidationStatus = new FormValidationStatus
        {
            IsValid = false,
            GeneralErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList(),
            ValidationScore = CalculateValidationScore(ModelState)
        };

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

    #region Private Helper Methods for Modern UI

    /// <summary>
    /// Get localized welcome message based on current language
    /// </summary>
    private string GetLocalizedWelcomeMessage()
    {
        var language = GetCurrentLanguage();
        return language switch
        {
            "th" => "ยินดีต้อนรับสู่ระบบ BizConnect Online Direct Debit",
            "en" => "Welcome to BizConnect Online Direct Debit System",
            _ => "ยินดีต้อนรับสู่ระบบ BizConnect"
        };
    }

    /// <summary>
    /// Get feature highlights for landing page
    /// </summary>
    private List<BizConnect.Models.ViewModels.FeatureHighlight> GetFeatureHighlights()
    {
        return new List<BizConnect.Models.ViewModels.FeatureHighlight>
        {
            new BizConnect.Models.ViewModels.FeatureHighlight
            {
                Title = "ปลอดภัยและเชื่อถือได้",
                Description = "ระบบรักษาความปลอดภัยระดับธนาคาร",
                IconClass = "fas fa-shield-alt",
                Color = "success",
                DisplayOrder = 1
            },
            new BizConnect.Models.ViewModels.FeatureHighlight
            {
                Title = "รวดเร็วและสะดวก",
                Description = "ลงทะเบียนได้ภายใน 3 นาที",
                IconClass = "fas fa-clock",
                Color = "primary",
                DisplayOrder = 2
            },
            new BizConnect.Models.ViewModels.FeatureHighlight
            {
                Title = "สนับสนุน 24/7",
                Description = "ติดต่อได้ตลอด 24 ชั่วโมง",
                IconClass = "fas fa-headset",
                Color = "info",
                DisplayOrder = 3
            }
        };
    }

    /// <summary>
    /// Get support information for user assistance
    /// </summary>
    private SupportInfo GetSupportInformation()
    {
        return new SupportInfo
        {
            ContactPhone = "02-123-4567",
            ContactEmail = "support@bizconnect.com",
            HelpDeskHours = "จันทร์-ศุกร์ 8:30-17:30 น.",
            FrequentlyAskedQuestions = new List<BizConnect.ViewModels.Modern.FaqItem>
            {
                new BizConnect.ViewModels.Modern.FaqItem
                {
                    Question = "OTAC คืออะไร?",
                    Answer = "OTAC คือรหัสยืนยันตัวตนชั่วคราว 8 หลัก ที่ใช้สำหรับเข้าใช้งานระบบ",
                    Category = "General"
                },
                new BizConnect.ViewModels.Modern.FaqItem
                {
                    Question = "จะได้รับ OTAC ได้อย่างไร?",
                    Answer = "ติดต่อธนาคารเพื่อขอรับรหัส OTAC หรือใช้ช่องทางออนไลน์",
                    Category = "OTAC"
                }
            },
            SupportChannels = new List<SupportChannel>
            {
                new SupportChannel
                {
                    Name = "โทรศัพท์",
                    Description = "สนับสนุนทันที",
                    ContactInfo = "02-123-4567",
                    IsAvailable = true,
                    AvailabilityHours = "8:30-17:30 น."
                },
                new SupportChannel
                {
                    Name = "อีเมล",
                    Description = "ตอบกลับภายใน 24 ชั่วโมง",
                    ContactInfo = "support@bizconnect.com",
                    IsAvailable = true,
                    AvailabilityHours = "ตลอด 24 ชั่วโมง"
                }
            }
        };
    }

    /// <summary>
    /// Calculate validation score for form
    /// </summary>
    private int CalculateValidationScore(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        if (modelState.IsValid)
            return 100;

        var totalErrors = modelState.Values.Sum(v => v.Errors.Count);
        var totalFields = modelState.Count;
        
        if (totalFields == 0)
            return 0;

        var errorRate = (double)totalErrors / totalFields;
        return Math.Max(0, 100 - (int)(errorRate * 100));
    }

    /// <summary>
    /// Get landing page statistics
    /// </summary>
    private async Task<LandingPageStats?> GetLandingPageStats()
    {
        try
        {
            // In a real implementation, these would come from the database or services
            return new LandingPageStats
            {
                TotalRegistrations = 15420,
                SuccessfulRegistrations = 15210,
                DailyRegistrations = 247,
                SuccessRate = 98.7,
                LastUpdated = DateTime.Now.ToString("HH:mm น.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting landing page stats");
            return null;
        }
    }

    /// <summary>
    /// Get trust indicators for landing page
    /// </summary>
    private List<BizConnect.Models.ViewModels.TrustIndicator> GetTrustIndicators()
    {
        return new List<BizConnect.Models.ViewModels.TrustIndicator>
        {
            new BizConnect.Models.ViewModels.TrustIndicator
            {
                Title = "SSL Certificate",
                Description = "Secured with 256-bit encryption",
                IconClass = "fas fa-shield-alt",
                BadgeColor = "success",
                IsVerified = true
            },
            new BizConnect.Models.ViewModels.TrustIndicator
            {
                Title = "Bank Authorized",
                Description = "Official KBank partner platform",
                IconClass = "fas fa-university",
                BadgeColor = "primary",
                IsVerified = true
            },
            new BizConnect.Models.ViewModels.TrustIndicator
            {
                Title = "System Uptime",
                Description = "99.9% availability guarantee",
                IconClass = "fas fa-check-circle",
                BadgeColor = "success",
                IsVerified = true
            }
        };
    }

    /// <summary>
    /// Get FAQ items for landing page
    /// </summary>
    private List<BizConnect.Models.ViewModels.FaqItem> GetFaqItems()
    {
        return new List<BizConnect.Models.ViewModels.FaqItem>
        {
            new BizConnect.Models.ViewModels.FaqItem
            {
                Question = "รหัส OTAC คืออะไร และจะได้รับอย่างไร?",
                Answer = "OTAC (One-Time Authorization Code) คือรหัสยืนยันตัวตนชั่วคราว 6-8 หลัก ที่ใช้สำหรับการลงทะเบียนบริการหักบัญชีอัตโนมัติ ผู้ใช้งานจะได้รับรหัสนี้จากเจ้าหน้าที่ หรือผ่านช่องทางที่ได้รับการยืนยันเท่านั้น",
                Category = "OTAC",
                DisplayOrder = 1
            },
            new BizConnect.Models.ViewModels.FaqItem
            {
                Question = "ข้อมูลของฉันปลอดภัยหรือไม่?",
                Answer = "เราใช้ระบบรักษาความปลอดภัยระดับธนาคาร พร้อมการเข้ารหัสข้อมูล SSL 256-bit และปฏิบัติตามมาตรฐานความปลอดภัยของอุตสาหกรรมการเงิน ข้อมูลของคุณจะได้รับการปกป้องในระดับเดียวกับธนาคารชั้นนำ",
                Category = "Security",
                DisplayOrder = 2
            },
            new BizConnect.Models.ViewModels.FaqItem
            {
                Question = "การลงทะเบียนใช้เวลานานแค่ไหน?",
                Answer = "การลงทะเบียนใช้เวลาเฉลี่ย 2-3 นาที โดยขึ้นอยู่กับความพร้อมของข้อมูล หลังจากกรอกข้อมูลเสร็จสิ้น ระบบจะประมวลผลและส่งข้อมูลไปยัง KBank โดยอัตโนมัติ คุณจะได้รับการแจ้งเตือนผลการดำเนินการทันที",
                Category = "Process",
                DisplayOrder = 3
            },
            new BizConnect.Models.ViewModels.FaqItem
            {
                Question = "ต้องใช้เอกสารอะไรในการลงทะเบียน?",
                Answer = "คุณต้องเตรียมข้อมูลดังนี้: เลขประจำตัวประชาชน 13 หลัก, เบอร์โทรศัพท์มือถือ, เลขที่บัญชีธนาคาร KBank, ชื่อสาขาธนาคารที่เปิดบัญชี",
                Category = "Requirements",
                DisplayOrder = 4
            },
            new BizConnect.Models.ViewModels.FaqItem
            {
                Question = "หากมีปัญหาจะติดต่อใครได้บ้าง?",
                Answer = "ทีมสนับสนุนของเราพร้อมให้บริการ: โทรศัพท์ 02-123-4567 (จันทร์-ศุกร์ 8:30-17:30 น.), อีเมล support@bizconnect.com (ตอบกลับภายใน 24 ชั่วโมง), ระบบแชทออนไลน์ในเว็บไซต์",
                Category = "Support",
                DisplayOrder = 5
            }
        };
    }

    /// <summary>
    /// Get default trust indicators for landing page (legacy method for compatibility)
    /// </summary>
    private List<BizConnect.ViewModels.Modern.TrustIndicator> GetDefaultTrustIndicators()
    {
        return new List<BizConnect.ViewModels.Modern.TrustIndicator>
        {
            new BizConnect.ViewModels.Modern.TrustIndicator
            {
                Title = "SSL Certificate",
                Description = "Secured with 256-bit encryption",
                IconClass = "fas fa-shield-alt",
                Value = "Active",
                Color = "success"
            },
            new BizConnect.ViewModels.Modern.TrustIndicator
            {
                Title = "Bank Authorized",
                Description = "Official KBank partner platform",
                IconClass = "fas fa-university",
                Value = "Verified",
                Color = "primary"
            },
            new BizConnect.ViewModels.Modern.TrustIndicator
            {
                Title = "Uptime",
                Description = "System availability",
                IconClass = "fas fa-check-circle",
                Value = "99.9%",
                Color = "success"
            }
        };
    }

    /// <summary>
    /// Get default security badges for landing page
    /// </summary>
    private List<BizConnect.ViewModels.Modern.SecurityBadge> GetDefaultSecurityBadges()
    {
        return new List<BizConnect.ViewModels.Modern.SecurityBadge>
        {
            new BizConnect.ViewModels.Modern.SecurityBadge
            {
                Title = "SSL Secured",
                Description = "Your connection is encrypted and secure",
                IconClass = "fas fa-lock",
                BadgeColor = "success",
                IsVerified = true
            },
            new BizConnect.ViewModels.Modern.SecurityBadge
            {
                Title = "Bank Grade Security",
                Description = "Meets banking industry security standards",
                IconClass = "fas fa-shield-check",
                BadgeColor = "primary",
                IsVerified = true
            }
        };
    }

    #endregion
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
/// Legacy ViewModel for backward compatibility - use ModernRegistrationViewModel for new implementations
/// </summary>
[Obsolete("Use ModernRegistrationViewModel instead", false)]
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
