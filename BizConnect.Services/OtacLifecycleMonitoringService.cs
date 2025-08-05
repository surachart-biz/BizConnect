using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service for monitoring and analyzing OTAC lifecycle performance and health.
/// Provides comprehensive analytics for business intelligence and system optimization.
/// </summary>
public class OtacLifecycleMonitoringService : IOtacLifecycleMonitoringService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OtacLifecycleMonitoringService> _logger;
    private readonly IOtacStateService _otacStateService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OtacLifecycleMonitoringService(
        IUnitOfWork unitOfWork,
        ILogger<OtacLifecycleMonitoringService> logger,
        IOtacStateService otacStateService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _otacStateService = otacStateService ?? throw new ArgumentNullException(nameof(otacStateService));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    /// <summary>
    /// Gets comprehensive lifecycle analytics for dashboard reporting.
    /// </summary>
    public async Task<OtacLifecycleAnalytics> GetLifecycleAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var currentTime = _dateTimeProvider.UtcNow;
            
            fromDate ??= currentTime.AddDays(-30);
            toDate ??= currentTime;

            var query = repository.Query()
                .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate);

            var records = await query.ToListAsync();
            
            var analytics = new OtacLifecycleAnalytics
            {
                TotalRecords = records.Count,
                StateDistribution = records.GroupBy(r => r.OtacState)
                    .ToDictionary(g => g.Key, g => g.Count()),
                PermanentRecords = records.Count(r => r.OtacState == "Used"),
                PurgeableRecords = records.Count(r => r.OtacState == "Expired" || r.OtacState == "Invalidated"),
                OldestActiveRecord = records.Where(r => r.OtacState == "Generated" || r.OtacState == "Validated")
                    .OrderBy(r => r.CreatedAt)
                    .FirstOrDefault()?.CreatedAt
            };

            // Calculate conversion rates
            var generatedCount = records.Count(r => r.OtacState == "Generated");
            var validatedCount = records.Count(r => r.OtacState == "Validated");
            var usedCount = records.Count(r => r.OtacState == "Used");

            if (generatedCount > 0)
            {
                analytics.ValidationSuccessRate = (double)validatedCount / generatedCount * 100;
                analytics.OverallConversionRate = (double)usedCount / generatedCount * 100;
            }

            // Calculate timing metrics (simplified for performance)
            var usedRecords = records.Where(r => r.OtacState == "Used" && r.UpdatedAt.HasValue).ToList();
            if (usedRecords.Any())
            {
                analytics.AverageTimeToUsage = TimeSpan.FromTicks(
                    (long)usedRecords.Average(r => (r.UpdatedAt!.Value - r.CreatedAt).Ticks));
            }

            // Daily trends (last 7 days for performance)
            analytics.DailyTrends = await GetDailyTrendsAsync(currentTime.AddDays(-7), currentTime);

            _logger.LogDebug("Generated lifecycle analytics: {TotalRecords} records, {ConversionRate:F2}% conversion rate", 
                analytics.TotalRecords, analytics.OverallConversionRate);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating lifecycle analytics");
            return new OtacLifecycleAnalytics();
        }
    }

    /// <summary>
    /// Gets conversion funnel analysis showing user progression through OTAC states.
    /// </summary>
    public async Task<OtacConversionFunnel> GetConversionFunnelAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var currentTime = _dateTimeProvider.UtcNow;
            
            fromDate ??= currentTime.AddDays(-30);
            toDate ??= currentTime;

            var stateStats = await repository.Query()
                .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
                .GroupBy(r => r.OtacState)
                .Select(g => new { State = g.Key, Count = g.Count() })
                .ToListAsync();

            var funnel = new OtacConversionFunnel();
            
            foreach (var stat in stateStats)
            {
                switch (stat.State)
                {
                    case "Generated":
                        funnel.GeneratedCount = stat.Count;
                        break;
                    case "Validated":
                        funnel.ValidatedCount = stat.Count;
                        break;
                    case "Used":
                        funnel.UsedCount = stat.Count;
                        break;
                    case "Expired":
                        funnel.ExpiredCount = stat.Count;
                        break;
                    case "Invalidated":
                        funnel.InvalidatedCount = stat.Count;
                        break;
                }
            }

            _logger.LogDebug("Conversion funnel: {Generated} → {Validated} → {Used} (Success: {SuccessRate:F2}%)", 
                funnel.GeneratedCount, funnel.ValidatedCount, funnel.UsedCount, funnel.OverallSuccessRate);

            return funnel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating conversion funnel");
            return new OtacConversionFunnel();
        }
    }

    /// <summary>
    /// Gets daily payment records (Used state) for KBank integration monitoring.
    /// CRITICAL: These records are permanent and required for payment processing.
    /// </summary>
    public async Task<PaymentReadinessReport> GetPaymentReadinessReportAsync()
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var branchRepository = _unitOfWork.GetRepository<Branch>();
            
            var paymentRecords = await repository.Query()
                .Where(r => r.OtacState == "Used")
                .Include(r => r.Branch)
                .ToListAsync();

            var report = new PaymentReadinessReport
            {
                TotalPaymentRecords = paymentRecords.Count,
                OldestPaymentRecord = paymentRecords.OrderBy(r => r.CreatedAt).FirstOrDefault()?.CreatedAt,
                NewestPaymentRecord = paymentRecords.OrderByDescending(r => r.CreatedAt).FirstOrDefault()?.CreatedAt
            };

            // Branch-specific statistics
            report.PaymentsByBranch = paymentRecords
                .Where(r => r.Branch != null)
                .GroupBy(r => new { r.BranchId, r.Branch!.Name })
                .Select(g => new BranchPaymentStats
                {
                    BranchId = g.Key.BranchId ?? 0,
                    BranchName = g.Key.Name,
                    PaymentRecordCount = g.Count(),
                    LastPaymentRecord = g.OrderByDescending(r => r.CreatedAt).First().CreatedAt
                })
                .OrderByDescending(b => b.PaymentRecordCount)
                .ToList();

            // Data integrity checks
            var integrityIssues = paymentRecords.Count(r => 
                string.IsNullOrEmpty(r.AccountNo) || 
                string.IsNullOrEmpty(r.ExternalReference) ||
                r.BranchId == null);

            report.PaymentDataIntegrity = paymentRecords.Count > 0 
                ? (double)(paymentRecords.Count - integrityIssues) / paymentRecords.Count * 100 
                : 100;

            if (integrityIssues > 0)
            {
                report.HealthWarnings.Add($"{integrityIssues} payment records have missing required data");
            }

            if (report.TotalPaymentRecords == 0)
            {
                report.HealthWarnings.Add("No active payment records found - this may impact daily payment processing");
            }

            _logger.LogInformation("Payment readiness: {TotalRecords} active payment records, {Integrity:F1}% data integrity", 
                report.TotalPaymentRecords, report.PaymentDataIntegrity);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating payment readiness report");
            return new PaymentReadinessReport();
        }
    }

    /// <summary>
    /// Gets records eligible for purging with safety validation.
    /// </summary>
    public async Task<PurgeEligibilityReport> GetPurgeEligibilityReportAsync()
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            
            var expiredRecords = await repository.Query()
                .Where(r => r.OtacState == "Expired")
                .ToListAsync();
                
            var invalidatedRecords = await repository.Query()
                .Where(r => r.OtacState == "Invalidated")
                .ToListAsync();
                
            var protectedRecords = await repository.Query()
                .Where(r => r.OtacState == "Used")
                .CountAsync();

            var report = new PurgeEligibilityReport
            {
                ExpiredRecordsCount = expiredRecords.Count,
                InvalidatedRecordsCount = invalidatedRecords.Count,
                TotalPurgeableRecords = expiredRecords.Count + invalidatedRecords.Count,
                ProtectedUsedRecords = protectedRecords,
                OldestPurgeableRecord = expiredRecords.Concat(invalidatedRecords)
                    .OrderBy(r => r.CreatedAt)
                    .FirstOrDefault()?.CreatedAt,
                EstimatedStorageSavingsKB = (expiredRecords.Count + invalidatedRecords.Count) * 2 // Rough estimate
            };

            // Safety checks
            report.SafetyChecks.Add($"✓ {protectedRecords} Used state records are protected from purging");
            report.SafetyChecks.Add($"✓ Only Expired and Invalidated states are eligible for purging");
            
            if (report.TotalPurgeableRecords == 0)
            {
                report.SafetyChecks.Add("ℹ No records currently eligible for purging");
            }

            _logger.LogDebug("Purge eligibility: {Purgeable} eligible, {Protected} protected (Used state)", 
                report.TotalPurgeableRecords, report.ProtectedUsedRecords);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating purge eligibility report");
            return new PurgeEligibilityReport();
        }
    }

    /// <summary>
    /// Gets state distribution over time for trend analysis.
    /// </summary>
    public async Task<List<StateDistributionSnapshot>> GetStateDistributionHistoryAsync(int days = 30)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var currentTime = _dateTimeProvider.UtcNow;
            var snapshots = new List<StateDistributionSnapshot>();

            // Generate snapshots for each day (simplified - in production you might store these)
            for (int i = 0; i < days; i++)
            {
                var snapshotDate = currentTime.AddDays(-i).Date;
                var endDate = snapshotDate.AddDays(1);

                var stateStats = await repository.Query()
                    .Where(r => r.CreatedAt < endDate) // All records up to this date
                    .GroupBy(r => r.OtacState)
                    .Select(g => new { State = g.Key, Count = g.Count() })
                    .ToListAsync();

                var snapshot = new StateDistributionSnapshot
                {
                    SnapshotDate = snapshotDate,
                    StateDistribution = stateStats.ToDictionary(s => s.State, s => s.Count),
                    TotalRecords = stateStats.Sum(s => s.Count)
                };

                snapshots.Add(snapshot);
            }

            return snapshots.OrderBy(s => s.SnapshotDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating state distribution history");
            return new List<StateDistributionSnapshot>();
        }
    }

    /// <summary>
    /// Gets performance metrics for OTAC validation process.
    /// </summary>
    public async Task<ValidationPerformanceMetrics> GetValidationPerformanceAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var currentTime = _dateTimeProvider.UtcNow;
            
            fromDate ??= currentTime.AddDays(-7);
            toDate ??= currentTime;

            var records = await repository.Query()
                .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
                .ToListAsync();

            var metrics = new ValidationPerformanceMetrics
            {
                TotalValidationAttempts = records.Sum(r => r.AttemptCount),
                SuccessfulValidations = records.Count(r => r.OtacState == "Validated" || r.OtacState == "Used"),
                FailedValidations = records.Count(r => r.OtacState == "Expired" || r.OtacState == "Invalidated"),
                LockedAccounts = records.Count(r => r.IsLocked),
                AttemptsDistribution = records.GroupBy(r => r.AttemptCount)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            var totalValidations = metrics.SuccessfulValidations + metrics.FailedValidations;
            if (totalValidations > 0)
            {
                metrics.SuccessRate = (double)metrics.SuccessfulValidations / totalValidations * 100;
            }

            // Calculate average validation time (simplified)
            var validatedRecords = records.Where(r => r.LastAttemptAt.HasValue).ToList();
            if (validatedRecords.Any())
            {
                metrics.AverageValidationTime = TimeSpan.FromTicks(
                    (long)validatedRecords.Average(r => (r.LastAttemptAt!.Value - r.CreatedAt).Ticks));
            }

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating validation performance metrics");
            return new ValidationPerformanceMetrics();
        }
    }

    /// <summary>
    /// Gets alert conditions based on business rules and thresholds.
    /// </summary>
    public async Task<List<OtacAlert>> GetActiveAlertsAsync()
    {
        var alerts = new List<OtacAlert>();
        var currentTime = _dateTimeProvider.UtcNow;

        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            
            // Alert 1: High failure rate
            var recentRecords = await repository.Query()
                .Where(r => r.CreatedAt >= currentTime.AddHours(-24))
                .ToListAsync();

            if (recentRecords.Count > 10) // Only alert if we have meaningful data
            {
                var failureRate = recentRecords.Count(r => r.OtacState == "Expired" || r.OtacState == "Invalidated") 
                    / (double)recentRecords.Count * 100;

                if (failureRate > 50) // More than 50% failure rate
                {
                    alerts.Add(new OtacAlert
                    {
                        AlertType = "HighFailureRate",
                        Severity = "Warning",
                        Message = $"High OTAC failure rate: {failureRate:F1}% in last 24 hours",
                        DetectedAt = currentTime,
                        Metadata = new Dictionary<string, object> { ["FailureRate"] = failureRate }
                    });
                }
            }

            // Alert 2: Old active records
            var oldActiveRecords = await repository.Query()
                .Where(r => (r.OtacState == "Generated" || r.OtacState == "Validated") && 
                           r.CreatedAt < currentTime.AddHours(-2))
                .CountAsync();

            if (oldActiveRecords > 100)
            {
                alerts.Add(new OtacAlert
                {
                    AlertType = "StaleActiveRecords",
                    Severity = "Info",
                    Message = $"{oldActiveRecords} OTAC codes are active but older than 2 hours",
                    DetectedAt = currentTime,
                    Metadata = new Dictionary<string, object> { ["StaleRecordCount"] = oldActiveRecords }
                });
            }

            // Alert 3: No recent Used records (payment processing concern)
            var recentUsedRecords = await repository.Query()
                .Where(r => r.OtacState == "Used" && r.CreatedAt >= currentTime.AddHours(-24))
                .CountAsync();

            if (recentUsedRecords == 0)
            {
                alerts.Add(new OtacAlert
                {
                    AlertType = "NoRecentPaymentRecords",
                    Severity = "Critical",
                    Message = "No Used state records created in last 24 hours - may impact payment processing",
                    DetectedAt = currentTime,
                    Metadata = new Dictionary<string, object> { ["RecentUsedCount"] = recentUsedRecords }
                });
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating alerts");
            alerts.Add(new OtacAlert
            {
                AlertType = "SystemError",
                Severity = "Error",
                Message = $"Error generating alerts: {ex.Message}",
                DetectedAt = currentTime
            });
        }

        return alerts;
    }

    /// <summary>
    /// Validates system integrity including state consistency checks.
    /// </summary>
    public async Task<SystemIntegrityReport> ValidateSystemIntegrityAsync()
    {
        var report = new SystemIntegrityReport
        {
            LastChecked = _dateTimeProvider.UtcNow,
            IsHealthy = true
        };

        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            
            // Check 1: Invalid states
            var invalidStateRecords = await repository.Query()
                .Where(r => !_otacStateService.GetAllValidStates().Contains(r.OtacState))
                .CountAsync();

            report.Checks.Add(new IntegrityCheck
            {
                CheckName = "ValidStates",
                Passed = invalidStateRecords == 0,
                Details = invalidStateRecords == 0 ? "All records have valid states" : $"{invalidStateRecords} records have invalid states",
                AffectedRecords = invalidStateRecords
            });

            report.InvalidStateRecords = invalidStateRecords;
            if (invalidStateRecords > 0) report.IsHealthy = false;

            // Check 2: Orphaned records
            var orphanedRecords = await repository.Query()
                .Where(r => r.BranchId != null && r.Branch == null)
                .CountAsync();

            report.Checks.Add(new IntegrityCheck
            {
                CheckName = "ForeignKeyIntegrity",
                Passed = orphanedRecords == 0,
                Details = orphanedRecords == 0 ? "No orphaned records found" : $"{orphanedRecords} records reference missing branches",
                AffectedRecords = orphanedRecords
            });

            report.OrphanedRecords = orphanedRecords;
            if (orphanedRecords > 0) report.IsHealthy = false;

            // Check 3: Used state protection
            var usedRecordCount = await repository.Query()
                .Where(r => r.OtacState == "Used")
                .CountAsync();

            report.Checks.Add(new IntegrityCheck
            {
                CheckName = "UsedStateProtection",
                Passed = true, // This is informational
                Details = $"{usedRecordCount} Used state records are protected (permanent for payments)",
                AffectedRecords = usedRecordCount
            });

            // Generate recommendations
            if (invalidStateRecords > 0)
            {
                report.Recommendations.Add("Run database migration to fix invalid state records");
            }
            if (orphanedRecords > 0)
            {
                report.Recommendations.Add("Clean up orphaned records referencing missing branches");
            }
            if (report.IsHealthy)
            {
                report.Recommendations.Add("System integrity is healthy - no action required");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating system integrity");
            report.IsHealthy = false;
            report.Checks.Add(new IntegrityCheck
            {
                CheckName = "SystemError",
                Passed = false,
                Details = $"Error during integrity check: {ex.Message}",
                AffectedRecords = 0
            });
        }

        return report;
    }

    /// <summary>
    /// Helper method to get daily trends.
    /// </summary>
    private async Task<List<DailyStats>> GetDailyTrendsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var trends = new List<DailyStats>();

            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                var nextDate = date.AddDays(1);
                var dayRecords = await repository.Query()
                    .Where(r => r.CreatedAt >= date && r.CreatedAt < nextDate)
                    .ToListAsync();

                var stats = new DailyStats
                {
                    Date = date,
                    Generated = dayRecords.Count(r => r.OtacState == "Generated"),
                    Validated = dayRecords.Count(r => r.OtacState == "Validated"),
                    Used = dayRecords.Count(r => r.OtacState == "Used"),
                    Expired = dayRecords.Count(r => r.OtacState == "Expired"),
                    Invalidated = dayRecords.Count(r => r.OtacState == "Invalidated")
                };

                var totalGenerated = stats.Generated;
                if (totalGenerated > 0)
                {
                    stats.ConversionRate = (double)stats.Used / totalGenerated * 100;
                }

                trends.Add(stats);
            }

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily trends");
            return new List<DailyStats>();
        }
    }
}