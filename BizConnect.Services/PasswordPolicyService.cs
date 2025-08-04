using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services
{
    /// <summary>
    /// Implementation of password policy service for enforcing security requirements
    /// </summary>
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private readonly BizConnectContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordPolicyService> _logger;
        
        // Common passwords to check against (this would typically be loaded from a file or database)
        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "password123", "admin", "qwerty", "letmein", "welcome",
            "monkey", "1234567890", "abc123", "Password1", "password1", "123456789",
            "welcome123", "admin123", "root", "toor", "pass", "test", "guest"
        };

        public PasswordPolicyService(
            BizConnectContext context,
            IConfiguration configuration,
            ILogger<PasswordPolicyService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<PasswordValidationResult> ValidatePasswordAsync(string password, string username = null)
        {
            var result = new PasswordValidationResult
            {
                Violations = new PasswordPolicyViolations()
            };

            var errors = new List<string>();
            var warnings = new List<string>();
            var config = GetPolicyConfiguration();

            // Validate length
            if (!ValidateLength(password))
            {
                if (password.Length < config.MinimumLength)
                {
                    result.Violations.TooShort = true;
                    errors.Add($"Password must be at least {config.MinimumLength} characters long.");
                }
                if (password.Length > config.MaximumLength)
                {
                    result.Violations.TooLong = true;
                    errors.Add($"Password must not exceed {config.MaximumLength} characters.");
                }
            }

            // Validate complexity
            if (!ValidateComplexity(password))
            {
                if (config.RequireUppercase && !password.Any(char.IsUpper))
                {
                    result.Violations.MissingUppercase = true;
                    errors.Add("Password must contain at least one uppercase letter.");
                }
                if (config.RequireLowercase && !password.Any(char.IsLower))
                {
                    result.Violations.MissingLowercase = true;
                    errors.Add("Password must contain at least one lowercase letter.");
                }
                if (config.RequireDigit && !password.Any(char.IsDigit))
                {
                    result.Violations.MissingDigit = true;
                    errors.Add("Password must contain at least one digit.");
                }
                if (config.RequireSpecialCharacter && !password.Any(c => config.SpecialCharacters.Contains(c)))
                {
                    result.Violations.MissingSpecialCharacter = true;
                    errors.Add($"Password must contain at least one special character ({config.SpecialCharacters}).");
                }
            }

            // Validate against username
            if (!string.IsNullOrEmpty(username) && !ValidateNotSimilarToUsername(password, username))
            {
                result.Violations.ContainsUsername = true;
                errors.Add("Password must not contain the username.");
            }

            // Check against common passwords
            if (config.CheckAgainstCommonPasswords && !await ValidateAgainstCommonPasswordsAsync(password))
            {
                result.Violations.ContainsCommonPassword = true;
                errors.Add("Password is too common. Please choose a more secure password.");
            }

            // Check password history
            if (!string.IsNullOrEmpty(username) && config.PasswordHistoryCount > 0)
            {
                if (!await ValidatePasswordHistoryAsync(username, password))
                {
                    result.Violations.ReusedFromHistory = true;
                    errors.Add($"Password has been used recently. Please choose a different password.");
                }
            }

            // Check for repeating characters
            if (HasTooManyRepeatingCharacters(password, config.MaxRepeatingCharacters))
            {
                result.Violations.HasRepeatingCharacters = true;
                warnings.Add($"Password contains too many repeating characters.");
            }

            // Check for sequential characters
            if (config.PreventSequentialCharacters && HasSequentialCharacters(password))
            {
                result.Violations.HasSequentialCharacters = true;
                warnings.Add("Password contains sequential characters (e.g., 123, abc).");
            }

            // Calculate strength
            result.StrengthScore = CalculatePasswordStrength(password);
            result.StrengthDescription = GetStrengthDescription(result.StrengthScore);

            // Determine if password meets minimum requirements
            result.MeetsMinimumRequirements = errors.Count == 0 && result.StrengthScore >= config.MinimumStrengthScore;
            result.IsValid = result.MeetsMinimumRequirements;

            if (result.StrengthScore < config.MinimumStrengthScore)
            {
                warnings.Add($"Password strength is below recommended level. Current: {result.StrengthScore}, Minimum: {config.MinimumStrengthScore}");
            }

            result.Errors = errors.ToArray();
            result.Warnings = warnings.ToArray();

            return result;
        }

        public bool ValidateLength(string password)
        {
            var config = GetPolicyConfiguration();
            return password.Length >= config.MinimumLength && password.Length <= config.MaximumLength;
        }

        public bool ValidateComplexity(string password)
        {
            var config = GetPolicyConfiguration();

            var hasUpper = !config.RequireUppercase || password.Any(char.IsUpper);
            var hasLower = !config.RequireLowercase || password.Any(char.IsLower);
            var hasDigit = !config.RequireDigit || password.Any(char.IsDigit);
            var hasSpecial = !config.RequireSpecialCharacter || password.Any(c => config.SpecialCharacters.Contains(c));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }

        public async Task<bool> ValidateAgainstCommonPasswordsAsync(string password)
        {
            // Check against our static list first
            if (CommonPasswords.Contains(password))
            {
                return false;
            }

            // Check for simple variations (password with numbers at end, etc.)
            var basePassword = Regex.Replace(password, @"\d+$", ""); // Remove trailing digits
            if (CommonPasswords.Contains(basePassword))
            {
                return false;
            }

            // TODO: In a real implementation, you might check against a larger database
            // of compromised passwords (e.g., HaveIBeenPwned API)
            
            await Task.CompletedTask;
            return true;
        }

        public bool ValidateNotSimilarToUsername(string password, string username)
        {
            if (string.IsNullOrEmpty(username))
                return true;

            // Check if password contains username (case insensitive)
            return !password.ToLowerInvariant().Contains(username.ToLowerInvariant());
        }

        public async Task<bool> ValidatePasswordHistoryAsync(string username, string password)
        {
            // TODO: In a real implementation, you would check against a password history table
            // This requires adding a PasswordHistory table to track hashed previous passwords
            
            // For now, we'll just return true (no history violation)
            // When implementing, you would:
            // 1. Hash the new password
            // 2. Compare against stored hashes of previous passwords
            // 3. Return false if it matches any recent password
            
            await Task.CompletedTask;
            return true;
        }

        public int CalculatePasswordStrength(string password)
        {
            var score = 0;
            
            // Length scoring
            if (password.Length >= 8) score += 20;
            if (password.Length >= 12) score += 10;
            if (password.Length >= 16) score += 10;

            // Character type scoring
            if (password.Any(char.IsUpper)) score += 15;
            if (password.Any(char.IsLower)) score += 15;
            if (password.Any(char.IsDigit)) score += 15;
            if (password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c))) score += 15;

            // Variety scoring
            var uniqueChars = password.Distinct().Count();
            if (uniqueChars >= password.Length * 0.7) score += 10; // Good character variety

            // Deduct points for patterns
            if (HasRepeatingCharacters(password, 3)) score -= 10;
            if (HasSequentialCharacters(password)) score -= 10;
            if (CommonPasswords.Contains(password.ToLowerInvariant())) score -= 30;

            // Ensure score is within bounds
            return Math.Max(0, Math.Min(100, score));
        }

        public string GenerateSecurePassword(int length = 12)
        {
            var config = GetPolicyConfiguration();
            length = Math.Max(length, config.MinimumLength);

            var uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var lowercase = "abcdefghijklmnopqrstuvwxyz";
            var digits = "0123456789";
            var specials = config.SpecialCharacters;

            var allChars = uppercase + lowercase + digits + specials;
            var password = new StringBuilder();
            var random = new Random();

            // Ensure at least one character from each required category
            if (config.RequireUppercase) password.Append(uppercase[random.Next(uppercase.Length)]);
            if (config.RequireLowercase) password.Append(lowercase[random.Next(lowercase.Length)]);
            if (config.RequireDigit) password.Append(digits[random.Next(digits.Length)]);
            if (config.RequireSpecialCharacter) password.Append(specials[random.Next(specials.Length)]);

            // Fill the rest randomly
            while (password.Length < length)
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password to avoid predictable patterns
            return ShuffleString(password.ToString());
        }

        public PasswordPolicyConfiguration GetPolicyConfiguration()
        {            
            return new PasswordPolicyConfiguration
            {
                MinimumLength = _configuration.GetValue("PasswordPolicy:MinimumLength", 8),
                MaximumLength = _configuration.GetValue("PasswordPolicy:MaximumLength", 128),
                RequireUppercase = _configuration.GetValue("PasswordPolicy:RequireUppercase", true),
                RequireLowercase = _configuration.GetValue("PasswordPolicy:RequireLowercase", true),
                RequireDigit = _configuration.GetValue("PasswordPolicy:RequireDigit", true),
                RequireSpecialCharacter = _configuration.GetValue("PasswordPolicy:RequireSpecialCharacter", true),
                SpecialCharacters = _configuration.GetValue("PasswordPolicy:SpecialCharacters", "!@#$%^&*()_+-=[]{}|;:,.<>?"),
                PreventUsernameInPassword = _configuration.GetValue("PasswordPolicy:PreventUsernameInPassword", true),
                CheckAgainstCommonPasswords = _configuration.GetValue("PasswordPolicy:CheckAgainstCommonPasswords", true),
                PasswordHistoryCount = _configuration.GetValue("PasswordPolicy:PasswordHistoryCount", 5),
                PasswordExpirationDays = _configuration.GetValue("PasswordPolicy:PasswordExpirationDays", 90),
                PasswordExpirationWarningDays = _configuration.GetValue("PasswordPolicy:PasswordExpirationWarningDays", 14),
                MaxRepeatingCharacters = _configuration.GetValue("PasswordPolicy:MaxRepeatingCharacters", 3),
                PreventSequentialCharacters = _configuration.GetValue("PasswordPolicy:PreventSequentialCharacters", true),
                MinimumStrengthScore = _configuration.GetValue("PasswordPolicy:MinimumStrengthScore", 60)
            };
        }

        public async Task<bool> IsPasswordExpiredAsync(string username)
        {
            // TODO: Implement password expiration check
            // This requires adding a PasswordChangedDate field to the User model
            await Task.CompletedTask;
            return false;
        }

        public async Task<int> GetDaysUntilPasswordExpiresAsync(string username)
        {
            // TODO: Implement password expiration calculation
            await Task.CompletedTask;
            return int.MaxValue; // Never expires for now
        }

        private bool HasRepeatingCharacters(string password, int maxRepeating)
        {
            return HasTooManyRepeatingCharacters(password, maxRepeating);
        }

        private bool HasTooManyRepeatingCharacters(string password, int maxRepeating)
        {
            for (int i = 0; i < password.Length - maxRepeating; i++)
            {
                var currentChar = password[i];
                var count = 1;
                
                for (int j = i + 1; j < password.Length && password[j] == currentChar; j++)
                {
                    count++;
                    if (count > maxRepeating)
                        return true;
                }
            }
            return false;
        }

        private bool HasSequentialCharacters(string password)
        {
            // Check for sequences like "123", "abc", "xyz"
            for (int i = 0; i < password.Length - 2; i++)
            {
                var char1 = password[i];
                var char2 = password[i + 1];
                var char3 = password[i + 2];

                // Check ascending sequence
                if (char.IsLetterOrDigit(char1) && char.IsLetterOrDigit(char2) && char.IsLetterOrDigit(char3))
                {
                    if ((char2 == char1 + 1) && (char3 == char2 + 1))
                        return true;
                    
                    // Check descending sequence
                    if ((char2 == char1 - 1) && (char3 == char2 - 1))
                        return true;
                }
            }
            return false;
        }

        private string GetStrengthDescription(int score)
        {
            return score switch
            {
                < 20 => "Very Weak",
                < 40 => "Weak",
                < 60 => "Fair",
                < 80 => "Good",
                < 90 => "Strong",
                _ => "Very Strong"
            };
        }

        private string ShuffleString(string input)
        {
            var chars = input.ToCharArray();
            var random = new Random();
            
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            
            return new string(chars);
        }
    }
}