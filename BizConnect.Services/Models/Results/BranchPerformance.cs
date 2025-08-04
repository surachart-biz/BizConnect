using System;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Branch performance metrics
    /// </summary>
    public class BranchPerformance
    {
        /// <summary>
        /// Branch ID
        /// </summary>
        public int BranchId { get; set; }

        /// <summary>
        /// Branch name
        /// </summary>
        public string BranchName { get; set; } = string.Empty;

        /// <summary>
        /// Total number of registrations for this branch
        /// </summary>
        public int TotalRegistrations { get; set; }

        /// <summary>
        /// Number of successful registrations
        /// </summary>
        public int SuccessfulRegistrations { get; set; }

        /// <summary>
        /// Number of failed registrations
        /// </summary>
        public int FailedRegistrations { get; set; }

        /// <summary>
        /// Number of pending registrations
        /// </summary>
        public int PendingRegistrations { get; set; }

        /// <summary>
        /// Success rate as percentage
        /// </summary>
        public decimal SuccessRate => TotalRegistrations > 0 
            ? (decimal)SuccessfulRegistrations / TotalRegistrations * 100 
            : 0;

        /// <summary>
        /// Average processing time in minutes
        /// </summary>
        public decimal? AverageProcessingTimeMinutes { get; set; }

        /// <summary>
        /// Most recent registration date
        /// </summary>
        public DateTime? LastRegistrationAt { get; set; }

        /// <summary>
        /// Period start date for this performance data
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Period end date for this performance data
        /// </summary>
        public DateTime PeriodEnd { get; set; }
    }
}