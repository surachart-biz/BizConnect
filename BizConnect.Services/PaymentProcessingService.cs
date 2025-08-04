using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service implementation for payment processing and reconciliation operations
/// </summary>
public class PaymentProcessingService : IPaymentProcessingService
{
    private readonly BizConnectContext _context;
    private readonly ILogger<PaymentProcessingService> _logger;

    public PaymentProcessingService(
        BizConnectContext context,
        ILogger<PaymentProcessingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Executes daily payment processing tasks including reconciliation and cleanup
    /// </summary>
    /// <returns>Processing results with statistics</returns>
    public async Task<DailyProcessingResult> ExecuteDailyProcessingAsync()
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting daily payment processing at {StartTime}", startTime);

        var result = new DailyProcessingResult
        {
            ProcessingStartTime = startTime
        };

        try
        {
            // Task 1: Generate daily reconciliation report
            result.ReconciliationReport = await GenerateDailyReconciliationReportAsync();

            // Task 2: Update stale pending registrations
            result.StaleRegistrationsUpdated = await UpdateStalePendingRegistrationsAsync();

            // Task 3: Generate daily statistics
            result.Statistics = await GenerateDailyStatisticsAsync();

            result.ProcessingEndTime = DateTime.UtcNow;
            result.IsSuccessful = true;

            _logger.LogInformation("Daily payment processing completed successfully in {Duration}ms", 
                result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            result.ProcessingEndTime = DateTime.UtcNow;
            result.IsSuccessful = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Daily payment processing failed after {Duration}ms", 
                result.Duration.TotalMilliseconds);
            
            throw;
        }
    }

    /// <summary>
    /// Generates a daily reconciliation report for KBank ODD registrations
    /// </summary>
    /// <param name="reportDate">Date for the report (defaults to yesterday)</param>
    /// <returns>Reconciliation report with statistics</returns>
    public async Task<ReconciliationReport> GenerateDailyReconciliationReportAsync(DateTime? reportDate = null)
    {
        var targetDate = reportDate ?? DateTime.UtcNow.Date.AddDays(-1);
        var nextDay = targetDate.AddDays(1);

        _logger.LogInformation("Generating daily reconciliation report for {Date}", targetDate.ToString("yyyy-MM-dd"));

        try
        {
            var dailyStats = await _context.KbankOddRegistrations
                .Where(r => r.CreatedAt >= targetDate && r.CreatedAt < nextDay)
                .GroupBy(r => r.Status)
                .Select(g => new ReconciliationReport.StatusCount 
                { 
                    Status = g.Key ?? "Unknown", 
                    Count = g.Count() 
                })
                .ToListAsync();

            var report = new ReconciliationReport
            {
                ReportDate = targetDate,
                StatusBreakdown = dailyStats,
                TotalRegistrations = dailyStats.Sum(s => s.Count)
            };

            foreach (var stat in dailyStats)
            {
                _logger.LogInformation("Daily report - Date: {Date}, Status: {Status}, Count: {Count}", 
                    targetDate.ToString("yyyy-MM-dd"), stat.Status, stat.Count);
            }

            _logger.LogInformation("Daily reconciliation report completed. Total registrations for {Date}: {Total}",
                targetDate.ToString("yyyy-MM-dd"), report.TotalRegistrations);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily reconciliation report for {Date}", 
                targetDate.ToString("yyyy-MM-dd"));
            throw;
        }
    }

    /// <summary>
    /// Updates registrations that have been pending for more than the specified threshold
    /// </summary>
    /// <param name="staleThresholdHours">Hours after which pending registrations are considered stale (default: 24)</param>
    /// <returns>Number of stale registrations updated</returns>
    public async Task<int> UpdateStalePendingRegistrationsAsync(int staleThresholdHours = 24)
    {
        _logger.LogInformation("Checking for stale pending registrations (threshold: {Hours} hours)", staleThresholdHours);

        try
        {
            var staleCutoff = DateTime.UtcNow.AddHours(-staleThresholdHours);

            var staleRegistrations = await _context.KbankOddRegistrations
                .Where(r => r.Status == "Pending" && r.CreatedAt <= staleCutoff)
                .ToListAsync();

            if (staleRegistrations.Count == 0)
            {
                _logger.LogInformation("No stale pending registrations found");
                return 0;
            }

            _logger.LogWarning("Found {Count} stale pending registrations (pending for more than {Hours} hours)", 
                staleRegistrations.Count, staleThresholdHours);

            foreach (var registration in staleRegistrations)
            {
                // Mark as failed if pending for more than threshold
                registration.Status = "Fail";
                registration.ReturnCode = "TIMEOUT";
                registration.UpdatedAt = DateTime.UtcNow;

                _logger.LogWarning("Marking stale registration {ExternalReference} as failed (pending since {CreatedAt})",
                    registration.ExternalReference, registration.CreatedAt);
            }

            var updatedCount = await _context.SaveChangesAsync();
            _logger.LogInformation("Updated {Count} stale pending registrations to failed status", updatedCount);

            return staleRegistrations.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stale pending registrations");
            throw;
        }
    }

    /// <summary>
    /// Generates comprehensive daily statistics for monitoring and reporting
    /// </summary>
    /// <returns>Daily statistics including totals, success rates, and branch breakdowns</returns>
    public async Task<DailyStatistics> GenerateDailyStatisticsAsync()
    {
        _logger.LogInformation("Generating daily statistics");

        try
        {
            // Overall statistics
            var totalRegistrations = await _context.KbankOddRegistrations.CountAsync();
            var successfulRegistrations = await _context.KbankOddRegistrations
                .CountAsync(r => r.Status == "Success");
            var failedRegistrations = await _context.KbankOddRegistrations
                .CountAsync(r => r.Status == "Fail");
            var pendingRegistrations = await _context.KbankOddRegistrations
                .CountAsync(r => r.Status == "Pending");

            // Calculate success rate
            var successRate = totalRegistrations > 0 
                ? (decimal)successfulRegistrations / totalRegistrations * 100 
                : 0;

            _logger.LogInformation("Daily statistics - Total: {Total}, Success: {Success}, Failed: {Failed}, Pending: {Pending}",
                totalRegistrations, successfulRegistrations, failedRegistrations, pendingRegistrations);

            _logger.LogInformation("Overall success rate: {SuccessRate:F2}%", successRate);

            // Branch statistics (if available)
            var branchStats = await _context.KbankOddRegistrations
                .Where(r => r.BranchId.HasValue)
                .Include(r => r.Branch) // Include branch details for name
                .GroupBy(r => new { r.BranchId, r.Branch!.Name })
                .Select(g => new DailyStatistics.BranchStatistic 
                { 
                    BranchId = g.Key.BranchId!.Value,
                    BranchName = g.Key.Name,
                    RegistrationCount = g.Count() 
                })
                .ToListAsync();

            foreach (var branchStat in branchStats)
            {
                _logger.LogInformation("Branch {BranchName} (ID: {BranchId}) registrations: {Count}", 
                    branchStat.BranchName, branchStat.BranchId, branchStat.RegistrationCount);
            }

            var statistics = new DailyStatistics
            {
                TotalRegistrations = totalRegistrations,
                SuccessfulRegistrations = successfulRegistrations,
                FailedRegistrations = failedRegistrations,
                PendingRegistrations = pendingRegistrations,
                SuccessRate = successRate,
                BranchStats = branchStats
            };

            _logger.LogInformation("Daily statistics generation completed");
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily statistics");
            throw;
        }
    }
}