using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BizConnect.Services.Models.Requests
{
    /// <summary>
    /// Request model for initiating KBank ODD registration
    /// </summary>
    public class RegistrationRequest
    {
        /// <summary>
        /// User's full name for registration
        /// </summary>
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters")]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Type of identification document
        /// </summary>
        [Required(ErrorMessage = "Identification type is required")]
        public string IdType { get; set; } = string.Empty;

        /// <summary>
        /// Identification document number/value
        /// </summary>
        [Required(ErrorMessage = "Identification value is required")]
        public string IdValue { get; set; } = string.Empty;

        /// <summary>
        /// Mobile phone number in Thai format
        /// </summary>
        [Required(ErrorMessage = "Mobile number is required")]
        [RegularExpression(@"^(08|09)\d{8}$", ErrorMessage = "Mobile number must be in format 08xxxxxxxx or 09xxxxxxxx")]
        public string MobileNo { get; set; } = string.Empty;

        /// <summary>
        /// Bank account number for ODD registration
        /// </summary>
        [Required(ErrorMessage = "Account number is required")]
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Account number must be between 10 and 15 digits")]
        [RegularExpression(@"^\d{10,15}$", ErrorMessage = "Account number must contain only digits")]
        public string AccountNo { get; set; } = string.Empty;

        /// <summary>
        /// Branch ID where the account is held
        /// </summary>
        [Required(ErrorMessage = "Branch selection is required")]
        public int BranchId { get; set; }

        /// <summary>
        /// OTAC code provided by user for validation
        /// </summary>
        [Required(ErrorMessage = "OTAC code is required")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "OTAC code must be exactly 8 characters")]
        [RegularExpression(@"^[A-Z0-9]{8}$", ErrorMessage = "OTAC code must be 8 alphanumeric characters")]
        public string OtacCode { get; set; } = string.Empty;

        /// <summary>
        /// Client IP address for security tracking
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// Additional metadata for the registration request
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }
}