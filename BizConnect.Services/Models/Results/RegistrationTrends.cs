using System;
using System.Collections.Generic;

namespace BizConnect.Services.Models.Results
{
    /// <summary>
    /// Registration trends data over time
    /// </summary>
    public class RegistrationTrends
    {
        /// <summary>
        /// Daily registration counts by date
        /// </summary>
        public Dictionary<DateTime, int> DailyCounts { get; set; } = new();

        /// <summary>
        /// Daily success counts by date
        /// </summary>
        public Dictionary<DateTime, int> DailySuccessCounts { get; set; } = new();

        /// <summary>
        /// Daily failure counts by date
        /// </summary>
        public Dictionary<DateTime, int> DailyFailureCounts { get; set; } = new();

        /// <summary>
        /// Overall success rate as percentage
        /// </summary>
        public decimal OverallSuccessRate { get; set; }

        /// <summary>
        /// Trend direction (increasing, decreasing, stable)
        /// </summary>
        public string TrendDirection { get; set; } = string.Empty;

        /// <summary>
        /// Peak registration day
        /// </summary>
        public DateTime? PeakDay { get; set; }

        /// <summary>
        /// Peak registration count
        /// </summary>
        public int PeakCount { get; set; }

        /// <summary>
        /// Average daily registrations
        /// </summary>
        public decimal AverageDailyCount { get; set; }

        /// <summary>
        /// Period start date
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Period end date
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Number of days analyzed
        /// </summary>
        public int DaysAnalyzed { get; set; }

        /// <summary>
        /// When the trends were calculated
        /// </summary>
        public DateTime GeneratedAt { get; set; }
    }
}