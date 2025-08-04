namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Interface for generating OTAC codes.
    /// This abstraction enables testable OTAC code generation.
    /// </summary>
    public interface IOtacCodeGenerator
    {
        /// <summary>
        /// Generates a cryptographically secure 8-character alphanumeric OTAC code
        /// Excludes confusing characters: 0 (zero), O (capital o), 1 (one), l (lowercase L), I (capital i)
        /// </summary>
        /// <returns>8-character OTAC code</returns>
        string GenerateCode();

        /// <summary>
        /// Validates OTAC code format (8 characters, valid character set)
        /// </summary>
        /// <param name="otacCode">OTAC code to validate</param>
        /// <returns>True if format is valid</returns>
        bool IsValidFormat(string? otacCode);

        /// <summary>
        /// Normalizes OTAC code to uppercase for case-insensitive comparison
        /// </summary>
        /// <param name="otacCode">OTAC code to normalize</param>
        /// <returns>Normalized OTAC code</returns>
        string NormalizeCode(string otacCode);
    }
}