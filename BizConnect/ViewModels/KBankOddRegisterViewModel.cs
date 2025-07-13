using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BizConnect.ViewModels;

/// <summary>
/// View model for KBank Online Direct Debit registration form
/// </summary>
public class KBankOddRegisterViewModel : IValidatableObject
{
    /// <summary>
    /// User's email address for ODD registration
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

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
    /// Available ID types for dropdown selection
    /// </summary>
    public static readonly List<SelectListItem> IdTypes = new()
    {
        new SelectListItem { Value = "National ID", Text = "National ID" },
        new SelectListItem { Value = "Passport", Text = "Passport" },
        new SelectListItem { Value = "Tax ID", Text = "Tax ID" },
        new SelectListItem { Value = "Company Tax ID", Text = "Company Tax ID" }
    };

    /// <summary>
    /// Custom validation for ID value based on ID type
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation result</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate ID value based on ID type
        switch (IdType)
        {
            case "National ID":
                if (!IsValidThaiNationalId(IdValue))
                {
                    results.Add(new ValidationResult(
                        "National ID must be 13 digits", 
                        new[] { nameof(IdValue) }));
                }
                break;

            case "Passport":
                if (!IsValidPassport(IdValue))
                {
                    results.Add(new ValidationResult(
                        "Passport number must be 8-20 alphanumeric characters", 
                        new[] { nameof(IdValue) }));
                }
                break;

            case "Tax ID":
                if (!IsValidTaxId(IdValue))
                {
                    results.Add(new ValidationResult(
                        "Tax ID must be 10-13 digits", 
                        new[] { nameof(IdValue) }));
                }
                break;

            case "Company Tax ID":
                if (!IsValidCompanyTaxId(IdValue))
                {
                    results.Add(new ValidationResult(
                        "Company Tax ID must be 13 digits", 
                        new[] { nameof(IdValue) }));
                }
                break;
        }

        return results;
    }

    /// <summary>
    /// Validates Thai National ID format (13 digits)
    /// </summary>
    private static bool IsValidThaiNationalId(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               value.Length == 13 && 
               value.All(char.IsDigit);
    }

    /// <summary>
    /// Validates passport format (8-20 alphanumeric characters)
    /// </summary>
    private static bool IsValidPassport(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               value.Length >= 8 && 
               value.Length <= 20 && 
               value.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Validates Tax ID format (10-13 digits)
    /// </summary>
    private static bool IsValidTaxId(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               value.Length >= 10 && 
               value.Length <= 13 && 
               value.All(char.IsDigit);
    }

    /// <summary>
    /// Validates Company Tax ID format (13 digits)
    /// </summary>
    private static bool IsValidCompanyTaxId(string value)
    {
        return !string.IsNullOrEmpty(value) && 
               value.Length == 13 && 
               value.All(char.IsDigit);
    }
}
