using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Registration-specific data container
    /// </summary>
    public class RegistrationInfo
    {
        public string RedirectUrl { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
        public string? RegId { get; set; }
        public Guid RegistrationId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompanyName { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result type specifically for registration operations
    /// </summary>
    public class RegistrationResult : Result<RegistrationInfo>
    {
        /// <summary>
        /// Convenience property for accessing redirect URL
        /// </summary>
        public string? RedirectUrl => Data?.RedirectUrl;

        /// <summary>
        /// Convenience property for accessing external reference
        /// </summary>
        public string? ExternalReference => Data?.ExternalReference;

        /// <summary>
        /// Convenience property for accessing RegId
        /// </summary>
        public string? RegId => Data?.RegId;

        /// <summary>
        /// Convenience property for accessing registration ID
        /// </summary>
        public Guid? RegistrationId => Data?.RegistrationId;

        /// <summary>
        /// Convenience property for accessing status
        /// </summary>
        public string? Status => Data?.Status;

        /// <summary>
        /// Creates a successful registration result
        /// </summary>
        public static RegistrationResult Success(string redirectUrl, string externalReference, string regId, Guid registrationId)
        {
            return new RegistrationResult
            {
                IsSuccess = true,
                Data = new RegistrationInfo
                {
                    RedirectUrl = redirectUrl,
                    ExternalReference = externalReference,
                    RegId = regId,
                    RegistrationId = registrationId,
                    Status = "Initiated",
                    CreatedAt = DateTime.UtcNow
                }
            };
        }

        /// <summary>
        /// Creates a successful registration result with full info
        /// </summary>
        public static RegistrationResult Success(RegistrationInfo registrationInfo)
        {
            return new RegistrationResult
            {
                IsSuccess = true,
                Data = registrationInfo
            };
        }

        /// <summary>
        /// Creates a failed registration result
        /// </summary>
        public new static RegistrationResult Failure(string errorMessage)
        {
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        /// <summary>
        /// Creates a failed registration result with multiple errors
        /// </summary>
        public static RegistrationResult Failure(string errorMessage, List<string> errors)
        {
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = errors ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates a failed registration result with exception
        /// </summary>
        public static RegistrationResult Failure(Exception ex)
        {
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }

        /// <summary>
        /// Creates a failed result for validation errors
        /// </summary>
        public static RegistrationResult ValidationFailure(Dictionary<string, List<string>> validationErrors)
        {
            var allErrors = new List<string>();
            foreach (var kvp in validationErrors)
            {
                allErrors.AddRange(kvp.Value);
            }

            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = "Validation failed",
                Errors = allErrors
            };
        }

        /// <summary>
        /// Creates a failed result for duplicate registration
        /// </summary>
        public static RegistrationResult DuplicateRegistration(string identifier)
        {
            var message = $"Registration already exists for: {identifier}";
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for registration not found
        /// </summary>
        public static RegistrationResult NotFound(Guid registrationId)
        {
            var message = $"Registration not found: {registrationId}";
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for expired registration
        /// </summary>
        public static RegistrationResult Expired(Guid registrationId)
        {
            var message = $"Registration has expired: {registrationId}";
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }

        /// <summary>
        /// Creates a failed result for external service error
        /// </summary>
        public static RegistrationResult ExternalServiceError(string serviceName, string error)
        {
            var message = $"External service error ({serviceName}): {error}";
            return new RegistrationResult
            {
                IsSuccess = false,
                ErrorMessage = message,
                Errors = new List<string> { message }
            };
        }
    }
}