using System;
using System.Collections.Generic;

namespace BizConnect.Services.Common
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
    /// Provides a structured way to handle errors without throwing exceptions.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Indicates whether the operation was successful
        /// </summary>
        public bool IsSuccess { get; protected set; }

        /// <summary>
        /// Indicates whether the operation failed
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Error message when the operation fails
        /// </summary>
        public string ErrorMessage { get; protected set; }

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string ErrorCode { get; protected set; }

        /// <summary>
        /// Additional context data for the result
        /// </summary>
        public Dictionary<string, object> Context { get; protected set; }

        /// <summary>
        /// Exception that caused the failure (if any)
        /// </summary>
        public Exception Exception { get; protected set; }

        protected Result(bool isSuccess, string errorMessage = null, string errorCode = null, 
            Exception exception = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Exception = exception;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static Result Success()
        {
            return new Result(true);
        }

        /// <summary>
        /// Creates a failed result with an error message
        /// </summary>
        public static Result Failure(string errorMessage, string errorCode = null)
        {
            return new Result(false, errorMessage, errorCode);
        }

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        public static Result Failure(Exception exception, string errorCode = null)
        {
            return new Result(false, exception.Message, errorCode, exception);
        }

        /// <summary>
        /// Creates a failed result with detailed error information
        /// </summary>
        public static Result Failure(string errorMessage, string errorCode, Exception exception)
        {
            return new Result(false, errorMessage, errorCode, exception);
        }

        /// <summary>
        /// Add context data to the result
        /// </summary>
        public Result WithContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple context data entries
        /// </summary>
        public Result WithContext(Dictionary<string, object> additionalContext)
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

        /// <summary>
        /// Execute an action if the result is successful
        /// </summary>
        public Result OnSuccess(Action action)
        {
            if (IsSuccess)
            {
                action();
            }
            return this;
        }

        /// <summary>
        /// Execute an action if the result is a failure
        /// </summary>
        public Result OnFailure(Action<string, string> action)
        {
            if (IsFailure)
            {
                action(ErrorMessage, ErrorCode);
            }
            return this;
        }

        /// <summary>
        /// Transform the result if successful
        /// </summary>
        public Result<T> Map<T>(Func<T> mapper)
        {
            if (IsSuccess)
            {
                try
                {
                    var value = mapper();
                    return Result<T>.Success(value).WithContext(Context);
                }
                catch (Exception ex)
                {
                    return Result<T>.Failure(ex).WithContext(Context);
                }
            }

            return Result<T>.Failure(ErrorMessage, ErrorCode, Exception).WithContext(Context);
        }
    }

    /// <summary>
    /// Represents the result of an operation that can either succeed with a value or fail.
    /// </summary>
    /// <typeparam name="T">The type of value returned on success</typeparam>
    public class Result<T> : Result
    {
        /// <summary>
        /// The value returned when the operation is successful
        /// </summary>
        public T Value { get; private set; }

        private Result(bool isSuccess, T value = default, string errorMessage = null, 
            string errorCode = null, Exception exception = null) 
            : base(isSuccess, errorMessage, errorCode, exception)
        {
            Value = value;
        }

        /// <summary>
        /// Creates a successful result with a value
        /// </summary>
        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value);
        }

        /// <summary>
        /// Creates a failed result with an error message
        /// </summary>
        public static new Result<T> Failure(string errorMessage, string errorCode = null)
        {
            return new Result<T>(false, default, errorMessage, errorCode);
        }

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        public static new Result<T> Failure(Exception exception, string errorCode = null)
        {
            return new Result<T>(false, default, exception.Message, errorCode, exception);
        }

        /// <summary>
        /// Creates a failed result with detailed error information
        /// </summary>
        public static new Result<T> Failure(string errorMessage, string errorCode, Exception exception)
        {
            return new Result<T>(false, default, errorMessage, errorCode, exception);
        }

        /// <summary>
        /// Add context data to the result
        /// </summary>
        public new Result<T> WithContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple context data entries
        /// </summary>
        public new Result<T> WithContext(Dictionary<string, object> additionalContext)
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

        /// <summary>
        /// Execute an action with the value if the result is successful
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess)
            {
                action(Value);
            }
            return this;
        }

        /// <summary>
        /// Execute an action if the result is a failure
        /// </summary>
        public new Result<T> OnFailure(Action<string, string> action)
        {
            if (IsFailure)
            {
                action(ErrorMessage, ErrorCode);
            }
            return this;
        }

        /// <summary>
        /// Transform the value if the result is successful
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        {
            if (IsSuccess)
            {
                try
                {
                    var newValue = mapper(Value);
                    return Result<TOut>.Success(newValue).WithContext(Context);
                }
                catch (Exception ex)
                {
                    return Result<TOut>.Failure(ex).WithContext(Context);
                }
            }

            return Result<TOut>.Failure(ErrorMessage, ErrorCode, Exception).WithContext(Context);
        }

        /// <summary>
        /// Chain operations that return Results
        /// </summary>
        public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
        {
            if (IsSuccess)
            {
                try
                {
                    var result = binder(Value);
                    return result.WithContext(Context);
                }
                catch (Exception ex)
                {
                    return Result<TOut>.Failure(ex).WithContext(Context);
                }
            }

            return Result<TOut>.Failure(ErrorMessage, ErrorCode, Exception).WithContext(Context);
        }

        /// <summary>
        /// Get the value or throw an exception if the result is a failure
        /// </summary>
        public T GetValueOrThrow()
        {
            if (IsFailure)
            {
                throw Exception ?? new InvalidOperationException(ErrorMessage ?? "Operation failed");
            }
            return Value;
        }

        /// <summary>
        /// Get the value or return a default value if the result is a failure
        /// </summary>
        public T GetValueOrDefault(T defaultValue = default)
        {
            return IsSuccess ? Value : defaultValue;
        }

        /// <summary>
        /// Implicit conversion from T to Result<T>
        /// </summary>
        public static implicit operator Result<T>(T value)
        {
            return Success(value);
        }

        /// <summary>
        /// Implicit conversion to bool (true if successful)
        /// </summary>
        public static implicit operator bool(Result<T> result)
        {
            return result.IsSuccess;
        }
    }

    /// <summary>
    /// Helper methods for working with Results
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>
        /// Execute an async action and wrap the result
        /// </summary>
        public static async Task<Result> TryAsync(Func<Task> action, string errorCode = null)
        {
            try
            {
                await action();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex, errorCode);
            }
        }

        /// <summary>
        /// Execute an async function and wrap the result
        /// </summary>
        public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func, string errorCode = null)
        {
            try
            {
                var value = await func();
                return Result<T>.Success(value);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex, errorCode);
            }
        }

        /// <summary>
        /// Execute a synchronous action and wrap the result
        /// </summary>
        public static Result Try(Action action, string errorCode = null)
        {
            try
            {
                action();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure(ex, errorCode);
            }
        }

        /// <summary>
        /// Execute a synchronous function and wrap the result
        /// </summary>
        public static Result<T> Try<T>(Func<T> func, string errorCode = null)
        {
            try
            {
                var value = func();
                return Result<T>.Success(value);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure(ex, errorCode);
            }
        }
    }
}