using BizConnect.Services.Interfaces;

namespace BizConnect.Services;

/// <summary>
/// Service for handling complex validation logic - moved from ViewModels to maintain 3-tier architecture
/// </summary>
public class ValidationService : IValidationService
{
    /// <summary>
    /// Validates ID value based on the specified ID type
    /// </summary>
    /// <param name="idType">Type of ID (National ID, Passport, etc.)</param>
    /// <param name="idValue">ID value to validate</param>
    /// <returns>Validation result with error message if invalid</returns>
    public BizConnect.Services.Interfaces.ValidationResult ValidateIdValue(string idType, string idValue)
    {
        if (string.IsNullOrWhiteSpace(idValue))
        {
            return BizConnect.Services.Interfaces.ValidationResult.Failure("ID value is required");
        }

        return idType switch
        {
            "National ID" => ValidateThaiNationalId(idValue),
            "Passport" => ValidatePassport(idValue),
            "Tax ID" => ValidateTaxId(idValue),
            "Company Tax ID" => ValidateCompanyTaxId(idValue),
            _ => BizConnect.Services.Interfaces.ValidationResult.Success() // Unknown ID types pass through basic validation
        };
    }

    /// <summary>
    /// Validates Thai National ID format (13 digits)
    /// </summary>
    private static BizConnect.Services.Interfaces.ValidationResult ValidateThaiNationalId(string value)
    {
        if (value.Length != 13 || !value.All(char.IsDigit))
        {
            return BizConnect.Services.Interfaces.ValidationResult.Failure("National ID must be exactly 13 digits");
        }
        return BizConnect.Services.Interfaces.ValidationResult.Success();
    }

    /// <summary>
    /// Validates passport format (8-20 alphanumeric characters)
    /// </summary>
    private static BizConnect.Services.Interfaces.ValidationResult ValidatePassport(string value)
    {
        if (value.Length < 8 || value.Length > 20 || !value.All(char.IsLetterOrDigit))
        {
            return BizConnect.Services.Interfaces.ValidationResult.Failure("Passport number must be 8-20 alphanumeric characters");
        }
        return BizConnect.Services.Interfaces.ValidationResult.Success();
    }

    /// <summary>
    /// Validates Tax ID format (10-13 digits)
    /// </summary>
    private static BizConnect.Services.Interfaces.ValidationResult ValidateTaxId(string value)
    {
        if (value.Length < 10 || value.Length > 13 || !value.All(char.IsDigit))
        {
            return BizConnect.Services.Interfaces.ValidationResult.Failure("Tax ID must be 10-13 digits");
        }
        return BizConnect.Services.Interfaces.ValidationResult.Success();
    }

    /// <summary>
    /// Validates Company Tax ID format (13 digits)
    /// </summary>
    private static BizConnect.Services.Interfaces.ValidationResult ValidateCompanyTaxId(string value)
    {
        if (value.Length != 13 || !value.All(char.IsDigit))
        {
            return BizConnect.Services.Interfaces.ValidationResult.Failure("Company Tax ID must be exactly 13 digits");
        }
        return BizConnect.Services.Interfaces.ValidationResult.Success();
    }
}