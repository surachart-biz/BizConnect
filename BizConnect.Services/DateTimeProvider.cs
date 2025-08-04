using System;
using BizConnect.Services.Interfaces;

namespace BizConnect.Services
{
    /// <summary>
    /// Default implementation of IDateTimeProvider using system time
    /// </summary>
    public class DateTimeProvider : IDateTimeProvider
    {
        /// <summary>
        /// Gets the current UTC date and time
        /// </summary>
        public DateTime UtcNow => DateTime.UtcNow;

        /// <summary>
        /// Gets the current local date and time
        /// </summary>
        public DateTime Now => DateTime.Now;

        /// <summary>
        /// Gets the current date only (time set to 00:00:00)
        /// </summary>
        public DateTime Today => DateTime.Today;
    }
}