using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Validation-specific result for handling field-level validation errors
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public Dictionary<string, List<string>> ValidationErrors { get; private set; }
        public List<string> GeneralErrors { get; private set; }
        public string? TraceId { get; private set; }
        public DateTime Timestamp { get; private set; }

        public ValidationResult()
        {
            ValidationErrors = new Dictionary<string, List<string>>();
            GeneralErrors = new List<string>();
            Timestamp = DateTime.UtcNow;
            TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            IsValid = true;
        }

        /// <summary>
        /// Creates a valid result
        /// </summary>
        public static ValidationResult Valid()
        {
            return new ValidationResult
            {
                IsValid = true
            };
        }

        /// <summary>
        /// Creates an invalid result with field-specific errors
        /// </summary>
        public static ValidationResult Invalid(Dictionary<string, List<string>> fieldErrors)
        {
            return new ValidationResult
            {
                IsValid = false,
                ValidationErrors = fieldErrors ?? new Dictionary<string, List<string>>()
            };
        }

        /// <summary>
        /// Creates an invalid result with a single field error
        /// </summary>
        public static ValidationResult Invalid(string fieldName, string error)
        {
            var errors = new Dictionary<string, List<string>>
            {
                { fieldName, new List<string> { error } }
            };

            return new ValidationResult
            {
                IsValid = false,
                ValidationErrors = errors
            };
        }

        /// <summary>
        /// Creates an invalid result with multiple errors for a single field
        /// </summary>
        public static ValidationResult Invalid(string fieldName, List<string> errors)
        {
            var fieldErrors = new Dictionary<string, List<string>>
            {
                { fieldName, errors ?? new List<string>() }
            };

            return new ValidationResult
            {
                IsValid = false,
                ValidationErrors = fieldErrors
            };
        }

        /// <summary>
        /// Creates an invalid result with general errors (not field-specific)
        /// </summary>
        public static ValidationResult Invalid(List<string> generalErrors)
        {
            return new ValidationResult
            {
                IsValid = false,
                GeneralErrors = generalErrors ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates an invalid result with a single general error
        /// </summary>
        public static ValidationResult Invalid(string generalError)
        {
            return new ValidationResult
            {
                IsValid = false,
                GeneralErrors = new List<string> { generalError }
            };
        }

        /// <summary>
        /// Adds a field-specific validation error
        /// </summary>
        public ValidationResult AddFieldError(string fieldName, string error)
        {
            if (!ValidationErrors.ContainsKey(fieldName))
            {
                ValidationErrors[fieldName] = new List<string>();
            }

            ValidationErrors[fieldName].Add(error);
            IsValid = false;
            return this;
        }

        /// <summary>
        /// Adds multiple errors for a specific field
        /// </summary>
        public ValidationResult AddFieldErrors(string fieldName, List<string> errors)
        {
            if (!ValidationErrors.ContainsKey(fieldName))
            {
                ValidationErrors[fieldName] = new List<string>();
            }

            ValidationErrors[fieldName].AddRange(errors);
            IsValid = false;
            return this;
        }

        /// <summary>
        /// Adds a general validation error
        /// </summary>
        public ValidationResult AddGeneralError(string error)
        {
            GeneralErrors.Add(error);
            IsValid = false;
            return this;
        }

        /// <summary>
        /// Adds multiple general validation errors
        /// </summary>
        public ValidationResult AddGeneralErrors(List<string> errors)
        {
            GeneralErrors.AddRange(errors);
            IsValid = false;
            return this;
        }

        /// <summary>
        /// Merges another validation result into this one
        /// </summary>
        public ValidationResult Merge(ValidationResult other)
        {
            if (other == null) return this;

            foreach (var kvp in other.ValidationErrors)
            {
                if (!ValidationErrors.ContainsKey(kvp.Key))
                {
                    ValidationErrors[kvp.Key] = new List<string>();
                }
                ValidationErrors[kvp.Key].AddRange(kvp.Value);
            }

            GeneralErrors.AddRange(other.GeneralErrors);

            if (!other.IsValid)
            {
                IsValid = false;
            }

            return this;
        }

        /// <summary>
        /// Gets all errors as a flat list
        /// </summary>
        public List<string> GetAllErrors()
        {
            var allErrors = new List<string>();
            allErrors.AddRange(GeneralErrors);

            foreach (var fieldErrors in ValidationErrors.Values)
            {
                allErrors.AddRange(fieldErrors);
            }

            return allErrors;
        }

        /// <summary>
        /// Gets errors for a specific field
        /// </summary>
        public List<string> GetFieldErrors(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) 
                ? ValidationErrors[fieldName] 
                : new List<string>();
        }

        /// <summary>
        /// Checks if a specific field has errors
        /// </summary>
        public bool HasFieldError(string fieldName)
        {
            return ValidationErrors.ContainsKey(fieldName) && ValidationErrors[fieldName].Any();
        }

        /// <summary>
        /// Gets the total count of all errors
        /// </summary>
        public int ErrorCount => ValidationErrors.Values.Sum(errors => errors.Count) + GeneralErrors.Count;

        /// <summary>
        /// Converts validation result to a generic Result<T>
        /// </summary>
        public Result<T> ToResult<T>(T? data = null) where T : class
        {
            if (IsValid)
            {
                return Result<T>.Success(data);
            }

            var allErrors = GetAllErrors();
            var errorMessage = allErrors.FirstOrDefault() ?? "Validation failed";
            return Result<T>.Failure(errorMessage, allErrors);
        }

        /// <summary>
        /// Converts validation result to a non-generic Result
        /// </summary>
        public Result ToResult()
        {
            if (IsValid)
            {
                return Result.Success();
            }

            var allErrors = GetAllErrors();
            var errorMessage = allErrors.FirstOrDefault() ?? "Validation failed";
            return Result.Failure(errorMessage, allErrors);
        }
    }
}