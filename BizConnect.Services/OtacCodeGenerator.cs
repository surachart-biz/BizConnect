using BizConnect.Services.Interfaces;
using BizConnect.Services.Utils;

namespace BizConnect.Services
{
    /// <summary>
    /// Default implementation of IOtacCodeGenerator using OtacUtils
    /// </summary>
    public class OtacCodeGenerator : IOtacCodeGenerator
    {
        /// <summary>
        /// Generates a cryptographically secure 8-character alphanumeric OTAC code
        /// Excludes confusing characters: 0 (zero), O (capital o), 1 (one), l (lowercase L), I (capital i)
        /// </summary>
        /// <returns>8-character OTAC code</returns>
        public string GenerateCode()
        {
            return OtacUtils.GenerateOtacCode();
        }

        /// <summary>
        /// Validates OTAC code format (8 characters, valid character set)
        /// </summary>
        /// <param name="otacCode">OTAC code to validate</param>
        /// <returns>True if format is valid</returns>
        public bool IsValidFormat(string? otacCode)
        {
            return OtacUtils.IsValidOtacFormat(otacCode);
        }

        /// <summary>
        /// Normalizes OTAC code to uppercase for case-insensitive comparison
        /// </summary>
        /// <param name="otacCode">OTAC code to normalize</param>
        /// <returns>Normalized OTAC code</returns>
        public string NormalizeCode(string otacCode)
        {
            return OtacUtils.NormalizeOtacCode(otacCode);
        }
    }
}