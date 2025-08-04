using System;
using System.Collections.Generic;

namespace BizConnect.Services.Models.Responses
{
    /// <summary>
    /// Statistical data for ODD registrations
    /// </summary>
    public class RegistrationStatistics
    {
        /// <summary>
        /// Total number of registrations
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
        /// Success rate as a percentage (0-100)
        /// </summary>
        public double SuccessRate => TotalRegistrations > 0 
            ? Math.Round((double)SuccessfulRegistrations / TotalRegistrations * 100, 2) 
            : 0;

        /// <summary>
        /// Number of registrations created today
        /// </summary>
        public int TodayRegistrations { get; set; }

        /// <summary>
        /// Number of registrations created this week
        /// </summary>
        public int WeekRegistrations { get; set; }

        /// <summary>
        /// Number of registrations created this month
        /// </summary>
        public int MonthRegistrations { get; set; }

        /// <summary>
        /// Registration counts by status
        /// </summary>
        public Dictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Registration counts by status (API compatible property)
        /// </summary>
        public Dictionary<string, int> StatusBreakdown { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Registration counts by branch
        /// </summary>
        public Dictionary<string, int> BranchCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Registration counts by day for the last 30 days
        /// </summary>
        public Dictionary<DateTime, int> DailyCounts { get; set; } = new Dictionary<DateTime, int>();

        /// <summary>
        /// Registration counts by time periods (API compatible property)
        /// </summary>
        public Dictionary<string, int> TimeBreakdown { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Average processing time in minutes for completed registrations
        /// </summary>
        public double? AverageProcessingTimeMinutes { get; set; }

        /// <summary>
        /// Most recent registration timestamp
        /// </summary>
        public DateTime? LastRegistrationAt { get; set; }

        /// <summary>
        /// Date range of the statistics
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date range of the statistics
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// When these statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}