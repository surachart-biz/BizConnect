using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for payment processing and reconciliation operations
/// </summary>
public interface IPaymentProcessingService
{
    /// <summary>
    /// Executes daily payment processing tasks including reconciliation and cleanup
    /// </summary>
    /// <returns>Processing results with statistics</returns>
    Task<DailyProcessingResult> ExecuteDailyProcessingAsync();

    /// <summary>
    /// Generates a daily reconciliation report for KBank ODD registrations
    /// </summary>
    /// <param name="reportDate">Date for the report (defaults to yesterday)</param>
    /// <returns>Reconciliation report with statistics</returns>
    Task<ReconciliationReport> GenerateDailyReconciliationReportAsync(DateTime? reportDate = null);

    /// <summary>
    /// Updates registrations that have been pending for more than the specified threshold
    /// </summary>
    /// <param name="staleThresholdHours">Hours after which pending registrations are considered stale (default: 24)</param>
    /// <returns>Number of stale registrations updated</returns>
    Task<int> UpdateStalePendingRegistrationsAsync(int staleThresholdHours = 24);

    /// <summary>
    /// Generates comprehensive daily statistics for monitoring and reporting
    /// </summary>
    /// <returns>Daily statistics including totals, success rates, and branch breakdowns</returns>
    Task<DailyStatistics> GenerateDailyStatisticsAsync();
}

/// <summary>
/// Result model for daily processing operations
/// </summary>
public class DailyProcessingResult
{
    public ReconciliationReport ReconciliationReport { get; set; } = new();
    public int StaleRegistrationsUpdated { get; set; }
    public DailyStatistics Statistics { get; set; } = new();
    public DateTime ProcessingStartTime { get; set; }
    public DateTime ProcessingEndTime { get; set; }
    public TimeSpan Duration => ProcessingEndTime - ProcessingStartTime;
    public bool IsSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Model for daily reconciliation report data
/// </summary>
public class ReconciliationReport
{
    public DateTime ReportDate { get; set; }
    public List<StatusCount> StatusBreakdown { get; set; } = new();
    public int TotalRegistrations { get; set; }
    
    public class StatusCount
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

/// <summary>
/// Model for comprehensive daily statistics
/// </summary>
public class DailyStatistics
{
    public int TotalRegistrations { get; set; }
    public int SuccessfulRegistrations { get; set; }
    public int FailedRegistrations { get; set; }
    public int PendingRegistrations { get; set; }
    public decimal SuccessRate { get; set; }
    public List<BranchStatistic> BranchStats { get; set; } = new();
    
    public class BranchStatistic
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int RegistrationCount { get; set; }
    }
}