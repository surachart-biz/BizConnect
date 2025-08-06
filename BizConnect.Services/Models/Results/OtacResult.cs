using System;
using System.Diagnostics;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// OTAC-specific data container
    /// </summary>
    public class OtacInfo
    {
        /// <summary>
        /// The OTAC code (may be masked for security)
        /// </summary>
        public string OtacCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Legacy Code property for backward compatibility
        /// </summary>
        public string Code
        {
            get => OtacCode;
            set => OtacCode = value;
        }
        
        /// <summary>
        /// When the OTAC was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }
        
        /// <summary>
        /// When the OTAC expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        
        /// <summary>
        /// The associated registration ID
        /// </summary>
        public int RegistrationId { get; set; }
        
        /// <summary>
        /// Current status of the OTAC
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of validation attempts made
        /// </summary>
        public int AttemptCount { get; set; }
        
        /// <summary>
        /// Time remaining until expiry in seconds
        /// </summary>
        public int TimeRemainingSeconds => Math.Max(0, (int)(ExpiresAt - DateTime.UtcNow).TotalSeconds);
        
        /// <summary>
        /// Remaining validation attempts
        /// </summary>
        public int RemainingAttempts { get; set; } = 5;
        
        /// <summary>
        /// Purpose of the OTAC (legacy property)
        /// </summary>
        public string Purpose { get; set; } = string.Empty;
        
        /// <summary>
        /// Delivery method (legacy property)
        /// </summary>
        public string DeliveryMethod { get; set; } = "Display";
        
        /// <summary>
        /// Delivery destination (legacy property)
        /// </summary>
        public string DeliveryDestination { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result type specifically for OTAC operations
    /// </summary>
    public class OtacResult : Result<OtacInfo>
    {
        /// <summary>
        /// Convenience property for accessing the OTAC code
        /// </summary>
        public string? Code => Data?.Code;

        /// <summary>
        /// Convenience property for accessing expiration time
        /// </summary>
        public DateTime? ExpiresAt => Data?.ExpiresAt;

        /// <summary>
        /// Convenience property for accessing registration ID
        /// </summary>
        public int? RegistrationId => Data?.RegistrationId;

        /// <summary>
        /// Convenience property for accessing remaining attempts
        /// </summary>
        public int? RemainingAttempts => Data?.RemainingAttempts;

        /// <summary>
        /// Convenience property for accessing attempts remaining (alias for RemainingAttempts)
        /// </summary>
        public int AttemptsRemaining => RemainingAttempts ?? 0;

        /// <summary>
        /// Lockout time remaining in minutes (0 if not locked)
        /// </summary>
        public int LockoutTimeRemaining { get; set; } = 0;

        /// <summary>
        /// Creates a successful OTAC result
        /// </summary>
        public static OtacResult Success(string code, DateTime expiresAt, int registrationId, string purpose = "Registration", int remainingAttempts = 5)
        {
            return new OtacResult
            {
                IsSuccess = true,
                Data = new OtacInfo
                {
                    Code = code,
                    ExpiresAt = expiresAt,
                    RegistrationId = registrationId,
                    Purpose = purpose,
                    RemainingAttempts = remainingAttempts
                }
            };
        }

        /// <summary>
        /// Creates a successful OTAC result with full info
        /// </summary>
        public static OtacResult Success(OtacInfo otacInfo)
        {
            return new OtacResult
            {
                IsSuccess = true,
                Data = otacInfo
            };
        }

        /// <summary>
        /// Creates a failed OTAC result
        /// </summary>
        public new static OtacResult Failure(string errorMessage)
        {
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        /// <summary>
        /// Creates a failed OTAC result with exception
        /// </summary>
        public static OtacResult Failure(Exception ex)
        {
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }

        /// <summary>
        /// Creates a failed result for invalid OTAC code
        /// </summary>
        public static OtacResult InvalidCode(int remainingAttempts = 0, string language = "en")
        {
            var message = GetLocalizedInvalidCodeMessage(remainingAttempts, language);

            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for expired OTAC
        /// </summary>
        public static OtacResult ExpiredCode(string language = "en")
        {
            var message = GetLocalizedExpiredCodeMessage(language);
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for locked OTAC (too many attempts)
        /// </summary>
        public static OtacResult LockedCode(string language = "en")
        {
            var message = GetLocalizedLockedCodeMessage(language);
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for OTAC not found
        /// </summary>
        public static OtacResult NotFound(string language = "en")
        {
            var message = GetLocalizedNotFoundMessage(language);
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        #region Localized Error Messages

        private static string GetLocalizedInvalidCodeMessage(int remainingAttempts, string language)
        {
            return language.ToLower() == "th" 
                ? (remainingAttempts > 0 
                    ? $"รหัส OTAC ไม่ถูกต้อง เหลือโอกาสในการลอง {remainingAttempts} ครั้ง" 
                    : "รหัส OTAC ไม่ถูกต้อง ไม่มีโอกาสในการลองเหลืออยู่")
                : (remainingAttempts > 0 
                    ? $"Invalid OTAC code. {remainingAttempts} attempts remaining." 
                    : "Invalid OTAC code. No attempts remaining.");
        }

        private static string GetLocalizedExpiredCodeMessage(string language)
        {
            return language.ToLower() == "th" 
                ? "รหัส OTAC หมดอายุแล้ว กรุณาขอรหัสใหม่"
                : "OTAC code has expired. Please request a new code.";
        }

        private static string GetLocalizedLockedCodeMessage(string language)
        {
            return language.ToLower() == "th" 
                ? "รหัส OTAC ถูกล็อกเนื่องจากป้อนรหัสผิดหลายครั้ง กรุณาขอรหัสใหม่"
                : "OTAC code is locked due to too many failed attempts. Please request a new code.";
        }

        private static string GetLocalizedNotFoundMessage(string language)
        {
            return language.ToLower() == "th" 
                ? "ไม่พบรหัส OTAC หรือรหัสถูกใช้งานแล้ว"
                : "OTAC code not found or has been used.";
        }

        #endregion
    }
}