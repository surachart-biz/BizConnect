using System;
using System.Threading.Tasks;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service for enforcing password policies and complexity validation
    /// </summary>
    public interface IPasswordPolicyService
    {
        /// <summary>
        /// Validate password against all configured policies
        /// </summary>
        Task<PasswordValidationResult> ValidatePasswordAsync(string password, string username = null);

        /// <summary>
        /// Check if password meets minimum length requirements
        /// </summary>
        bool ValidateLength(string password);

        /// <summary>
        /// Check if password contains required character types
        /// </summary>
        bool ValidateComplexity(string password);

        /// <summary>
        /// Check if password contains common patterns or dictionary words
        /// </summary>
        Task<bool> ValidateAgainstCommonPasswordsAsync(string password);

        /// <summary>
        /// Check if password is too similar to username
        /// </summary>
        bool ValidateNotSimilarToUsername(string password, string username);

        /// <summary>
        /// Check if password has been used recently (password history)
        /// </summary>
        Task<bool> ValidatePasswordHistoryAsync(string username, string password);

        /// <summary>
        /// Calculate password strength score (0-100)
        /// </summary>
        int CalculatePasswordStrength(string password);

        /// <summary>
        /// Generate a secure password that meets all policy requirements
        /// </summary>
        string GenerateSecurePassword(int length = 12);

        /// <summary>
        /// Get current password policy configuration
        /// </summary>
        PasswordPolicyConfiguration GetPolicyConfiguration();

        /// <summary>
        /// Check if user's password has expired
        /// </summary>
        Task<bool> IsPasswordExpiredAsync(string username);

        /// <summary>
        /// Get days until password expires
        /// </summary>
        Task<int> GetDaysUntilPasswordExpiresAsync(string username);
    }

    /// <summary>
    /// Result of password validation
    /// </summary>
    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();
        public int StrengthScore { get; set; }
        public string StrengthDescription { get; set; }
        public bool MeetsMinimumRequirements { get; set; }
        public PasswordPolicyViolations Violations { get; set; } = new();
    }

    /// <summary>
    /// Specific policy violations
    /// </summary>
    public class PasswordPolicyViolations
    {
        public bool TooShort { get; set; }
        public bool TooLong { get; set; }
        public bool MissingUppercase { get; set; }
        public bool MissingLowercase { get; set; }
        public bool MissingDigit { get; set; }
        public bool MissingSpecialCharacter { get; set; }
        public bool ContainsUsername { get; set; }
        public bool ContainsCommonPassword { get; set; }
        public bool ReusedFromHistory { get; set; }
        public bool ContainsPersonalInfo { get; set; }
        public bool HasRepeatingCharacters { get; set; }
        public bool HasSequentialCharacters { get; set; }
    }

    /// <summary>
    /// Password policy configuration
    /// </summary>
    public class PasswordPolicyConfiguration
    {
        public int MinimumLength { get; set; } = 8;
        public int MaximumLength { get; set; } = 128;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialCharacter { get; set; } = true;
        public string SpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";
        public bool PreventUsernameInPassword { get; set; } = true;
        public bool CheckAgainstCommonPasswords { get; set; } = true;
        public int PasswordHistoryCount { get; set; } = 5;
        public int PasswordExpirationDays { get; set; } = 90;
        public int PasswordExpirationWarningDays { get; set; } = 14;
        public int MaxRepeatingCharacters { get; set; } = 3;
        public bool PreventSequentialCharacters { get; set; } = true;
        public int MinimumStrengthScore { get; set; } = 60;
    }

    /// <summary>
    /// Password strength levels
    /// </summary>
    public enum PasswordStrength
    {
        VeryWeak = 0,
        Weak = 20,
        Fair = 40,
        Good = 60,
        Strong = 80,
        VeryStrong = 100
    }
}