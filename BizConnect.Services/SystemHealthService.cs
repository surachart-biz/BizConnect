using BizConnect.Services.DTOs;
using BizConnect.Services.Interfaces;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Caching;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BizConnect.Services;

/// <summary>
/// System health monitoring service for comprehensive system status checking
/// </summary>
public class SystemHealthService : ISystemHealthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IKBankOddClient _kbankClient;
    private readonly ILogger<SystemHealthService> _logger;

    private const string SYSTEM_HEALTH_CACHE_KEY = "system:health_status";
    private const string ACTIVE_ALERTS_CACHE_KEY = "system:active_alerts";
    private const string PUBLIC_STATUS_CACHE_KEY = "system:public_status";
    
    private static readonly TimeSpan HealthCheckCacheExpiry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PublicStatusCacheExpiry = TimeSpan.FromMinutes(5);

    public SystemHealthService(
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IKBankOddClient kbankClient,
        ILogger<SystemHealthService> logger)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _kbankClient = kbankClient;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive system health status with detailed component checks
    /// </summary>
    public async Task<SystemHealthStatus> GetSystemHealthAsync()
    {
        try
        {
            // Check cache first
            var cachedHealth = await _cacheService.GetAsync<SystemHealthStatus>(SYSTEM_HEALTH_CACHE_KEY);
            if (cachedHealth != null)
            {
                return cachedHealth;
            }

            var stopwatch = Stopwatch.StartNew();
            
            // Run health checks in parallel for performance
            var healthCheckTasks = new[]
            {
                CheckDatabaseHealthAsync(),
                CheckKBankApiHealthAsync(),
                CheckBackgroundJobsHealthAsync(),
                CheckSystemResourcesHealthAsync(),
                CheckCacheHealthAsync()
            };

            var healthChecks = await Task.WhenAll(healthCheckTasks);
            var allChecks = healthChecks.ToList();

            stopwatch.Stop();

            // Determine overall system status
            var overallStatus = DetermineOverallStatus(allChecks);
            var systemMetrics = await GetSystemMetricsAsync();

            var systemHealth = new SystemHealthStatus
            {
                OverallStatus = overallStatus,
                DatabaseConnected = IsHealthy(allChecks, "Database"),
                KBankApiAvailable = IsHealthy(allChecks, "KBankAPI"),
                BackgroundJobsRunning = IsHealthy(allChecks, "BackgroundJobs"),
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                CpuUsagePercent = systemMetrics.CpuUsage,
                MemoryUsagePercent = systemMetrics.MemoryUsage,
                DiskUsagePercent = systemMetrics.DiskUsage,
                HealthChecks = allChecks,
                CheckedAt = DateTime.UtcNow
            };

            // Cache the health status
            await _cacheService.SetAsync(systemHealth, SYSTEM_HEALTH_CACHE_KEY, HealthCheckCacheExpiry);

            _logger.LogDebug("System health check completed in {ElapsedMs}ms with status {OverallStatus}", 
                stopwatch.ElapsedMilliseconds, overallStatus);

            return systemHealth;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system health check");
            return new SystemHealthStatus
            {
                OverallStatus = "Error",
                ResponseTimeMs = -1,
                CheckedAt = DateTime.UtcNow,
                HealthChecks = new List<HealthCheck>
                {
                    new HealthCheck
                    {
                        Name = "SystemHealth",
                        Status = "Error",
                        Description = "Failed to perform health check",
                        Details = new Dictionary<string, object> { ["error"] = ex.Message }
                    }
                }
            };
        }
    }

    /// <summary>
    /// Get active alerts and notifications with optional severity filtering
    /// </summary>
    public async Task<List<AlertMessage>> GetActiveAlertsAsync(string? severityFilter = null)
    {
        try
        {
            var cacheKey = string.IsNullOrEmpty(severityFilter) 
                ? ACTIVE_ALERTS_CACHE_KEY 
                : $"{ACTIVE_ALERTS_CACHE_KEY}:{severityFilter}";

            var cachedAlerts = await _cacheService.GetAsync<List<AlertMessage>>(cacheKey);
            if (cachedAlerts != null)
            {
                return cachedAlerts;
            }

            var alerts = await GenerateSystemAlertsAsync(severityFilter);
            
            await _cacheService.SetAsync(alerts, cacheKey, TimeSpan.FromMinutes(2));

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system alerts with severity filter {SeverityFilter}", severityFilter);
            return new List<AlertMessage>();
        }
    }

    /// <summary>
    /// Get public system status for guest users (limited information)
    /// </summary>
    public async Task<PublicSystemStatus> GetPublicStatusAsync()
    {
        try
        {
            var cachedStatus = await _cacheService.GetAsync<PublicSystemStatus>(PUBLIC_STATUS_CACHE_KEY);
            if (cachedStatus != null)
            {
                return cachedStatus;
            }

            // Perform lightweight checks for public status
            var publicStatus = await GeneratePublicSystemStatusAsync();
            
            await _cacheService.SetAsync(publicStatus, PUBLIC_STATUS_CACHE_KEY, PublicStatusCacheExpiry);

            return publicStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving public system status");
            return new PublicSystemStatus
            {
                Status = "Limited",
                Message = "System status temporarily unavailable",
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Check specific system component health
    /// </summary>
    public async Task<HealthCheck> CheckComponentHealthAsync(string componentName)
    {
        try
        {
            return componentName.ToLower() switch
            {
                "database" => await CheckDatabaseHealthAsync(),
                "kbankapi" => await CheckKBankApiHealthAsync(),
                "backgroundjobs" => await CheckBackgroundJobsHealthAsync(),
                "cache" => await CheckCacheHealthAsync(),
                "systemresources" => await CheckSystemResourcesHealthAsync(),
                _ => new HealthCheck
                {
                    Name = componentName,
                    Status = "Unknown",
                    Description = "Component not recognized",
                    ResponseTimeMs = 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health for component {ComponentName}", componentName);
            return new HealthCheck
            {
                Name = componentName,
                Status = "Error",
                Description = "Health check failed",
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    #region Private Health Check Methods

    private async Task<HealthCheck> CheckDatabaseHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Simple database connectivity test
            var testResult = await _unitOfWork.TestConnectionAsync();
            
            stopwatch.Stop();

            return new HealthCheck
            {
                Name = "Database",
                Status = testResult ? "Healthy" : "Unhealthy",
                Description = testResult ? "Database connection successful" : "Database connection failed",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["connectionTest"] = testResult,
                    ["responseTime"] = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Database health check failed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return new HealthCheck
            {
                Name = "Database",
                Status = "Error",
                Description = "Database health check failed",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    private async Task<HealthCheck> CheckKBankApiHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Test KBank API availability (without actual transaction)
            var isAvailable = await _kbankClient.TestConnectivityAsync();
            
            stopwatch.Stop();

            return new HealthCheck
            {
                Name = "KBankAPI",
                Status = isAvailable ? "Healthy" : "Degraded",
                Description = isAvailable ? "KBank API is accessible" : "KBank API is not responding",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["apiAvailable"] = isAvailable,
                    ["responseTime"] = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "KBank API health check failed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return new HealthCheck
            {
                Name = "KBankAPI",
                Status = "Error",
                Description = "KBank API health check failed",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    private async Task<HealthCheck> CheckBackgroundJobsHealthAsync()
    {
        try
        {
            // Check if background jobs are running (Hangfire status)
            var jobsRunning = await CheckHangfireStatusAsync();
            
            return new HealthCheck
            {
                Name = "BackgroundJobs",
                Status = jobsRunning ? "Healthy" : "Warning",
                Description = jobsRunning ? "Background jobs are running" : "Background jobs may be stopped",
                ResponseTimeMs = 0,
                Details = new Dictionary<string, object>
                {
                    ["hangfireActive"] = jobsRunning,
                    ["lastJobExecution"] = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(1, 60))
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background jobs health check failed");
            
            return new HealthCheck
            {
                Name = "BackgroundJobs",
                Status = "Error",
                Description = "Background jobs health check failed",
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    private async Task<HealthCheck> CheckCacheHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Test cache read/write
            var testKey = "health_check_test";
            var testValue = DateTime.UtcNow.ToString();
            
            await _cacheService.SetAsync(testValue, testKey, TimeSpan.FromMinutes(1));
            var retrievedValue = await _cacheService.GetAsync<string>(testKey);
            
            stopwatch.Stop();

            var isHealthy = retrievedValue == testValue;
            
            return new HealthCheck
            {
                Name = "Cache",
                Status = isHealthy ? "Healthy" : "Warning",
                Description = isHealthy ? "Cache read/write successful" : "Cache operations may be failing",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object>
                {
                    ["readWriteTest"] = isHealthy,
                    ["responseTime"] = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Cache health check failed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return new HealthCheck
            {
                Name = "Cache",
                Status = "Warning",
                Description = "Cache health check failed",
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    private async Task<HealthCheck> CheckSystemResourcesHealthAsync()
    {
        try
        {
            var metrics = await GetSystemMetricsAsync();
            
            var status = DetermineResourceStatus(metrics);
            var description = GenerateResourceDescription(metrics);
            
            return new HealthCheck
            {
                Name = "SystemResources",
                Status = status,
                Description = description,
                ResponseTimeMs = 0,
                Details = new Dictionary<string, object>
                {
                    ["cpuUsage"] = metrics.CpuUsage,
                    ["memoryUsage"] = metrics.MemoryUsage,
                    ["diskUsage"] = metrics.DiskUsage
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System resources health check failed");
            
            return new HealthCheck
            {
                Name = "SystemResources",
                Status = "Warning",
                Description = "System resources health check failed",
                Details = new Dictionary<string, object> { ["error"] = ex.Message }
            };
        }
    }

    #endregion

    #region Private Helper Methods

    private string DetermineOverallStatus(List<HealthCheck> healthChecks)
    {
        if (healthChecks.Any(h => h.Status == "Error"))
            return "Critical";
            
        if (healthChecks.Any(h => h.Status == "Unhealthy"))
            return "Error";
            
        if (healthChecks.Any(h => h.Status == "Warning" || h.Status == "Degraded"))
            return "Warning";
            
        return "Healthy";
    }

    private bool IsHealthy(List<HealthCheck> healthChecks, string componentName)
    {
        var check = healthChecks.FirstOrDefault(h => h.Name == componentName);
        return check?.Status == "Healthy";
    }

    private async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        // Implementation would integrate with system monitoring tools
        // For now, return simulated metrics
        return new SystemMetrics
        {
            CpuUsage = Random.Shared.Next(10, 80),
            MemoryUsage = Random.Shared.Next(30, 90),
            DiskUsage = Random.Shared.Next(40, 85)
        };
    }

    private string DetermineResourceStatus(SystemMetrics metrics)
    {
        if (metrics.CpuUsage > 90 || metrics.MemoryUsage > 95 || metrics.DiskUsage > 95)
            return "Error";
            
        if (metrics.CpuUsage > 80 || metrics.MemoryUsage > 85 || metrics.DiskUsage > 85)
            return "Warning";
            
        return "Healthy";
    }

    private string GenerateResourceDescription(SystemMetrics metrics)
    {
        return $"CPU: {metrics.CpuUsage}%, Memory: {metrics.MemoryUsage}%, Disk: {metrics.DiskUsage}%";
    }

    private async Task<bool> CheckHangfireStatusAsync()
    {
        // Implementation would check Hangfire dashboard or job status
        // For now, return simulated status
        return Random.Shared.NextDouble() > 0.1; // 90% chance of healthy
    }

    private async Task<List<AlertMessage>> GenerateSystemAlertsAsync(string? severityFilter)
    {
        var alerts = new List<AlertMessage>();
        
        // Generate sample alerts based on system conditions
        var systemHealth = await GetSystemHealthAsync();
        
        foreach (var healthCheck in systemHealth.HealthChecks)
        {
            if (healthCheck.Status == "Error" || healthCheck.Status == "Warning")
            {
                var severity = healthCheck.Status == "Error" ? "Error" : "Warning";
                
                if (string.IsNullOrEmpty(severityFilter) || 
                    severity.Equals(severityFilter, StringComparison.OrdinalIgnoreCase))
                {
                    alerts.Add(new AlertMessage
                    {
                        Id = Random.Shared.Next(1, 1000),
                        Title = $"{healthCheck.Name} Health Issue",
                        Message = healthCheck.Description ?? "Component health issue detected",
                        Severity = severity,
                        Category = "System",
                        CreatedAt = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(1, 60)),
                        IsRead = false,
                        IsResolved = false,
                        Details = healthCheck.Details
                    });
                }
            }
        }
        
        return alerts;
    }

    private async Task<PublicSystemStatus> GeneratePublicSystemStatusAsync()
    {
        // Lightweight checks for public status
        var dbHealthy = await TestDatabaseConnectivityAsync();
        var kbankHealthy = await TestKBankConnectivityAsync();
        
        var status = (dbHealthy && kbankHealthy) ? "Operational" : 
                     (dbHealthy || kbankHealthy) ? "Limited" : "Offline";
        
        return new PublicSystemStatus
        {
            Status = status,
            Message = GeneratePublicStatusMessage(status),
            IsOtacGenerationAvailable = dbHealthy,
            IsRegistrationAvailable = dbHealthy && kbankHealthy,
            LastUpdated = DateTime.UtcNow,
            Services = new List<ServiceStatus>
            {
                new ServiceStatus
                {
                    Name = "OTAC Generation",
                    Status = dbHealthy ? "Operational" : "Offline",
                    Description = dbHealthy ? null : "Service temporarily unavailable"
                },
                new ServiceStatus
                {
                    Name = "Registration Processing",
                    Status = (dbHealthy && kbankHealthy) ? "Operational" : "Limited",
                    Description = (dbHealthy && kbankHealthy) ? null : "Service may experience delays"
                }
            }
        };
    }

    private string GeneratePublicStatusMessage(string status)
    {
        return status switch
        {
            "Operational" => "All systems are operating normally",
            "Limited" => "Some services may experience delays",
            "Maintenance" => "System maintenance in progress",
            "Offline" => "Services are temporarily unavailable",
            _ => "System status unknown"
        };
    }

    private async Task<bool> TestDatabaseConnectivityAsync()
    {
        try
        {
            return await _unitOfWork.TestConnectionAsync();
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestKBankConnectivityAsync()
    {
        try
        {
            return await _kbankClient.TestConnectivityAsync();
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

/// <summary>
/// System metrics for resource monitoring
/// </summary>
public class SystemMetrics
{
    public decimal CpuUsage { get; set; }
    public decimal MemoryUsage { get; set; }
    public decimal DiskUsage { get; set; }
}