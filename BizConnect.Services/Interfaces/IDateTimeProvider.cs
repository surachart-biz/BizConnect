using System;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Interface for providing current date and time values.
    /// This abstraction enables testable time-dependent operations.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Gets the current UTC date and time
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// Gets the current local date and time
        /// </summary>
        DateTime Now { get; }

        /// <summary>
        /// Gets the current date only (time set to 00:00:00)
        /// </summary>
        DateTime Today { get; }
    }
}