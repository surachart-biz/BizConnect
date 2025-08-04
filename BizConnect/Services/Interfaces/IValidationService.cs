namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service for handling complex validation logic
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates ID value based on the specified ID type
    /// </summary>
    /// <param name="idType">Type of ID (National ID, Passport, etc.)</param>
    /// <param name="idValue">ID value to validate</param>
    /// <returns>Validation result with error message if invalid</returns>
    ValidationResult ValidateIdValue(string idType, string idValue);
}

/// <summary>
/// Validation result containing success status and error message
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
}