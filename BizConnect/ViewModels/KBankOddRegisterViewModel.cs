using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.ViewModels;

/// <summary>
/// View model for KBank Online Direct Debit registration form V1.9.7
/// Pure data transfer object - business logic moved to ValidationService
/// Email field removed as per V1.9.7 specification
/// </summary>
public class KBankOddRegisterViewModel
{
    /// <summary>
    /// User's full name for registration
    /// </summary>
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 2, 
        ErrorMessage = "Full name must be between 2 and 100 characters")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Type of identification document
    /// </summary>
    [Required(ErrorMessage = "ID type is required")]
    [Display(Name = "ID Type")]
    public string IdType { get; set; } = string.Empty;

    /// <summary>
    /// Identification document number/value
    /// </summary>
    [Required(ErrorMessage = "ID number is required")]
    [StringLength(30, MinimumLength = 8, 
        ErrorMessage = "ID number must be between 8 and 30 characters")]
    [Display(Name = "ID Number")]
    public string IdValue { get; set; } = string.Empty;

    /// <summary>
    /// User's mobile number (format: 08xxxxxxxx or +66xxxxxxxx)
    /// </summary>
    [Required(ErrorMessage = "Mobile number is required")]
    [RegularExpression(@"^(08\d{8}|\+66\d{8,9})$", 
        ErrorMessage = "Mobile number must be in format 08xxxxxxxx or +66xxxxxxxx")]
    [StringLength(20, ErrorMessage = "Mobile number cannot exceed 20 characters")]
    [Display(Name = "Mobile Number")]
    public string MobileNo { get; set; } = string.Empty;

    /// <summary>
    /// Bank account number for ODD registration
    /// </summary>
    [Required(ErrorMessage = "Account number is required")]
    [RegularExpression(@"^\d{10,15}$", 
        ErrorMessage = "Account number must be 10-15 digits")]
    [Display(Name = "Account Number")]
    public string AccountNo { get; set; } = string.Empty;

    /// <summary>
    /// Selected branch ID for the registration
    /// </summary>
    [Required(ErrorMessage = "Branch selection is required")]
    [Display(Name = "Branch")]
    public int? BranchId { get; set; }

    /// <summary>
    /// Available branches for dropdown selection
    /// </summary>
    public List<SelectListItem> Branches { get; set; } = new();

    /// <summary>
    /// Available ID types for dropdown selection
    /// </summary>
    public static readonly List<SelectListItem> IdTypes = new()
    {
        new SelectListItem { Value = "National ID", Text = "National ID" },
        new SelectListItem { Value = "Passport", Text = "Passport" },
        new SelectListItem { Value = "Tax ID", Text = "Tax ID" },
        new SelectListItem { Value = "Company Tax ID", Text = "Company Tax ID" }
    };

}
