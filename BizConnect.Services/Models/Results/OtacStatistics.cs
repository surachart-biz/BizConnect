using System;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Statistics about OTAC usage over a time period
    /// </summary>
    public class OtacStatistics
    {
        /// <summary>
        /// Total number of OTAC codes generated
        /// </summary>
        public int TotalGenerated { get; set; }

        /// <summary>
        /// Number of OTAC codes successfully validated
        /// </summary>
        public int TotalValidated { get; set; }

        /// <summary>
        /// Number of OTAC codes that expired without validation
        /// </summary>
        public int TotalExpired { get; set; }

        /// <summary>
        /// Number of OTAC codes that were locked due to too many attempts
        /// </summary>
        public int TotalLocked { get; set; }

        /// <summary>
        /// Number of OTAC codes that were invalidated
        /// </summary>
        public int TotalInvalidated { get; set; }

        /// <summary>
        /// Success rate as a percentage (validated / generated * 100)
        /// </summary>
        public decimal SuccessRate => TotalGenerated > 0 ? (decimal)TotalValidated / TotalGenerated * 100 : 0;

        /// <summary>
        /// Average validation attempts per code
        /// </summary>
        public decimal AverageAttempts { get; set; }

        /// <summary>
        /// Period these statistics cover
        /// </summary>
        public TimeSpan Period { get; set; }

        /// <summary>
        /// Start of the statistics period
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// End of the statistics period
        /// </summary>
        public DateTime PeriodEnd { get; set; }
    }
}