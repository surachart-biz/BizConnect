using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Jobs;

/// <summary>
/// Optimized background job to purge expired OTAC codes with batch processing capabilities.
/// Uses bulk operations and configurable batch sizes for improved performance.
/// Includes cache invalidation and enhanced error recovery mechanisms.
/// </summary>
public class OptimizedPurgeExpiredOtacCodesJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OptimizedPurgeExpiredOtacCodesJob> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService _cacheService;

    // Configuration constants - enhanced for better performance
    private const int DefaultBatchSize = 100; // Reduced for better performance
    private const int MaxBatchSize = 1000; // Reduced max size
    private const int DefaultTimeoutMinutes = 10;
    private const int MaxRetryAttempts = 3;

    public OptimizedPurgeExpiredOtacCodesJob(
        IUnitOfWork unitOfWork,
        ILogger<OptimizedPurgeExpiredOtacCodesJob> logger,
        IDateTimeProvider dateTimeProvider,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    /// <summary>
    /// Executes the optimized OTAC purge job with batch processing.
    /// </summary>
    /// <param name="batchSize">Number of records to process in each batch (default: 1000)</param>
    /// <param name="maxBatches">Maximum number of batches to process (0 = unlimited)</param>
    /// <returns>Purge operation result</returns>
    public async Task<PurgeResult> ExecuteAsync(int batchSize = DefaultBatchSize, int maxBatches = 0)
    {
        var startTime = _dateTimeProvider.UtcNow;
        var result = new PurgeResult { StartTime = startTime };

        try
        {
            // Validate batch size
            batchSize = Math.Max(1, Math.Min(batchSize, MaxBatchSize));
            
            _logger.LogInformation("Starting optimized expired OTAC purge job with batch size: {BatchSize}, max batches: {MaxBatches}", 
                batchSize, maxBatches == 0 ? "unlimited" : maxBatches.ToString());

            var currentTime = _dateTimeProvider.UtcNow;
            var totalPurged = 0;
            var batchCount = 0;
            var cacheInvalidationCount = 0;

            while (maxBatches == 0 || batchCount < maxBatches)
            {
                batchCount++;
                var batchStartTime = _dateTimeProvider.UtcNow;
                var retryAttempt = 0;
                var batchSuccess = false;
                var batchPurgedCount = 0;

                while (!batchSuccess && retryAttempt < MaxRetryAttempts)
                {
                    try
                    {
                        // Find expired OTAC codes in batches for memory efficiency
                        var expiredRecords = await GetExpiredOtacRecordsAsync(currentTime, batchSize);
                        
                        if (!expiredRecords.Any())
                        {
                            _logger.LogDebug("No more expired OTAC codes found. Batch processing complete.");
                            batchSuccess = true;
                            break;
                        }

                        // Process the batch with cache invalidation
                        batchPurgedCount = await ProcessExpiredOtacBatchAsync(expiredRecords);
                        totalPurged += batchPurgedCount;
                        cacheInvalidationCount += expiredRecords.Count;
                        batchSuccess = true;

                        var batchDuration = _dateTimeProvider.UtcNow - batchStartTime;
                        _logger.LogDebug("Batch {BatchNumber} completed: {BatchCount} records purged in {Duration}ms", 
                            batchCount, batchPurgedCount, batchDuration.TotalMilliseconds);

                        result.BatchResults.Add(new BatchResult
                        {
                            BatchNumber = batchCount,
                            RecordsPurged = batchPurgedCount,
                            Duration = batchDuration,
                            Success = true
                        });

                        // Progressive delay based on batch size to reduce database load
                        var delayMs = Math.Min(batchPurgedCount * 2, 500); // Max 500ms delay
                        if (delayMs > 50)
                        {
                            await Task.Delay(delayMs);
                        }
                    }
                    catch (Exception batchEx)
                    {
                        retryAttempt++;
                        var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)); // Exponential backoff
                        
                        _logger.LogWarning(batchEx, "Batch {BatchNumber} failed on attempt {RetryAttempt}/{MaxRetries}. " +
                            "Retrying in {RetryDelay}s", batchCount, retryAttempt, MaxRetryAttempts, retryDelay.TotalSeconds);

                        if (retryAttempt < MaxRetryAttempts)
                        {
                            await Task.Delay(retryDelay);
                        }
                        else
                        {
                            // Log the failure and add to results
                            _logger.LogError(batchEx, "Batch {BatchNumber} failed after {MaxRetries} attempts", 
                                batchCount, MaxRetryAttempts);
                            
                            result.BatchResults.Add(new BatchResult
                            {
                                BatchNumber = batchCount,
                                RecordsPurged = 0,
                                Duration = _dateTimeProvider.UtcNow - batchStartTime,
                                Success = false,
                                ErrorMessage = batchEx.Message
                            });
                            
                            batchSuccess = true; // Exit retry loop but continue with next batch
                        }
                    }
                }

                // Break if no records were processed and we're not retrying
                if (batchPurgedCount == 0 && batchSuccess)
                {
                    break;
                }
            }

            // Final cache cleanup - remove any remaining OTAC-related cache entries
            await InvalidateOtacCacheAsync();

            result.TotalRecordsPurged = totalPurged;
            result.TotalBatches = batchCount;
            result.CacheInvalidationCount = cacheInvalidationCount;
            result.Success = true;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            if (totalPurged > 0)
            {
                _logger.LogInformation("Optimized purge job completed successfully. " +
                    "Purged {TotalPurged} expired OTAC codes in {BatchCount} batches over {Duration}ms. " +
                    "Cache invalidations: {CacheInvalidations}",
                    totalPurged, batchCount, result.Duration.TotalMilliseconds, cacheInvalidationCount);
            }
            else
            {
                _logger.LogDebug("Optimized purge job completed. No expired OTAC codes found.");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = _dateTimeProvider.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            _logger.LogError(ex, "Error occurred during optimized OTAC purge job execution after {Duration}ms", 
                result.Duration.TotalMilliseconds);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Gets expired OTAC code records in batches for efficient processing with cache key information.
    /// </summary>
    private async Task<List<ExpiredOtacRecord>> GetExpiredOtacRecordsAsync(DateTime currentTime, int batchSize)
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var expiredRecords = await repository.Query()
                .Where(r => r.OtacExpiresAt != null && 
                           r.OtacExpiresAt < currentTime && 
                           (r.OtacState == "Generated" || r.OtacState == "Validated"))
                .Select(r => new ExpiredOtacRecord
                {
                    Id = r.Id,
                    OtacCode = r.OtacCode,
                    CreatedAt = r.CreatedAt
                })
                .Take(batchSize)
                .ToListAsync();

            return expiredRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expired OTAC records");
            throw;
        }
    }

    /// <summary>
    /// Processes a batch of expired OTAC codes with bulk operations and cache invalidation.
    /// </summary>
    private async Task<int> ProcessExpiredOtacBatchAsync(List<ExpiredOtacRecord> expiredRecords)
    {
        if (!expiredRecords.Any())
            return 0;

        try
        {
            var expiredIds = expiredRecords.Select(r => r.Id).ToList();
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            
            // Update each record individually since bulk operations aren't supported in the generic repository
            var updatedCount = 0;
            foreach (var recordId in expiredIds)
            {
                var record = await repository.GetByIdAsync(recordId);
                if (record != null)
                {
                    record.OtacState = "Expired";
                    record.UpdatedAt = _dateTimeProvider.UtcNow;
                    record.IsLocked = true; // Lock the code to prevent further attempts
                    record.StatusMessageTh = "รหัส OTAC หมดอายุแล้ว";
                    record.StatusMessageEn = "OTAC code expired";
                    
                    repository.Update(record);
                    updatedCount++;
                }
            }
            
            // Save all changes in a single transaction
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache entries for the processed OTAC codes
            await InvalidateOtacBatchCacheAsync(expiredRecords);

            _logger.LogDebug("Processed {UpdatedCount} expired OTAC records with cache invalidation", updatedCount);
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired OTAC batch with {Count} records", expiredRecords.Count);
            throw;
        }
    }

    /// <summary>
    /// Gets statistics about OTAC codes that will be purged without actually purging them.
    /// Useful for monitoring and planning purposes.
    /// </summary>
    public async Task<PurgeStatistics> GetPurgeStatisticsAsync()
    {
        try
        {
            var currentTime = _dateTimeProvider.UtcNow;
            var expirationTime = currentTime.AddMinutes(-10);

            // Get expired records first, then calculate statistics in memory to avoid complex EF queries
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            var expiredRecords = await repository.Query()
                .Where(r => r.OtacExpiresAt != null && 
                           r.OtacExpiresAt < currentTime && 
                           (r.OtacState == "Generated" || r.OtacState == "Validated"))
                .Select(r => new { r.CreatedAt })
                .ToListAsync();

            PurgeStatistics? statistics = null;
            if (expiredRecords.Any())
            {
                statistics = new PurgeStatistics
                {
                    TotalExpiredCodes = expiredRecords.Count(),
                    OldestExpiredDate = expiredRecords.Min(r => r.CreatedAt),
                    AverageAgeHours = expiredRecords.Average(r => (currentTime - r.CreatedAt).TotalHours)
                };
            }

            return statistics ?? new PurgeStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purge statistics");
            return new PurgeStatistics();
        }
    }

    /// <summary>
    /// Schedules purge jobs based on system load and expired code volume.
    /// </summary>
    public async Task<PurgeRecommendation> GetPurgeRecommendationAsync()
    {
        try
        {
            var stats = await GetPurgeStatisticsAsync();
            var recommendation = new PurgeRecommendation();

            if (stats.TotalExpiredCodes == 0)
            {
                recommendation.ShouldPurge = false;
                recommendation.RecommendedBatchSize = DefaultBatchSize;
                recommendation.Reason = "No expired OTAC codes found";
            }
            else if (stats.TotalExpiredCodes < 100)
            {
                recommendation.ShouldPurge = true;
                recommendation.RecommendedBatchSize = stats.TotalExpiredCodes;
                recommendation.Reason = "Small number of expired codes, process in single batch";
            }
            else if (stats.TotalExpiredCodes < 1000)
            {
                recommendation.ShouldPurge = true;
                recommendation.RecommendedBatchSize = DefaultBatchSize;
                recommendation.Reason = "Moderate number of expired codes, use default batch size";
            }
            else
            {
                recommendation.ShouldPurge = true;
                recommendation.RecommendedBatchSize = Math.Min(stats.TotalExpiredCodes / 10, MaxBatchSize);
                recommendation.Reason = "Large number of expired codes, use larger batch size";
            }

            recommendation.EstimatedDurationMinutes = (stats.TotalExpiredCodes / (double)recommendation.RecommendedBatchSize) * 0.1; // ~0.1 minutes per batch

            _logger.LogDebug("Purge recommendation: ShouldPurge={ShouldPurge}, BatchSize={BatchSize}, " +
                "EstimatedDuration={Duration}min, Reason={Reason}",
                recommendation.ShouldPurge, recommendation.RecommendedBatchSize, 
                recommendation.EstimatedDurationMinutes, recommendation.Reason);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating purge recommendation");
            return new PurgeRecommendation
            {
                ShouldPurge = false,
                RecommendedBatchSize = DefaultBatchSize,
                Reason = $"Error occurred: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Invalidates cache entries for a batch of expired OTAC records.
    /// </summary>
    private async Task InvalidateOtacBatchCacheAsync(List<ExpiredOtacRecord> expiredRecords)
    {
        try
        {
            var tasks = new List<Task>();

            foreach (var record in expiredRecords)
            {
                // Invalidate cache entries that might contain this OTAC code
                if (!string.IsNullOrEmpty(record.OtacCode))
                {
                    tasks.Add(_cacheService.RemoveAsync($"OTAC:{record.OtacCode}"));
                    tasks.Add(_cacheService.RemoveAsync($"OtacValidation:{record.OtacCode}"));
                }
                
                // Invalidate registration-specific cache entries
                tasks.Add(_cacheService.RemoveAsync($"Registration:{record.Id}"));
                tasks.Add(_cacheService.RemoveAsync($"KbankRegistration:{record.Id}"));
            }

            // Execute all cache invalidations concurrently
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("Invalidated cache entries for {Count} expired OTAC records", expiredRecords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating cache for expired OTAC batch. Continuing with job execution.");
            // Don't throw - cache invalidation failure shouldn't stop the purge job
        }
    }

    /// <summary>
    /// Invalidates all OTAC-related cache entries for general cleanup.
    /// </summary>
    private async Task InvalidateOtacCacheAsync()
    {
        try
        {
            var tasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync("OTAC:*"),
                _cacheService.RemoveByPatternAsync("OtacValidation:*"),
                _cacheService.RemoveByPatternAsync("Registration:*"),
                _cacheService.RemoveByPatternAsync("KbankRegistration:*"),
                _cacheService.RemoveByPatternAsync("OtacStats:*")
            };

            await Task.WhenAll(tasks);
            _logger.LogDebug("Completed general OTAC cache invalidation");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during general OTAC cache invalidation");
            // Don't throw - cache invalidation failure shouldn't stop the job
        }
    }

    /// <summary>
    /// Gets comprehensive job execution metrics for monitoring and optimization.
    /// </summary>
    public async Task<JobExecutionMetrics> GetExecutionMetricsAsync()
    {
        try
        {
            var stats = await GetPurgeStatisticsAsync();
            var cacheStats = _cacheService.GetStatistics();
            
            return new JobExecutionMetrics
            {
                ExpiredCodesAvailable = stats.TotalExpiredCodes,
                CacheHitRatio = cacheStats.HitRatio,
                CacheEntryCount = cacheStats.CurrentEntryCount,
                RecommendedNextRun = DateTime.UtcNow.AddMinutes(5), // Standard 5-minute interval
                SystemLoad = Environment.ProcessorCount > 0 ? (double)stats.TotalExpiredCodes / Environment.ProcessorCount : 0,
                LastExecutionTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job execution metrics");
            return new JobExecutionMetrics
            {
                LastExecutionTime = DateTime.UtcNow,
                SystemLoad = 0
            };
        }
    }
}

/// <summary>
/// Result of a purge operation including performance metrics.
/// </summary>
public class PurgeResult
{
    public bool Success { get; set; }
    public int TotalRecordsPurged { get; set; }
    public int TotalBatches { get; set; }
    public int CacheInvalidationCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public List<BatchResult> BatchResults { get; set; } = new();
    
    public double AverageBatchDurationMs => BatchResults.Any() ? BatchResults.Average(b => b.Duration.TotalMilliseconds) : 0;
    public double RecordsPerSecond => Duration.TotalSeconds > 0 ? TotalRecordsPurged / Duration.TotalSeconds : 0;
    public double SuccessfulBatchRatio => TotalBatches > 0 ? (double)BatchResults.Count(b => b.Success) / TotalBatches : 0;
}

/// <summary>
/// Result of a single batch operation.
/// </summary>
public class BatchResult
{
    public int BatchNumber { get; set; }
    public int RecordsPurged { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Statistics about OTAC codes eligible for purging.
/// </summary>
public class PurgeStatistics
{
    public int TotalExpiredCodes { get; set; }
    public DateTime? OldestExpiredDate { get; set; }
    public double AverageAgeHours { get; set; }
}

/// <summary>
/// Recommendation for purge operation parameters.
/// </summary>
public class PurgeRecommendation
{
    public bool ShouldPurge { get; set; }
    public int RecommendedBatchSize { get; set; }
    public double EstimatedDurationMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Record structure for expired OTAC data with cache invalidation information.
/// </summary>
public class ExpiredOtacRecord
{
    public int Id { get; set; }
    public string? OtacCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Comprehensive job execution metrics for monitoring and optimization.
/// </summary>
public class JobExecutionMetrics
{
    public int ExpiredCodesAvailable { get; set; }
    public double CacheHitRatio { get; set; }
    public int CacheEntryCount { get; set; }
    public DateTime RecommendedNextRun { get; set; }
    public double SystemLoad { get; set; }
    public DateTime LastExecutionTime { get; set; }
}