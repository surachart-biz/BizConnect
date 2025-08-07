using System.ComponentModel.DataAnnotations;

namespace BizConnect.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the BizConnect landing page with OTAC verification form
    /// </summary>
    public class LandingPageViewModel
    {
        /// <summary>
        /// OTAC code for verification (6-8 characters, alphanumeric)
        /// </summary>
        [Required(ErrorMessage = "กรุณากรอกรหัส OTAC")]
        [StringLength(8, MinimumLength = 6, ErrorMessage = "รหัส OTAC ต้องมี 6-8 ตัวอักษร")]
        [RegularExpression("^[A-Z0-9]{6,8}$", ErrorMessage = "รหัส OTAC ต้องเป็นตัวอักษรภาษาอังกฤษตัวใหญ่และตัวเลขเท่านั้น")]
        [Display(Name = "รหัส OTAC")]
        public string OtacCode { get; set; } = string.Empty;

        /// <summary>
        /// Statistics for display on landing page
        /// </summary>
        public LandingPageStats? Stats { get; set; }

        /// <summary>
        /// System status information
        /// </summary>
        public SystemStatus? SystemStatus { get; set; }

        /// <summary>
        /// Trust and security indicators
        /// </summary>
        public List<TrustIndicator> TrustIndicators { get; set; } = new();

        /// <summary>
        /// Feature highlights for the landing page
        /// </summary>
        public List<FeatureHighlight> Features { get; set; } = new();

        /// <summary>
        /// FAQ items for the accordion section
        /// </summary>
        public List<FaqItem> FaqItems { get; set; } = new();
    }

    /// <summary>
    /// Statistics displayed on the landing page
    /// </summary>
    public class LandingPageStats
    {
        public int TotalRegistrations { get; set; }
        public int SuccessfulRegistrations { get; set; }
        public int DailyRegistrations { get; set; }
        public double SuccessRate { get; set; }
        public string LastUpdated { get; set; } = string.Empty;
    }

    /// <summary>
    /// System status information
    /// </summary>
    public class SystemStatus
    {
        public bool IsOperational { get; set; }
        public string Status { get; set; } = "Operational";
        public string Message { get; set; } = "All systems running normally";
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Trust indicator for security and reliability
    /// </summary>
    public class TrustIndicator
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "success";
        public bool IsVerified { get; set; } = true;
    }

    /// <summary>
    /// Feature highlight for the landing page
    /// </summary>
    public class FeatureHighlight
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string Color { get; set; } = "primary";
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// FAQ item for the accordion section
    /// </summary>
    public class FaqItem
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public int DisplayOrder { get; set; }
    }
}