using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Generic result pattern for consistent error handling and data return
    /// </summary>
    public class Result<T> where T : class
    {
        public bool IsSuccess { get; protected set; }
        public T? Data { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public List<string> Errors { get; protected set; } = new List<string>();
        public string? TraceId { get; protected set; }
        public DateTime Timestamp { get; protected set; }

        protected Result()
        {
            Timestamp = DateTime.UtcNow;
            TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Creates a successful result with data
        /// </summary>
        public static Result<T> Success(T data)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Data = data
            };
        }

        /// <summary>
        /// Creates a failed result with error message
        /// </summary>
        public static Result<T> Failure(string errorMessage)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        /// <summary>
        /// Creates a failed result with multiple errors
        /// </summary>
        public static Result<T> Failure(string errorMessage, List<string> errors)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = errors ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates a failed result with exception details
        /// </summary>
        public static Result<T> Failure(Exception ex)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }

        /// <summary>
        /// Adds an error to the existing result
        /// </summary>
        public Result<T> AddError(string error)
        {
            Errors.Add(error);
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = error;
            }
            return this;
        }

        /// <summary>
        /// Checks if result has any errors
        /// </summary>
        public bool HasErrors => Errors.Any();
    }

    /// <summary>
    /// Non-generic result for operations without return data
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public List<string> Errors { get; protected set; } = new List<string>();
        public string? TraceId { get; protected set; }
        public DateTime Timestamp { get; protected set; }

        protected Result()
        {
            Timestamp = DateTime.UtcNow;
            TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static Result Success()
        {
            return new Result
            {
                IsSuccess = true
            };
        }

        /// <summary>
        /// Creates a failed result with error message
        /// </summary>
        public static Result Failure(string errorMessage)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        /// <summary>
        /// Creates a failed result with multiple errors
        /// </summary>
        public static Result Failure(string errorMessage, List<string> errors)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = errors ?? new List<string>()
            };
        }

        /// <summary>
        /// Creates a failed result with exception details
        /// </summary>
        public static Result Failure(Exception ex)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }

        /// <summary>
        /// Adds an error to the existing result
        /// </summary>
        public Result AddError(string error)
        {
            Errors.Add(error);
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = error;
            }
            return this;
        }

        /// <summary>
        /// Checks if result has any errors
        /// </summary>
        public bool HasErrors => Errors.Any();
    }
}