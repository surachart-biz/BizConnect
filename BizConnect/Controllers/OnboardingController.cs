using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BizConnect.Models;

namespace BizConnect.Controllers
{
    [Authorize]
    public class OnboardingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Welcome()
        {
            return View();
        }

        public IActionResult ProfileSetup()
        {
            return View();
        }

        public IActionResult NetworkingGuide()
        {
            return View();
        }

        public IActionResult PlatformTour()
        {
            return View();
        }

        public IActionResult Complete()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CompleteStep([FromBody] OnboardingStepRequest request)
        {
            // In a real application, this would save the user's onboarding progress
            // For now, we'll just return success
            return Json(new { success = true, message = "Step completed successfully!" });
        }

        [HttpPost]
        public IActionResult SkipOnboarding()
        {
            // Mark onboarding as skipped for the user
            return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
        }

        [HttpGet]
        public IActionResult GetProgress()
        {
            // Return user's onboarding progress
            var progress = new
            {
                currentStep = 1,
                totalSteps = 5,
                completedSteps = new[] { 1 },
                canSkip = true
            };

            return Json(progress);
        }
    }

    public class OnboardingStepRequest
    {
        public int StepNumber { get; set; }
        public string StepName { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
