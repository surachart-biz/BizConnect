using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BizConnect.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when validation errors occur during data processing.
    /// Contains detailed field-level validation errors for user feedback.
    /// </summary>
    [Serializable]
    public class ValidationException : Exception
    {
        /// <summary>
        /// Collection of validation errors organized by field name
        /// </summary>
        public Dictionary<string, List<string>> ValidationErrors { get; }

        /// <summary>
        /// Global validation errors not specific to any field
        /// </summary>
        public List<string> GlobalErrors { get; }

        /// <summary>
        /// Validation context information
        /// </summary>
        public string ValidationContext { get; }

        /// <summary>
        /// The entity or object type being validated
        /// </summary>
        public string EntityType { get; }

        public ValidationException(string message) : base(message)
        {
            ValidationErrors = new Dictionary<string, List<string>>();
            GlobalErrors = new List<string>();
        }

        public ValidationException(string message, Exception innerException) : base(message, innerException)
        {
            ValidationErrors = new Dictionary<string, List<string>>();
            GlobalErrors = new List<string>();
        }

        public ValidationException(Dictionary<string, List<string>> validationErrors, string message = null) 
            : base(message ?? "Validation failed")
        {
            ValidationErrors = validationErrors ?? new Dictionary<string, List<string>>();
            GlobalErrors = new List<string>();
        }

        public ValidationException(string fieldName, string errorMessage, string message = null) 
            : base(message ?? $"Validation failed for field '{fieldName}'")
        {
            ValidationErrors = new Dictionary<string, List<string>>
            {
                { fieldName, new List<string> { errorMessage } }
            };
            GlobalErrors = new List<string>();
        }

        public ValidationException(Dictionary<string, List<string>> validationErrors, List<string> globalErrors, 
            string entityType = null, string validationContext = null, string message = null) 
            : base(message ?? "Validation failed")
        {
            ValidationErrors = validationErrors ?? new Dictionary<string, List<string>>();
            GlobalErrors = globalErrors ?? new List<string>();
            EntityType = entityType;
            ValidationContext = validationContext;
        }

        protected ValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            ValidationErrors = new Dictionary<string, List<string>>();
            GlobalErrors = new List<string>();
            EntityType = info.GetString(nameof(EntityType));
            ValidationContext = info.GetString(nameof(ValidationContext));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(EntityType), EntityType);
            info.AddValue(nameof(ValidationContext), ValidationContext);
        }

        /// <summary>
        /// Add a validation error for a specific field
        /// </summary>
        public ValidationException AddFieldError(string fieldName, string errorMessage)
        {
            if (!ValidationErrors.ContainsKey(fieldName))
            {
                ValidationErrors[fieldName] = new List<string>();
            }
            ValidationErrors[fieldName].Add(errorMessage);
            return this;
        }

        /// <summary>
        /// Add multiple validation errors for a specific field
        /// </summary>
        public ValidationException AddFieldErrors(string fieldName, IEnumerable<string> errorMessages)
        {
            if (!ValidationErrors.ContainsKey(fieldName))
            {
                ValidationErrors[fieldName] = new List<string>();
            }
            ValidationErrors[fieldName].AddRange(errorMessages);
            return this;
        }

        /// <summary>
        /// Add a global validation error
        /// </summary>
        public ValidationException AddGlobalError(string errorMessage)
        {
            GlobalErrors.Add(errorMessage);
            return this;
        }

        /// <summary>
        /// Add multiple global validation errors
        /// </summary>
        public ValidationException AddGlobalErrors(IEnumerable<string> errorMessages)
        {
            GlobalErrors.AddRange(errorMessages);
            return this;
        }

        /// <summary>
        /// Check if there are any validation errors
        /// </summary>
        public bool HasErrors => ValidationErrors.Any(kvp => kvp.Value.Any()) || GlobalErrors.Any();

        /// <summary>
        /// Get total count of all validation errors
        /// </summary>
        public int ErrorCount => ValidationErrors.Values.SelectMany(errors => errors).Count() + GlobalErrors.Count;

        /// <summary>
        /// Get all validation errors as flat list with field context
        /// </summary>
        public List<string> GetAllErrors()
        {
            var allErrors = new List<string>();
            
            // Add field-specific errors with context
            foreach (var kvp in ValidationErrors)
            {
                foreach (var error in kvp.Value)
                {
                    allErrors.Add($"{kvp.Key}: {error}");
                }
            }
            
            // Add global errors
            allErrors.AddRange(GlobalErrors);
            
            return allErrors;
        }

        /// <summary>
        /// Get validation errors for a specific field
        /// </summary>
        public List<string> GetFieldErrors(string fieldName)
        {
            return ValidationErrors.TryGetValue(fieldName, out var errors) ? errors : new List<string>();
        }

        /// <summary>
        /// Check if a specific field has validation errors
        /// </summary>
        public bool HasFieldErrors(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) && ValidationErrors[fieldName].Any();
        }

        /// <summary>
        /// Get a summary message of all validation errors
        /// </summary>
        public string GetSummaryMessage()
        {
            if (!HasErrors)
                return "No validation errors";

            var errorMessages = GetAllErrors();
            if (errorMessages.Count == 1)
                return errorMessages[0];

            return $"Multiple validation errors occurred: {string.Join("; ", errorMessages)}";
        }
    }

    /// <summary>
    /// Builder class for constructing ValidationException with fluent syntax
    /// </summary>
    public class ValidationExceptionBuilder
    {
        private readonly Dictionary<string, List<string>> _validationErrors = new();
        private readonly List<string> _globalErrors = new();
        private string _entityType;
        private string _validationContext;
        private string _message;

        public static ValidationExceptionBuilder Create(string message = null)
        {
            return new ValidationExceptionBuilder { _message = message };
        }

        public ValidationExceptionBuilder WithFieldError(string fieldName, string errorMessage)
        {
            if (!_validationErrors.ContainsKey(fieldName))
            {
                _validationErrors[fieldName] = new List<string>();
            }
            _validationErrors[fieldName].Add(errorMessage);
            return this;
        }

        public ValidationExceptionBuilder WithFieldErrors(string fieldName, IEnumerable<string> errorMessages)
        {
            if (!_validationErrors.ContainsKey(fieldName))
            {
                _validationErrors[fieldName] = new List<string>();
            }
            _validationErrors[fieldName].AddRange(errorMessages);
            return this;
        }

        public ValidationExceptionBuilder WithGlobalError(string errorMessage)
        {
            _globalErrors.Add(errorMessage);
            return this;
        }

        public ValidationExceptionBuilder WithGlobalErrors(IEnumerable<string> errorMessages)
        {
            _globalErrors.AddRange(errorMessages);
            return this;
        }

        public ValidationExceptionBuilder WithEntityType(string entityType)
        {
            _entityType = entityType;
            return this;
        }

        public ValidationExceptionBuilder WithContext(string validationContext)
        {
            _validationContext = validationContext;
            return this;
        }

        public ValidationExceptionBuilder WithMessage(string message)
        {
            _message = message;
            return this;
        }

        public ValidationException Build()
        {
            return new ValidationException(_validationErrors, _globalErrors, _entityType, _validationContext, _message);
        }

        public void ThrowIfHasErrors()
        {
            if (_validationErrors.Any(kvp => kvp.Value.Any()) || _globalErrors.Any())
            {
                throw Build();
            }
        }
    }
}