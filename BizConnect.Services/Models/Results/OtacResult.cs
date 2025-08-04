using System;
using System.Diagnostics;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// OTAC-specific data container
    /// </summary>
    public class OtacInfo
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid RegistrationId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public int RemainingAttempts { get; set; }
        public string DeliveryMethod { get; set; } = "Email";
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
        public Guid? RegistrationId => Data?.RegistrationId;

        /// <summary>
        /// Convenience property for accessing remaining attempts
        /// </summary>
        public int? RemainingAttempts => Data?.RemainingAttempts;

        /// <summary>
        /// Creates a successful OTAC result
        /// </summary>
        public static OtacResult Success(string code, DateTime expiresAt, Guid registrationId, string purpose = "Registration", int remainingAttempts = 5)
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