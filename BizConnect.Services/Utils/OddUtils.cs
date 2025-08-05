using System.Security.Cryptography;
using System.Text;

namespace BizConnect.Services.Utils;

/// <summary>
/// Utility class for KBank Online Direct Debit (ODD) operations
/// </summary>
public static class OddUtils
{
    /// <summary>
    /// Builds authentication hash using SHA-256 for KBank ODD API
    /// </summary>
    /// <param name="passPhrase">The pass phrase from configuration</param>
    /// <param name="parameters">Parameters to concatenate with pass phrase</param>
    /// <returns>Upper-case SHA-256 hex string</returns>
    /// <exception cref="ArgumentNullException">Thrown when passPhrase is null or empty</exception>
    /// <exception cref="ArgumentException">Thrown when no parameters are provided</exception>
    public static string BuildAuth(string passPhrase, params string[] parameters)
    {
        if (string.IsNullOrEmpty(passPhrase))
            throw new ArgumentNullException(nameof(passPhrase), "Pass phrase cannot be null or empty");

        if (parameters == null || parameters.Length == 0)
            throw new ArgumentException("At least one parameter must be provided", nameof(parameters));

        // Concatenate pass phrase with all parameters (no spaces or commas)
        var concatenated = passPhrase + string.Join("", parameters);

        // Generate SHA-256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(concatenated));

        // Convert to upper-case hex string
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Generates external reference in the format BIZyyyyMMddHHmmssfff with microsecond precision
    /// Uses Guid suffix to ensure uniqueness in high-concurrency scenarios
    /// </summary>
    /// <returns>External reference string</returns>
    public static string GenerateExternalReference()
    {
        var now = DateTime.Now;
        var guid = Guid.NewGuid().ToString("N")[..4]; // Take first 4 chars of GUID for uniqueness
        return $"BIZ{now:yyyyMMddHHmmssfff}{guid}";
    }

    /// <summary>
    /// Validates external reference format
    /// </summary>
    /// <param name="externalReference">External reference to validate</param>
    /// <returns>True if format is valid, false otherwise</returns>
    public static bool IsValidExternalReference(string? externalReference)
    {
        if (string.IsNullOrEmpty(externalReference))
            return false;

        if (!externalReference.StartsWith("BIZ"))
            return false;

        // Support both old format (20 chars) and new format (24 chars with GUID suffix)
        if (externalReference.Length == 20)
        {
            // Old format: BIZyyyyMMddHHmmssfff
            var dateTimePart = externalReference.Substring(3);
            return DateTime.TryParseExact(dateTimePart, "yyyyMMddHHmmssfff", null, 
                System.Globalization.DateTimeStyles.None, out _);
        }
        else if (externalReference.Length == 24)
        {
            // New format: BIZyyyyMMddHHmmssfff + 4-char GUID suffix
            var dateTimePart = externalReference.Substring(3, 17);
            var guidPart = externalReference.Substring(20, 4);
            
            // Validate datetime part
            var isValidDateTime = DateTime.TryParseExact(dateTimePart, "yyyyMMddHHmmssfff", null, 
                System.Globalization.DateTimeStyles.None, out _);
            
            // Validate GUID part (should be hex characters)
            var isValidGuid = guidPart.All(c => "0123456789abcdefABCDEF".Contains(c));
            
            return isValidDateTime && isValidGuid;
        }

        return false;
    }
}
