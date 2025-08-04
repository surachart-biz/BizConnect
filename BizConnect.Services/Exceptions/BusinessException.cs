using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BizConnect.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when business rules are violated or business logic errors occur.
    /// These exceptions typically result in user-friendly error messages being displayed.
    /// </summary>
    [Serializable]
    public class BusinessException : Exception
    {
        /// <summary>
        /// Business error code for programmatic handling
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// User-friendly error message in Thai language
        /// </summary>
        public string UserMessage { get; }

        /// <summary>
        /// Additional context data for error handling
        /// </summary>
        public Dictionary<string, object> Context { get; }

        /// <summary>
        /// Indicates if this error should be logged as a warning rather than error
        /// </summary>
        public bool IsWarning { get; }

        public BusinessException(string message) : base(message)
        {
            Context = new Dictionary<string, object>();
        }

        public BusinessException(string message, Exception innerException) : base(message, innerException)
        {
            Context = new Dictionary<string, object>();
        }

        public BusinessException(string message, string errorCode, string userMessage = null, bool isWarning = false) 
            : base(message)
        {
            ErrorCode = errorCode;
            UserMessage = userMessage ?? message;
            IsWarning = isWarning;
            Context = new Dictionary<string, object>();
        }

        public BusinessException(string message, string errorCode, Dictionary<string, object> context, 
            string userMessage = null, bool isWarning = false) : base(message)
        {
            ErrorCode = errorCode;
            UserMessage = userMessage ?? message;
            IsWarning = isWarning;
            Context = context ?? new Dictionary<string, object>();
        }

        protected BusinessException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            ErrorCode = info.GetString(nameof(ErrorCode));
            UserMessage = info.GetString(nameof(UserMessage));
            IsWarning = info.GetBoolean(nameof(IsWarning));
            Context = new Dictionary<string, object>();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(UserMessage), UserMessage);
            info.AddValue(nameof(IsWarning), IsWarning);
        }

        /// <summary>
        /// Add context data to the exception
        /// </summary>
        public BusinessException WithContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple context data entries
        /// </summary>
        public BusinessException WithContext(Dictionary<string, object> additionalContext)
        {
            if (additionalContext != null)
            {
                foreach (var kvp in additionalContext)
                {
                    Context[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }
    }

    /// <summary>
    /// Common business error codes for consistent error handling
    /// </summary>
    public static class BusinessErrorCodes
    {
        // User Management
        public const string UserNotFound = "USER_NOT_FOUND";
        public const string UserAlreadyExists = "USER_ALREADY_EXISTS";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string AccountLocked = "ACCOUNT_LOCKED";
        public const string PasswordExpired = "PASSWORD_EXPIRED";
        public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";

        // OTAC Management
        public const string OtacNotFound = "OTAC_NOT_FOUND";
        public const string OtacExpired = "OTAC_EXPIRED";
        public const string OtacAlreadyUsed = "OTAC_ALREADY_USED";
        public const string OtacLocked = "OTAC_LOCKED";
        public const string OtacGenerationFailed = "OTAC_GENERATION_FAILED";
        public const string OtacDeliveryFailed = "OTAC_DELIVERY_FAILED";
        public const string TooManyOtacAttempts = "TOO_MANY_OTAC_ATTEMPTS";

        // Registration Management
        public const string RegistrationNotFound = "REGISTRATION_NOT_FOUND";
        public const string RegistrationAlreadyExists = "REGISTRATION_ALREADY_EXISTS";
        public const string RegistrationExpired = "REGISTRATION_EXPIRED";
        public const string RegistrationCompleted = "REGISTRATION_COMPLETED";
        public const string InvalidRegistrationData = "INVALID_REGISTRATION_DATA";

        // KBank Integration
        public const string KbankServiceUnavailable = "KBANK_SERVICE_UNAVAILABLE";
        public const string KbankInvalidResponse = "KBANK_INVALID_RESPONSE";
        public const string KbankAuthenticationFailed = "KBANK_AUTH_FAILED";
        public const string PaymentProcessingFailed = "PAYMENT_PROCESSING_FAILED";

        // Data Validation
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string RequiredFieldMissing = "REQUIRED_FIELD_MISSING";
        public const string InvalidFormat = "INVALID_FORMAT";
        public const string DuplicateEntry = "DUPLICATE_ENTRY";

        // System Errors
        public const string SystemUnavailable = "SYSTEM_UNAVAILABLE";
        public const string ConfigurationError = "CONFIGURATION_ERROR";
        public const string ExternalServiceError = "EXTERNAL_SERVICE_ERROR";
        public const string DatabaseError = "DATABASE_ERROR";

        // Rate Limiting
        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
        public const string TooManyRequests = "TOO_MANY_REQUESTS";
        public const string ConcurrentAccessDenied = "CONCURRENT_ACCESS_DENIED";
    }
}