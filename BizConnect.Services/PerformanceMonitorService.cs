using BizConnect.Services.DTOs;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BizConnect.Services;

/// <summary>
/// Service for performance monitoring and metrics collection
/// </summary>
public class PerformanceMonitorService : IPerformanceMonitorService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<PerformanceMonitorService> _logger;
    
    // In-memory storage for recent performance data
    private readonly ConcurrentQueue<PerformanceSnapshot> _performanceHistory;
    private readonly ConcurrentDictionary<string, List<PerformanceMetric>> _endpointMetrics;
    
    private const string PERFORMANCE_CACHE_KEY = "performance:current_metrics";
    private const string PERFORMANCE_HISTORY_CACHE_KEY = "performance:history";
    private const int MAX_HISTORY_SIZE = 1000;
    
    public PerformanceMonitorService(
        ICacheService cacheService,
        ILogger<PerformanceMonitorService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
        _performanceHistory = new ConcurrentQueue<PerformanceSnapshot>();
        _endpointMetrics = new ConcurrentDictionary<string, List<PerformanceMetric>>();
    }

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    /// <returns>Current performance metrics</returns>
    public async Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        try
        {
            // Check cache first
            var cachedMetrics = await _cacheService.GetAsync<PerformanceMetrics>(PERFORMANCE_CACHE_KEY);
            if (cachedMetrics != null)
            {
                return cachedMetrics;
            }

            _logger.LogDebug("Calculating current performance metrics");

            var currentTime = DateTime.UtcNow;
            var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(5));

            var currentMetrics = new PerformanceMetrics
            {
                Timestamp = currentTime,
                ActiveConnections = GetCurrentActiveConnections(),
                CpuUsagePercent = GetCurrentCpuUsage(),
                MemoryUsagePercent = GetCurrentMemoryUsage(),
                RequestsPerMinute = CalculateRequestsPerMinute(recentMetrics),
                ErrorsPerMinute = CalculateErrorsPerMinute(recentMetrics),
                AverageResponseTimeMs = CalculateAverageResponseTime(recentMetrics),
                PeakResponseTimeMs = CalculatePeakResponseTime(recentMetrics),
                DatabaseConnections = GetCurrentDatabaseConnections(),
                IsHealthy = DetermineHealthStatus(recentMetrics),
                TotalRequests = recentMetrics.Count,
                SuccessfulRequests = recentMetrics.Count(m => m.IsSuccess)
            };

            // Cache for 30 seconds
            await _cacheService.SetAsync(currentMetrics, PERFORMANCE_CACHE_KEY, TimeSpan.FromSeconds(30));

            return currentMetrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating current performance metrics");
            
            return new PerformanceMetrics
            {
                Timestamp = DateTime.UtcNow,
                IsHealthy = false,
                AverageResponseTimeMs = -1
            };
        }
    }

    /// <summary>
    /// Get average response time for dashboard display
    /// </summary>
    /// <returns>Average response time in milliseconds</returns>
    public async Task<int> GetAvgResponseTimeAsync()
    {
        try
        {
            var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(15));
            if (!recentMetrics.Any())
            {
                return 0;
            }

            var averageMs = recentMetrics.Average(m => m.ResponseTimeMs);
            return (int)Math.Round(averageMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average response time");
            return 0;
        }
    }

    /// <summary>
    /// Record performance metric for tracking
    /// </summary>
    /// <param name="endpoint">Endpoint name</param>
    /// <param name="responseTimeMs">Response time in milliseconds</param>
    /// <param name="isSuccess">Whether the request was successful</param>
    public async Task RecordMetricAsync(string endpoint, int responseTimeMs, bool isSuccess)
    {
        try
        {
            var metric = new PerformanceMetric
            {
                Endpoint = endpoint,
                ResponseTimeMs = responseTimeMs,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow,
                SessionId = GetCurrentSessionId()
            };

            // Add to in-memory storage
            RecordEndpointMetric(endpoint, metric);
            
            // Create performance snapshot
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = metric.Timestamp,
                ResponseTimeMs = responseTimeMs,
                AvgResponseTimeMs = responseTimeMs, // Single metric, so average is the same
                RequestCount = 1,
                ErrorCount = isSuccess ? 0 : 1,
                CpuUsage = GetCurrentCpuUsage(),
                MemoryUsage = GetCurrentMemoryUsage(),
                TotalRequests = 1,
                SuccessfulRequests = isSuccess ? 1 : 0
            };

            AddToPerformanceHistory(snapshot);

            _logger.LogDebug("Recorded performance metric for endpoint {Endpoint}: {ResponseTimeMs}ms, Success: {IsSuccess}", 
                endpoint, responseTimeMs, isSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording performance metric for endpoint {Endpoint}", endpoint);
        }
    }

    /// <summary>
    /// Get performance history for analysis
    /// </summary>
    /// <param name="timeRange">Time range for performance data</param>
    /// <returns>Performance history data</returns>
    public async Task<PerformanceHistory> GetPerformanceHistoryAsync(TimeSpan timeRange)
    {
        try
        {
            var cacheKey = $"{PERFORMANCE_HISTORY_CACHE_KEY}:{timeRange.TotalMinutes}m";
            var cachedHistory = await _cacheService.GetAsync<PerformanceHistory>(cacheKey);
            if (cachedHistory != null)
            {
                return cachedHistory;
            }

            _logger.LogDebug("Generating performance history for time range {TimeRange}", timeRange);

            var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
            var snapshots = _performanceHistory
                .Where(s => s.Timestamp >= cutoffTime)
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (!snapshots.Any())
            {
                return new PerformanceHistory
                {
                    TimeRange = timeRange,
                    GeneratedAt = DateTime.UtcNow
                };
            }

            var history = new PerformanceHistory
            {
                TimeRange = timeRange,
                Snapshots = snapshots,
                AverageResponseTime = (decimal)snapshots.Average(s => s.AvgResponseTimeMs),
                PeakResponseTime = (decimal)snapshots.Max(s => s.ResponseTimeMs),
                ErrorRate = snapshots.Sum(s => s.ErrorCount) > 0 
                    ? (decimal)snapshots.Sum(s => s.ErrorCount) / snapshots.Sum(s => s.RequestCount) * 100 
                    : 0,
                GeneratedAt = DateTime.UtcNow
            };

            // Cache for 2 minutes
            await _cacheService.SetAsync(history, cacheKey, TimeSpan.FromMinutes(2));

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance history for time range {TimeRange}", timeRange);
            
            return new PerformanceHistory
            {
                TimeRange = timeRange,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Get performance metrics for specific endpoint
    /// </summary>
    /// <param name="endpoint">Endpoint name</param>
    /// <param name="timeRange">Time range for metrics</param>
    /// <returns>Endpoint-specific performance metrics</returns>
    public async Task<EndpointPerformanceMetrics> GetEndpointMetricsAsync(string endpoint, TimeSpan? timeRange = null)
    {
        try
        {
            var range = timeRange ?? TimeSpan.FromHours(1);
            var cutoffTime = DateTime.UtcNow.Subtract(range);

            if (!_endpointMetrics.TryGetValue(endpoint, out var metrics))
            {
                return new EndpointPerformanceMetrics
                {
                    Endpoint = endpoint,
                    TimeRange = range,
                    RequestCount = 0
                };
            }

            var recentMetrics = metrics
                .Where(m => m.Timestamp >= cutoffTime)
                .ToList();

            if (!recentMetrics.Any())
            {
                return new EndpointPerformanceMetrics
                {
                    Endpoint = endpoint,
                    TimeRange = range,
                    RequestCount = 0
                };
            }

            return new EndpointPerformanceMetrics
            {
                Endpoint = endpoint,
                TimeRange = range,
                RequestCount = recentMetrics.Count,
                SuccessCount = recentMetrics.Count(m => m.IsSuccess),
                ErrorCount = recentMetrics.Count(m => !m.IsSuccess),
                AverageResponseTimeMs = (decimal)recentMetrics.Average(m => m.ResponseTimeMs),
                MinResponseTimeMs = recentMetrics.Min(m => m.ResponseTimeMs),
                MaxResponseTimeMs = recentMetrics.Max(m => m.ResponseTimeMs),
                SuccessRate = recentMetrics.Count > 0 
                    ? (decimal)recentMetrics.Count(m => m.IsSuccess) / recentMetrics.Count * 100 
                    : 0,
                RequestsPerMinute = (decimal)(recentMetrics.Count / range.TotalMinutes)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating endpoint metrics for {Endpoint}", endpoint);
            
            return new EndpointPerformanceMetrics
            {
                Endpoint = endpoint,
                TimeRange = timeRange ?? TimeSpan.FromHours(1),
                RequestCount = 0
            };
        }
    }

    /// <summary>
    /// Clear old performance data to prevent memory leaks
    /// </summary>
    public async Task CleanupOldDataAsync()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            
            // Clean up performance history
            var itemsToRemove = new List<PerformanceSnapshot>();
            while (_performanceHistory.TryPeek(out var snapshot) && snapshot.Timestamp < cutoffTime)
            {
                if (_performanceHistory.TryDequeue(out var removedSnapshot))
                {
                    itemsToRemove.Add(removedSnapshot);
                }
            }

            // Clean up endpoint metrics
            foreach (var kvp in _endpointMetrics.ToList())
            {
                var recentMetrics = kvp.Value.Where(m => m.Timestamp >= cutoffTime).ToList();
                _endpointMetrics[kvp.Key] = recentMetrics;
                
                // Remove endpoint entirely if no recent metrics
                if (!recentMetrics.Any())
                {
                    _endpointMetrics.TryRemove(kvp.Key, out _);
                }
            }

            _logger.LogDebug("Cleaned up {RemovedCount} old performance snapshots and pruned endpoint metrics", 
                itemsToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old performance data");
        }
    }

    #region Private Methods

    private List<PerformanceMetric> GetRecentMetrics(TimeSpan timeRange)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeRange);
        return _endpointMetrics
            .SelectMany(kvp => kvp.Value)
            .Where(m => m.Timestamp >= cutoffTime)
            .ToList();
    }

    private void RecordEndpointMetric(string endpoint, PerformanceMetric metric)
    {
        _endpointMetrics.AddOrUpdate(
            endpoint,
            new List<PerformanceMetric> { metric },
            (key, existingMetrics) =>
            {
                existingMetrics.Add(metric);
                
                // Keep only recent metrics to prevent memory growth
                var cutoffTime = DateTime.UtcNow.AddHours(-2);
                return existingMetrics.Where(m => m.Timestamp >= cutoffTime).ToList();
            }
        );
    }

    private void AddToPerformanceHistory(PerformanceSnapshot snapshot)
    {
        _performanceHistory.Enqueue(snapshot);
        
        // Maintain size limit
        while (_performanceHistory.Count > MAX_HISTORY_SIZE)
        {
            _performanceHistory.TryDequeue(out _);
        }
    }

    private int GetCurrentActiveConnections()
    {
        // In a real implementation, this would query the actual connection pool
        return Random.Shared.Next(5, 50);
    }

    private decimal GetCurrentCpuUsage()
    {
        try
        {
            // In a real implementation, use PerformanceCounters or similar
            return (decimal)(Random.Shared.NextDouble() * 30 + 10); // 10-40%
        }
        catch
        {
            return 0;
        }
    }

    private decimal GetCurrentMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            
            // Convert to percentage (simplified calculation)
            return Math.Min((decimal)(memoryMB / 10.24), 100); // Rough percentage
        }
        catch
        {
            return 0;
        }
    }

    private int GetCurrentDatabaseConnections()
    {
        // In a real implementation, this would query the connection pool
        return Random.Shared.Next(1, 10);
    }

    private bool DetermineHealthStatus(List<PerformanceMetric> recentMetrics)
    {
        if (!recentMetrics.Any()) return true;

        var errorRate = (double)recentMetrics.Count(m => !m.IsSuccess) / recentMetrics.Count;
        var avgResponseTime = recentMetrics.Average(m => m.ResponseTimeMs);

        // Consider healthy if error rate < 10% and average response time < 2000ms
        return errorRate < 0.1 && avgResponseTime < 2000;
    }

    private int CalculateRequestsPerMinute(List<PerformanceMetric> recentMetrics)
    {
        if (!recentMetrics.Any()) return 0;

        var timeSpan = DateTime.UtcNow - recentMetrics.Min(m => m.Timestamp);
        var minutes = Math.Max(timeSpan.TotalMinutes, 1);
        
        return (int)Math.Round(recentMetrics.Count / minutes);
    }

    private int CalculateErrorsPerMinute(List<PerformanceMetric> recentMetrics)
    {
        if (!recentMetrics.Any()) return 0;

        var errors = recentMetrics.Count(m => !m.IsSuccess);
        var timeSpan = DateTime.UtcNow - recentMetrics.Min(m => m.Timestamp);
        var minutes = Math.Max(timeSpan.TotalMinutes, 1);
        
        return (int)Math.Round(errors / minutes);
    }

    private decimal CalculateAverageResponseTime(List<PerformanceMetric> recentMetrics)
    {
        return recentMetrics.Any() ? (decimal)recentMetrics.Average(m => m.ResponseTimeMs) : 0;
    }

    private decimal CalculatePeakResponseTime(List<PerformanceMetric> recentMetrics)
    {
        return recentMetrics.Any() ? recentMetrics.Max(m => m.ResponseTimeMs) : 0;
    }

    private string GetCurrentSessionId()
    {
        // In a real implementation, this would get the actual session ID
        return $"session_{Random.Shared.Next(1000, 9999)}";
    }

    #endregion
}

/// <summary>
/// Individual performance metric record
/// </summary>
public class PerformanceMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public int ResponseTimeMs { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime Timestamp { get; set; }
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Performance metrics for a specific endpoint
/// </summary>
public class EndpointPerformanceMetrics
{
    public string Endpoint { get; set; } = string.Empty;
    public TimeSpan TimeRange { get; set; }
    public int RequestCount { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public decimal AverageResponseTimeMs { get; set; }
    public int MinResponseTimeMs { get; set; }
    public int MaxResponseTimeMs { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal RequestsPerMinute { get; set; }
}