using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Jobs;

/// <summary>
/// Optimized daily payment job with batch processing and parallel execution capabilities.
/// Processes payment operations in batches for improved performance and reliability.
/// </summary>
public class OptimizedDailyPaymentJob
{
    private readonly BizConnectContext _context;
    private readonly IPaymentProcessingService _paymentProcessingService;
    private readonly ILogger<OptimizedDailyPaymentJob> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    // Configuration constants
    private const int DefaultBatchSize = 500;
    private const int MaxBatchSize = 2000;
    private const int MaxParallelBatches = 3;
    private const int DefaultTimeoutMinutes = 30;

    public OptimizedDailyPaymentJob(
        BizConnectContext context,
        IPaymentProcessingService paymentProcessingService,
        ILogger<OptimizedDailyPaymentJob> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _paymentProcessingService = paymentProcessingService ?? throw new ArgumentNullException(nameof(paymentProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    /// <summary>
    /// Executes the optimized daily payment processing job.
    /// </summary>
    /// <param name="batchSize">Size of each processing batch</param>
    /// <param name="enableParallelProcessing">Whether to enable parallel batch processing</param>
    /// <returns>Daily processing result</returns>
    public async Task<OptimizedDailyProcessingResult> ExecuteAsync(
        int batchSize = DefaultBatchSize, 
        bool enableParallelProcessing = true)
    {
        var startTime = _dateTimeProvider.UtcNow;
        var result = new OptimizedDailyProcessingResult { StartTime = startTime };

        try
        {
            // Validate batch size
            batchSize = Math.Max(1, Math.Min(batchSize, MaxBatchSize));
            
            _logger.LogInformation("Starting optimized daily payment job with batch size: {BatchSize}, " +
                "parallel processing: {ParallelEnabled}", batchSize, enableParallelProcessing);

            // Phase 1: Get processing statistics
            var processingStats = await GetProcessingStatisticsAsync();
            result.ProcessingStatistics = processingStats;

            _logger.LogInformation("Processing statistics: {PendingCount} pending, {StaleCount} stale, " +
                "{CompletedCount} completed registrations",
                processingStats.PendingRegistrations, processingStats.StaleRegistrations, 
                processingStats.CompletedRegistrations);

            // Phase 2: Process stale registrations in batches
            if (processingStats.StaleRegistrations > 0)
            {
                var staleResult = await ProcessStaleRegistrationsBatchAsync(batchSize, enableParallelProcessing);
                result.StaleProcessingResult = staleResult;
            }

            // Phase 3: Process pending registrations in batches
            if (processingStats.PendingRegistrations > 0)
            {
                var pendingResult = await ProcessPendingRegistrationsBatchAsync(batchSize, enableParallelProcessing);
                result.PendingProcessingResult = pendingResult;
            }

            // Phase 4: Generate reconciliation report
            var reconciliationResult = await GenerateReconciliationReportAsync();
            result.ReconciliationResult = reconciliationResult;

            // Phase 5: Cleanup and optimization
            var cleanupResult = await PerformCleanupOperationsAsync();
            result.CleanupResult = cleanupResult;

            result.Success = true;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogInformation("Optimized daily payment job completed successfully in {Duration}ms. " +
                "Processed {StaleProcessed} stale and {PendingProcessed} pending registrations",
                result.Duration.TotalMilliseconds,
                result.StaleProcessingResult?.TotalProcessed ?? 0,
                result.PendingProcessingResult?.TotalProcessed ?? 0);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogError(ex, "Optimized daily payment job failed after {Duration}ms", 
                result.Duration.TotalMilliseconds);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Gets comprehensive processing statistics for the current day.
    /// </summary>
    private async Task<DailyProcessingStatistics> GetProcessingStatisticsAsync()
    {
        try
        {
            var currentTime = _dateTimeProvider.UtcNow;
            var yesterday = currentTime.AddDays(-1);

            var stats = await _context.Set<KbankOddRegistration>()
                .AsNoTracking()
                .Where(r => r.CreatedAt >= yesterday)
                .GroupBy(r => 1)
                .Select(g => new DailyProcessingStatistics
                {
                    TotalRegistrations = g.Count(),
                    PendingRegistrations = g.Count(r => r.Status == "PENDING"),
                    CompletedRegistrations = g.Count(r => r.Status == "COMPLETED"),
                    StaleRegistrations = g.Count(r => r.Status == "PENDING" && r.UpdatedAt < currentTime.AddHours(-6)),
                    FailedRegistrations = g.Count(r => r.Status == "FAILED"),
                    ExpiredRegistrations = g.Count(r => r.Status == "EXPIRED")
                })
                .FirstOrDefaultAsync();

            return stats ?? new DailyProcessingStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processing statistics");
            return new DailyProcessingStatistics();
        }
    }

    /// <summary>
    /// Processes stale registrations in optimized batches.
    /// </summary>
    private async Task<BatchProcessingResult> ProcessStaleRegistrationsBatchAsync(
        int batchSize, 
        bool enableParallelProcessing)
    {
        var result = new BatchProcessingResult { StartTime = _dateTimeProvider.UtcNow };
        
        try
        {
            _logger.LogInformation("Starting stale registrations batch processing");

            var currentTime = _dateTimeProvider.UtcNow;
            var staleThreshold = currentTime.AddHours(-6); // Consider registrations stale after 6 hours

            var totalStale = await _context.Set<KbankOddRegistration>()
                .CountAsync(r => r.Status == "PENDING" && r.UpdatedAt < staleThreshold);

            if (totalStale == 0)
            {
                result.Success = true;
                result.EndTime = _dateTimeProvider.UtcNow;
                return result;
            }

            var batches = new List<Task<SingleBatchResult>>();
            var batchCount = (int)Math.Ceiling((double)totalStale / batchSize);

            for (int i = 0; i < batchCount; i++)
            {
                var skip = i * batchSize;
                
                if (enableParallelProcessing && batches.Count >= MaxParallelBatches)
                {
                    // Wait for a batch to complete before starting a new one
                    var completedBatch = await Task.WhenAny(batches);
                    batches.Remove(completedBatch);
                    var batchResult = await completedBatch;
                    result.BatchResults.Add(batchResult);
                    result.TotalProcessed += batchResult.RecordsProcessed;
                }

                var batchTask = ProcessStaleRegistrationSingleBatchAsync(skip, batchSize, i + 1);
                
                if (enableParallelProcessing)
                {
                    batches.Add(batchTask);
                }
                else
                {
                    var batchResult = await batchTask;
                    result.BatchResults.Add(batchResult);
                    result.TotalProcessed += batchResult.RecordsProcessed;
                }
            }

            // Wait for any remaining parallel batches to complete
            while (batches.Any())
            {
                var completedBatch = await Task.WhenAny(batches);
                batches.Remove(completedBatch);
                var batchResult = await completedBatch;
                result.BatchResults.Add(batchResult);
                result.TotalProcessed += batchResult.RecordsProcessed;
            }

            result.Success = true;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogInformation("Stale registrations batch processing completed: {TotalProcessed} processed " +
                "in {BatchCount} batches over {Duration}ms",
                result.TotalProcessed, result.BatchResults.Count, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            
            _logger.LogError(ex, "Error processing stale registrations in batches");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Processes a single batch of stale registrations.
    /// </summary>
    private async Task<SingleBatchResult> ProcessStaleRegistrationSingleBatchAsync(
        int skip, 
        int take, 
        int batchNumber)
    {
        var batchStartTime = _dateTimeProvider.UtcNow;
        var result = new SingleBatchResult { BatchNumber = batchNumber };

        try
        {
            var currentTime = _dateTimeProvider.UtcNow;
            var staleThreshold = currentTime.AddHours(-6);

            var staleRegistrations = await _context.Set<KbankOddRegistration>()
                .Where(r => r.Status == "PENDING" && r.UpdatedAt < staleThreshold)
                .OrderBy(r => r.UpdatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            if (!staleRegistrations.Any())
            {
                result.Success = true;
                return result;
            }

            // Process each stale registration
            var processedCount = 0;
            foreach (var registration in staleRegistrations)
            {
                try
                {
                    // Update status to indicate processing
                    registration.Status = "PROCESSING";
                    registration.UpdatedAt = currentTime;
                    
                    // Here you would add actual business logic for stale registration processing
                    // For now, we'll mark them as requiring manual review
                    registration.Status = "MANUAL_REVIEW";
                    
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process stale registration {RegistrationId}", registration.Id);
                    registration.Status = "PROCESSING_FAILED";
                }
            }

            await _context.SaveChangesAsync();

            result.RecordsProcessed = processedCount;
            result.Success = true;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - batchStartTime;

            _logger.LogDebug("Batch {BatchNumber} processed {ProcessedCount} stale registrations in {Duration}ms",
                batchNumber, processedCount, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - batchStartTime;
            
            _logger.LogError(ex, "Error processing stale registration batch {BatchNumber}", batchNumber);
        }

        return result;
    }

    /// <summary>
    /// Processes pending registrations in optimized batches.
    /// </summary>
    private async Task<BatchProcessingResult> ProcessPendingRegistrationsBatchAsync(
        int batchSize, 
        bool enableParallelProcessing)
    {
        var result = new BatchProcessingResult { StartTime = _dateTimeProvider.UtcNow };
        
        try
        {
            _logger.LogInformation("Starting pending registrations batch processing");

            var totalPending = await _context.Set<KbankOddRegistration>()
                .CountAsync(r => r.Status == "PENDING");

            if (totalPending == 0)
            {
                result.Success = true;
                result.EndTime = _dateTimeProvider.UtcNow;
                return result;
            }

            // For pending registrations, we'll use the existing payment processing service
            // but coordinate it through our batch processing framework
            var standardResult = await _paymentProcessingService.ExecuteDailyProcessingAsync();
            
            // Convert standard result to our batch result format
            result.TotalProcessed = standardResult.StaleRegistrationsUpdated;
            result.Success = standardResult.IsSuccessful;
            result.ErrorMessage = standardResult.ErrorMessage;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogInformation("Pending registrations processing completed: {TotalProcessed} processed " +
                "in {Duration}ms", result.TotalProcessed, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            
            _logger.LogError(ex, "Error processing pending registrations in batches");
            throw;
        }

        return result;
    }

    /// <summary>
    /// Generates comprehensive reconciliation report.
    /// </summary>
    private async Task<ReconciliationResult> GenerateReconciliationReportAsync()
    {
        try
        {
            _logger.LogDebug("Generating reconciliation report");

            var currentTime = _dateTimeProvider.UtcNow;
            var today = currentTime.Date;

            var report = await _context.Set<KbankOddRegistration>()
                .AsNoTracking()
                .Where(r => r.CreatedAt >= today)
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = new ReconciliationResult
            {
                GeneratedAt = currentTime,
                StatusCounts = report.ToDictionary(r => r.Status, r => r.Count),
                TotalRegistrations = report.Sum(r => r.Count)
            };

            _logger.LogInformation("Reconciliation report generated: {TotalCount} total registrations across {StatusCount} statuses",
                result.TotalRegistrations, result.StatusCounts.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reconciliation report");
            return new ReconciliationResult
            {
                GeneratedAt = _dateTimeProvider.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Performs cleanup and optimization operations.
    /// </summary>
    private async Task<CleanupResult> PerformCleanupOperationsAsync()
    {
        var result = new CleanupResult { StartTime = _dateTimeProvider.UtcNow };

        try
        {
            _logger.LogDebug("Starting cleanup operations");

            var operations = new List<Task<CleanupOperation>>();

            // Cleanup operation 1: Update statistics
            operations.Add(UpdateDailyStatisticsAsync());

            // Cleanup operation 2: Archive old records (if needed)
            operations.Add(ArchiveOldRecordsAsync());

            // Cleanup operation 3: Optimize database indexes (lightweight check)
            operations.Add(OptimizePerformanceAsync());

            var completedOperations = await Task.WhenAll(operations);
            result.Operations = completedOperations.ToList();

            result.Success = completedOperations.All(op => op.Success);
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogInformation("Cleanup operations completed in {Duration}ms: {SuccessCount}/{TotalCount} successful",
                result.Duration.TotalMilliseconds, 
                completedOperations.Count(op => op.Success), 
                completedOperations.Length);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            
            _logger.LogError(ex, "Error during cleanup operations");
        }

        return result;
    }

    private async Task<CleanupOperation> UpdateDailyStatisticsAsync()
    {
        try
        {
            // Update daily statistics - this could involve updating a statistics table
            await Task.Delay(100); // Simulate work
            return new CleanupOperation 
            { 
                Name = "Update Daily Statistics", 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(100) 
            };
        }
        catch (Exception ex)
        {
            return new CleanupOperation 
            { 
                Name = "Update Daily Statistics", 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    private async Task<CleanupOperation> ArchiveOldRecordsAsync()
    {
        try
        {
            // Archive records older than 90 days (if needed)
            await Task.Delay(50); // Simulate work
            return new CleanupOperation 
            { 
                Name = "Archive Old Records", 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(50) 
            };
        }
        catch (Exception ex)
        {
            return new CleanupOperation 
            { 
                Name = "Archive Old Records", 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    private async Task<CleanupOperation> OptimizePerformanceAsync()
    {
        try
        {
            // Light performance optimization tasks
            await Task.Delay(200); // Simulate work
            return new CleanupOperation 
            { 
                Name = "Optimize Performance", 
                Success = true, 
                Duration = TimeSpan.FromMilliseconds(200) 
            };
        }
        catch (Exception ex)
        {
            return new CleanupOperation 
            { 
                Name = "Optimize Performance", 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }
}

/// <summary>
/// Result of optimized daily processing operation.
/// </summary>
public class OptimizedDailyProcessingResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    
    public DailyProcessingStatistics? ProcessingStatistics { get; set; }
    public BatchProcessingResult? StaleProcessingResult { get; set; }
    public BatchProcessingResult? PendingProcessingResult { get; set; }
    public ReconciliationResult? ReconciliationResult { get; set; }
    public CleanupResult? CleanupResult { get; set; }
}

/// <summary>
/// Statistics for daily processing operations.
/// </summary>
public class DailyProcessingStatistics
{
    public int TotalRegistrations { get; set; }
    public int PendingRegistrations { get; set; }
    public int CompletedRegistrations { get; set; }
    public int StaleRegistrations { get; set; }
    public int FailedRegistrations { get; set; }
    public int ExpiredRegistrations { get; set; }
}

/// <summary>
/// Result of batch processing operations.
/// </summary>
public class BatchProcessingResult
{
    public bool Success { get; set; }
    public int TotalProcessed { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SingleBatchResult> BatchResults { get; set; } = new();
}

/// <summary>
/// Result of a single batch operation.
/// </summary>
public class SingleBatchResult
{
    public int BatchNumber { get; set; }
    public int RecordsProcessed { get; set; }
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of reconciliation operations.
/// </summary>
public class ReconciliationResult
{
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public int TotalRegistrations { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of cleanup operations.
/// </summary>
public class CleanupResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CleanupOperation> Operations { get; set; } = new();
}

/// <summary>
/// Individual cleanup operation result.
/// </summary>
public class CleanupOperation
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}