using System.Security.Claims;

namespace BizConnect.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to simplify user identity access
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the user ID from claims with proper error handling
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>User ID as integer, or 0 if not found or invalid</returns>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Gets the username from claims with fallback
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>Username or "Unknown" if not found</returns>
    public static string GetUserName(this ClaimsPrincipal principal)
    {
        return principal?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }

    /// <summary>
    /// Gets the user's primary role
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>Primary role or empty string if not found</returns>
    public static string GetUserRole(this ClaimsPrincipal principal)
    {
        return principal?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Checks if user is in any of the specified roles
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <param name="roles">Array of role names to check</param>
    /// <returns>True if user has any of the specified roles</returns>
    public static bool IsInAnyRole(this ClaimsPrincipal principal, params string[] roles)
    {
        if (principal == null || roles == null || roles.Length == 0)
            return false;

        return roles.Any(role => principal.IsInRole(role));
    }

    /// <summary>
    /// Convenience method to check if user is Admin
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>True if user has Admin role</returns>
    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal?.IsInRole("Admin") == true;
    }

    /// <summary>
    /// Convenience method to check if user is Admin or Employee
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>True if user has Admin or Employee role</returns>
    public static bool IsAdminOrEmployee(this ClaimsPrincipal principal)
    {
        return principal?.IsInAnyRole("Admin", "Employee") == true;
    }

    /// <summary>
    /// Gets the user's email from claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>Email address or empty string if not found</returns>
    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        return principal?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Gets all roles for the user
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>List of role names</returns>
    public static IEnumerable<string> GetUserRoles(this ClaimsPrincipal principal)
    {
        if (principal == null)
            return Enumerable.Empty<string>();

        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }
}