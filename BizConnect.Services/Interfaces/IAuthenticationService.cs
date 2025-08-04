using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service for handling authentication operations following 3-tier architecture
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticate a user with username and password
        /// </summary>
        Task<AuthenticationResult> AuthenticateAsync(string username, string password, string ipAddress, string userAgent = null);

        /// <summary>
        /// Create claims principal for authenticated user
        /// </summary>
        ClaimsPrincipal CreateClaimsPrincipal(User user, string ipAddress);

        /// <summary>
        /// Validate if a session is still valid
        /// </summary>
        Task<bool> ValidateSessionAsync(ClaimsPrincipal principal);

        /// <summary>
        /// Handle logout operations
        /// </summary>
        Task LogoutAsync(string username, string ipAddress);

        /// <summary>
        /// Change user password with validation
        /// </summary>
        Task<PasswordChangeResult> ChangePasswordAsync(string username, string currentPassword, string newPassword, string changedBy, string ipAddress);

        /// <summary>
        /// Reset user password (admin operation)
        /// </summary>
        Task<PasswordResetResult> ResetPasswordAsync(string username, string newPassword, string resetBy, string ipAddress);

        /// <summary>
        /// Validate password against policy
        /// </summary>
        Task<PasswordValidationResult> ValidatePasswordAsync(string password, string username = null);

        /// <summary>
        /// Get authentication configuration
        /// </summary>
        AuthenticationConfiguration GetConfiguration();
    }

    /// <summary>
    /// Result of authentication attempt
    /// </summary>
    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public User User { get; set; }
        public string FailureReason { get; set; }
        public bool IsAccountLocked { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public DateTime? LockoutEndTime { get; set; }
    }

    /// <summary>
    /// Result of password change operation
    /// </summary>
    public class PasswordChangeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string[] ValidationErrors { get; set; }
    }

    /// <summary>
    /// Result of password reset operation
    /// </summary>
    public class PasswordResetResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool RequiresUserNotification { get; set; }
    }


    /// <summary>
    /// Authentication configuration settings
    /// </summary>
    public class AuthenticationConfiguration
    {
        public int MaxLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 15;
        public int SessionTimeoutMinutes { get; set; } = 30;
        public bool RequireTwoFactor { get; set; } = false;
        public int PasswordExpirationDays { get; set; } = 90;
        public int PasswordHistoryCount { get; set; } = 5;
        public bool AllowRememberMe { get; set; } = true;
        public int RememberMeDays { get; set; } = 7;
    }
}