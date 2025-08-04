using System.Security.Cryptography;

namespace BizConnect.Services.Utils;

/// <summary>
/// Utility class for OTAC (One-Time Access Code) operations
/// </summary>
public static class OtacUtils
{
    private const string ValidChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // Excludes 0, O, 1, l, I for clarity
    private const int OtacLength = 8;

    /// <summary>
    /// Generates a cryptographically secure 8-character alphanumeric OTAC code
    /// Excludes confusing characters: 0 (zero), O (capital o), 1 (one), l (lowercase L), I (capital i)
    /// </summary>
    /// <returns>8-character OTAC code</returns>
    public static string GenerateOtacCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var result = new char[OtacLength];
        var randomBytes = new byte[OtacLength];
        
        rng.GetBytes(randomBytes);
        
        for (int i = 0; i < OtacLength; i++)
        {
            result[i] = ValidChars[randomBytes[i] % ValidChars.Length];
        }
        
        return new string(result);
    }

    /// <summary>
    /// Validates OTAC code format (8 characters, valid character set)
    /// </summary>
    /// <param name="otacCode">OTAC code to validate</param>
    /// <returns>True if format is valid</returns>
    public static bool IsValidOtacFormat(string? otacCode)
    {
        if (string.IsNullOrEmpty(otacCode) || otacCode.Length != OtacLength)
            return false;

        return otacCode.All(c => ValidChars.Contains(char.ToUpper(c)));
    }

    /// <summary>
    /// Normalizes OTAC code to uppercase for case-insensitive comparison
    /// </summary>
    /// <param name="otacCode">OTAC code to normalize</param>
    /// <returns>Normalized OTAC code</returns>
    public static string NormalizeOtacCode(string otacCode)
    {
        return otacCode?.ToUpper() ?? string.Empty;
    }
}