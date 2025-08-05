using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace BizConnect.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminOrEmployee")]
    public abstract class BaseAdminController : Controller
    {
        /// <summary>
        /// Gets the current user's language preference
        /// </summary>
        /// <returns>Language code ("th" or "en")</returns>
        protected string GetCurrentLanguage()
        {
            var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            var currentCulture = requestCulture?.RequestCulture.Culture.Name ?? "en-US";
            
            // Return simplified language code for service calls
            return currentCulture.StartsWith("th") ? "th" : "en";
        }

        /// <summary>
        /// Gets the full culture code (e.g., "en-US", "th-TH")
        /// </summary>
        /// <returns>Full culture code</returns>
        protected string GetCurrentCulture()
        {
            var requestCulture = HttpContext.Features.Get<IRequestCultureFeature>();
            return requestCulture?.RequestCulture.Culture.Name ?? "en-US";
        }

        /// <summary>
        /// Sets ViewBag properties for language context
        /// </summary>
        protected void SetLanguageContext()
        {
            ViewBag.CurrentLanguage = GetCurrentLanguage();
            ViewBag.CurrentCulture = GetCurrentCulture();
            ViewBag.IsThaiLanguage = GetCurrentLanguage() == "th";
        }

        /// <summary>
        /// Override to automatically set language context for all views
        /// </summary>
        public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
        {
            SetLanguageContext();
            base.OnActionExecuting(context);
        }
    }
}