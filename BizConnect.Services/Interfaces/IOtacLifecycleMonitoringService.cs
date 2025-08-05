using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service for monitoring and analyzing OTAC lifecycle performance and health.
/// Provides comprehensive analytics for business intelligence and system optimization.
/// </summary>
public interface IOtacLifecycleMonitoringService
{
    /// <summary>
    /// Gets comprehensive lifecycle analytics for dashboard reporting.
    /// </summary>
    Task<OtacLifecycleAnalytics> GetLifecycleAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Gets conversion funnel analysis showing user progression through OTAC states.
    /// </summary>
    Task<OtacConversionFunnel> GetConversionFunnelAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Gets daily payment records (Used state) for KBank integration monitoring.
    /// CRITICAL: These records are permanent and required for payment processing.
    /// </summary>
    Task<PaymentReadinessReport> GetPaymentReadinessReportAsync();

    /// <summary>
    /// Gets records eligible for purging with safety validation.
    /// </summary>
    Task<PurgeEligibilityReport> GetPurgeEligibilityReportAsync();

    /// <summary>
    /// Gets state distribution over time for trend analysis.
    /// </summary>
    Task<List<StateDistributionSnapshot>> GetStateDistributionHistoryAsync(int days = 30);

    /// <summary>
    /// Gets performance metrics for OTAC validation process.
    /// </summary>
    Task<ValidationPerformanceMetrics> GetValidationPerformanceAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Gets alert conditions based on business rules and thresholds.
    /// </summary>
    Task<List<OtacAlert>> GetActiveAlertsAsync();

    /// <summary>
    /// Validates system integrity including state consistency checks.
    /// </summary>
    Task<SystemIntegrityReport> ValidateSystemIntegrityAsync();
}

/// <summary>
/// Comprehensive OTAC lifecycle analytics.
/// </summary>
public class OtacLifecycleAnalytics
{
    public int TotalRecords { get; set; }
    public Dictionary<string, int> StateDistribution { get; set; } = new();
    public double OverallConversionRate { get; set; }
    public double ValidationSuccessRate { get; set; }
    public TimeSpan AverageTimeToValidation { get; set; }
    public TimeSpan AverageTimeToUsage { get; set; }
    public int PermanentRecords { get; set; }
    public int PurgeableRecords { get; set; }
    public DateTime? OldestActiveRecord { get; set; }
    public List<DailyStats> DailyTrends { get; set; } = new();
}

/// <summary>
/// Conversion funnel showing user progression through OTAC states.
/// </summary>
public class OtacConversionFunnel
{
    public int GeneratedCount { get; set; }
    public int ValidatedCount { get; set; }
    public int UsedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int InvalidatedCount { get; set; }
    
    public double GeneratedToValidatedRate => GeneratedCount > 0 ? (double)ValidatedCount / GeneratedCount * 100 : 0;
    public double ValidatedToUsedRate => ValidatedCount > 0 ? (double)UsedCount / ValidatedCount * 100 : 0;
    public double OverallSuccessRate => GeneratedCount > 0 ? (double)UsedCount / GeneratedCount * 100 : 0;
    public double FailureRate => GeneratedCount > 0 ? (double)(ExpiredCount + InvalidatedCount) / GeneratedCount * 100 : 0;
}

/// <summary>
/// Report on payment processing readiness.
/// </summary>
public class PaymentReadinessReport
{
    public int TotalPaymentRecords { get; set; }
    public DateTime? OldestPaymentRecord { get; set; }
    public DateTime? NewestPaymentRecord { get; set; }
    public List<BranchPaymentStats> PaymentsByBranch { get; set; } = new();
    public double PaymentDataIntegrity { get; set; }
    public List<string> HealthWarnings { get; set; } = new();
}

/// <summary>
/// Branch-specific payment statistics.
/// </summary>
public class BranchPaymentStats
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int PaymentRecordCount { get; set; }
    public DateTime? LastPaymentRecord { get; set; }
}

/// <summary>
/// Report on records eligible for purging.
/// </summary>
public class PurgeEligibilityReport
{
    public int ExpiredRecordsCount { get; set; }
    public int InvalidatedRecordsCount { get; set; }
    public int TotalPurgeableRecords { get; set; }
    public int ProtectedUsedRecords { get; set; }
    public DateTime? OldestPurgeableRecord { get; set; }
    public long EstimatedStorageSavingsKB { get; set; }
    public List<string> SafetyChecks { get; set; } = new();
}

/// <summary>
/// State distribution at a point in time.
/// </summary>
public class StateDistributionSnapshot
{
    public DateTime SnapshotDate { get; set; }
    public Dictionary<string, int> StateDistribution { get; set; } = new();
    public int TotalRecords { get; set; }
}

/// <summary>
/// Daily statistics for trending.
/// </summary>
public class DailyStats
{
    public DateTime Date { get; set; }
    public int Generated { get; set; }
    public int Validated { get; set; }
    public int Used { get; set; }
    public int Expired { get; set; }
    public int Invalidated { get; set; }
    public double ConversionRate { get; set; }
}

/// <summary>
/// OTAC validation performance metrics.
/// </summary>
public class ValidationPerformanceMetrics
{
    public int TotalValidationAttempts { get; set; }
    public int SuccessfulValidations { get; set; }
    public int FailedValidations { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageValidationTime { get; set; }
    public int LockedAccounts { get; set; }
    public Dictionary<int, int> AttemptsDistribution { get; set; } = new(); // AttemptsCount -> RecordCount
}

/// <summary>
/// System alert for monitoring.
/// </summary>
public class OtacAlert
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// System integrity validation report.
/// </summary>
public class SystemIntegrityReport
{
    public bool IsHealthy { get; set; }
    public List<IntegrityCheck> Checks { get; set; } = new();
    public int InvalidStateRecords { get; set; }
    public int OrphanedRecords { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// Individual integrity check result.
/// </summary>
public class IntegrityCheck
{
    public string CheckName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Details { get; set; } = string.Empty;
    public int AffectedRecords { get; set; }
}