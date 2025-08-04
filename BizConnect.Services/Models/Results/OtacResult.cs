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
        public string DeliveryMethod { get; set; } = "Email";
        
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
        public static OtacResult InvalidCode(int remainingAttempts = 0)
        {
            var message = remainingAttempts > 0 
                ? $"Invalid OTAC code. {remainingAttempts} attempts remaining." 
                : "Invalid OTAC code. No attempts remaining.";

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
        public static OtacResult ExpiredCode()
        {
            const string message = "OTAC code has expired. Please request a new code.";
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
        public static OtacResult LockedCode()
        {
            const string message = "OTAC code is locked due to too many failed attempts. Please request a new code.";
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
        public static OtacResult NotFound()
        {
            const string message = "OTAC code not found or has been used.";
            return new OtacResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }
    }
}