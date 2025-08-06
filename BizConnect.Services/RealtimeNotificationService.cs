using BizConnect.Services.DTOs;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BizConnect.Services;

/// <summary>
/// Service for real-time notification and activity tracking
/// </summary>
public class RealtimeNotificationService : IRealtimeNotificationService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<RealtimeNotificationService> _logger;
    
    // In-memory storage for real-time activities
    private readonly ConcurrentQueue<ActivityNotification> _recentActivities;
    private readonly ConcurrentDictionary<string, UserSession> _activeSessions;
    
    private const string ACTIVITY_FEED_CACHE_KEY = "realtime:activity_feed";
    private const string USER_SESSIONS_CACHE_KEY = "realtime:user_sessions";
    private const int MAX_ACTIVITY_HISTORY = 500;
    
    public RealtimeNotificationService(
        ICacheService cacheService,
        ILogger<RealtimeNotificationService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
        _recentActivities = new ConcurrentQueue<ActivityNotification>();
        _activeSessions = new ConcurrentDictionary<string, UserSession>();
    }

    /// <summary>
    /// Track new OTAC generation activity
    /// </summary>
    /// <param name="otacCode">Generated OTAC code</param>
    /// <param name="externalReference">External reference</param>
    /// <param name="branchName">Branch name</param>
    /// <param name="userId">User ID who generated</param>
    public async Task TrackOtacGenerationAsync(string otacCode, string? externalReference, string? branchName, string? userId = null)
    {
        try
        {
            var activity = new ActivityNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = "OTAC_GENERATED",
                Title = "OTAC Code Generated",
                Description = $"New OTAC code generated for registration",
                Timestamp = DateTime.UtcNow,
                Severity = "Info",
                Category = "OTAC",
                UserId = userId ?? "System",
                Metadata = new Dictionary<string, object>
                {
                    ["otacCode"] = MaskOtacCode(otacCode),
                    ["externalReference"] = externalReference ?? string.Empty,
                    ["branchName"] = branchName ?? "Unknown",
                    ["fullOtacCode"] = otacCode // For internal tracking only
                },
                IsSystemGenerated = string.IsNullOrEmpty(userId),
                RequiresUserAttention = false
            };

            await AddActivityNotificationAsync(activity);
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Tracked OTAC generation: {OtacCode} for {ExternalReference}", 
                MaskOtacCode(otacCode), externalReference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking OTAC generation for code {OtacCode}", MaskOtacCode(otacCode));
        }
    }

    /// <summary>
    /// Track OTAC validation activity
    /// </summary>
    /// <param name="otacCode">Validated OTAC code</param>
    /// <param name="isValid">Whether validation was successful</param>
    /// <param name="externalReference">External reference</param>
    /// <param name="userId">User ID who validated</param>
    public async Task TrackOtacValidationAsync(string otacCode, bool isValid, string? externalReference, string? userId = null)
    {
        try
        {
            var activity = new ActivityNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = isValid ? "OTAC_VALIDATED" : "OTAC_VALIDATION_FAILED",
                Title = isValid ? "OTAC Code Validated" : "OTAC Validation Failed",
                Description = isValid 
                    ? $"OTAC code successfully validated" 
                    : $"OTAC code validation failed",
                Timestamp = DateTime.UtcNow,
                Severity = isValid ? "Success" : "Warning",
                Category = "OTAC",
                UserId = userId ?? "System",
                Metadata = new Dictionary<string, object>
                {
                    ["otacCode"] = MaskOtacCode(otacCode),
                    ["externalReference"] = externalReference ?? string.Empty,
                    ["validationResult"] = isValid,
                    ["fullOtacCode"] = otacCode // For internal tracking only
                },
                IsSystemGenerated = string.IsNullOrEmpty(userId),
                RequiresUserAttention = !isValid
            };

            await AddActivityNotificationAsync(activity);
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Tracked OTAC validation: {OtacCode} - Valid: {IsValid}", 
                MaskOtacCode(otacCode), isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking OTAC validation for code {OtacCode}", MaskOtacCode(otacCode));
        }
    }

    /// <summary>
    /// Track registration completion activity
    /// </summary>
    /// <param name="externalReference">External reference</param>
    /// <param name="status">Registration status (Success, Fail)</param>
    /// <param name="branchName">Branch name</param>
    /// <param name="otacCode">Associated OTAC code</param>
    public async Task TrackRegistrationCompletionAsync(string externalReference, string status, string? branchName, string? otacCode = null)
    {
        try
        {
            var isSuccess = status.Equals("Success", StringComparison.OrdinalIgnoreCase);
            
            var activity = new ActivityNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = isSuccess ? "REGISTRATION_COMPLETED" : "REGISTRATION_FAILED",
                Title = isSuccess ? "Registration Completed" : "Registration Failed",
                Description = isSuccess 
                    ? $"KBank registration completed successfully" 
                    : $"KBank registration failed",
                Timestamp = DateTime.UtcNow,
                Severity = isSuccess ? "Success" : "Error",
                Category = "Registration",
                UserId = "System",
                Metadata = new Dictionary<string, object>
                {
                    ["externalReference"] = externalReference,
                    ["status"] = status,
                    ["branchName"] = branchName ?? "Unknown",
                    ["otacCode"] = !string.IsNullOrEmpty(otacCode) ? MaskOtacCode(otacCode) : string.Empty
                },
                IsSystemGenerated = true,
                RequiresUserAttention = !isSuccess
            };

            await AddActivityNotificationAsync(activity);
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Tracked registration completion: {ExternalReference} - Status: {Status}", 
                externalReference, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking registration completion for {ExternalReference}", externalReference);
        }
    }

    /// <summary>
    /// Track user login activity
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="username">Username</param>
    /// <param name="isSuccess">Whether login was successful</param>
    /// <param name="ipAddress">User IP address</param>
    public async Task TrackUserLoginAsync(string userId, string username, bool isSuccess, string? ipAddress = null)
    {
        try
        {
            var activity = new ActivityNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = isSuccess ? "USER_LOGIN" : "USER_LOGIN_FAILED",
                Title = isSuccess ? "User Logged In" : "Login Failed",
                Description = isSuccess 
                    ? $"User {username} logged in successfully" 
                    : $"Failed login attempt for user {username}",
                Timestamp = DateTime.UtcNow,
                Severity = isSuccess ? "Info" : "Warning",
                Category = "Authentication",
                UserId = userId,
                Metadata = new Dictionary<string, object>
                {
                    ["username"] = username,
                    ["loginResult"] = isSuccess,
                    ["ipAddress"] = ipAddress ?? "Unknown"
                },
                IsSystemGenerated = false,
                RequiresUserAttention = !isSuccess
            };

            await AddActivityNotificationAsync(activity);
            
            // Track active session if login successful
            if (isSuccess)
            {
                await TrackUserSessionAsync(userId, username, ipAddress);
            }
            
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Tracked user login: {Username} - Success: {IsSuccess}", username, isSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking user login for {Username}", username);
        }
    }

    /// <summary>
    /// Track system health events
    /// </summary>
    /// <param name="component">System component</param>
    /// <param name="status">Health status</param>
    /// <param name="message">Status message</param>
    /// <param name="severity">Event severity</param>
    public async Task TrackSystemHealthEventAsync(string component, string status, string message, string severity = "Info")
    {
        try
        {
            var activity = new ActivityNotification
            {
                Id = Guid.NewGuid().ToString(),
                Type = "SYSTEM_HEALTH_EVENT",
                Title = $"{component} Health Update",
                Description = message,
                Timestamp = DateTime.UtcNow,
                Severity = severity,
                Category = "System",
                UserId = "System",
                Metadata = new Dictionary<string, object>
                {
                    ["component"] = component,
                    ["healthStatus"] = status,
                    ["originalMessage"] = message
                },
                IsSystemGenerated = true,
                RequiresUserAttention = severity == "Error" || severity == "Critical"
            };

            await AddActivityNotificationAsync(activity);
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Tracked system health event: {Component} - {Status}", component, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking system health event for {Component}", component);
        }
    }

    /// <summary>
    /// Get recent activity feed for dashboard
    /// </summary>
    /// <param name="count">Number of activities to return</param>
    /// <param name="userId">Optional user ID filter</param>
    /// <returns>List of recent activities</returns>
    public async Task<List<ActivityNotification>> GetRecentActivitiesAsync(int count = 20, string? userId = null)
    {
        try
        {
            var cacheKey = $"{ACTIVITY_FEED_CACHE_KEY}:{count}:{userId ?? "all"}";
            var cachedActivities = await _cacheService.GetAsync<List<ActivityNotification>>(cacheKey);
            if (cachedActivities != null)
            {
                return cachedActivities;
            }

            var activities = _recentActivities.ToList();
            
            // Apply user filter if specified
            if (!string.IsNullOrEmpty(userId))
            {
                activities = activities.Where(a => a.UserId == userId || a.IsSystemGenerated).ToList();
            }

            // Sort by timestamp and take requested count
            var recentActivities = activities
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToList();

            // Cache for 30 seconds
            await _cacheService.SetAsync(recentActivities, cacheKey, TimeSpan.FromSeconds(30));

            return recentActivities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activities for user {UserId}", userId);
            return new List<ActivityNotification>();
        }
    }

    /// <summary>
    /// Get active user sessions count
    /// </summary>
    /// <returns>Number of active user sessions</returns>
    public async Task<int> GetActiveSessionsCountAsync()
    {
        try
        {
            var cachedCount = await _cacheService.GetAsync<int?>(USER_SESSIONS_CACHE_KEY);
            if (cachedCount.HasValue)
            {
                return cachedCount.Value;
            }

            // Clean up expired sessions
            await CleanupExpiredSessionsAsync();
            
            var activeCount = _activeSessions.Count;
            
            // Cache for 1 minute
            await _cacheService.SetAsync(activeCount, USER_SESSIONS_CACHE_KEY, TimeSpan.FromMinutes(1));
            
            return activeCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions count");
            return 0;
        }
    }

    /// <summary>
    /// Get activity statistics for dashboard
    /// </summary>
    /// <param name="timeRange">Time range for statistics</param>
    /// <returns>Activity statistics</returns>
    public async Task<ActivityStatistics> GetActivityStatisticsAsync(TimeSpan? timeRange = null)
    {
        try
        {
            var range = timeRange ?? TimeSpan.FromHours(24);
            var cutoffTime = DateTime.UtcNow.Subtract(range);
            
            var activities = _recentActivities
                .Where(a => a.Timestamp >= cutoffTime)
                .ToList();

            var statistics = new ActivityStatistics
            {
                TimeRange = range,
                TotalActivities = activities.Count,
                OtacGenerated = activities.Count(a => a.Type == "OTAC_GENERATED"),
                OtacValidated = activities.Count(a => a.Type == "OTAC_VALIDATED"),
                OtacValidationFailed = activities.Count(a => a.Type == "OTAC_VALIDATION_FAILED"),
                RegistrationsCompleted = activities.Count(a => a.Type == "REGISTRATION_COMPLETED"),
                RegistrationsFailed = activities.Count(a => a.Type == "REGISTRATION_FAILED"),
                UserLogins = activities.Count(a => a.Type == "USER_LOGIN"),
                LoginFailures = activities.Count(a => a.Type == "USER_LOGIN_FAILED"),
                SystemHealthEvents = activities.Count(a => a.Type == "SYSTEM_HEALTH_EVENT"),
                GeneratedAt = DateTime.UtcNow,
                MostActiveUsers = GetMostActiveUsers(activities, 5),
                ActivityByHour = GetActivityByHour(activities, range)
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating activity statistics");
            
            return new ActivityStatistics
            {
                TimeRange = timeRange ?? TimeSpan.FromHours(24),
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Clean up old activities to prevent memory growth
    /// </summary>
    public async Task CleanupOldActivitiesAsync()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var itemsToRemove = new List<ActivityNotification>();
            
            // Remove activities older than 24 hours
            while (_recentActivities.TryPeek(out var activity) && activity.Timestamp < cutoffTime)
            {
                if (_recentActivities.TryDequeue(out var removedActivity))
                {
                    itemsToRemove.Add(removedActivity);
                }
            }

            await CleanupExpiredSessionsAsync();
            await InvalidateActivityCacheAsync();
            
            _logger.LogDebug("Cleaned up {RemovedCount} old activities", itemsToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old activities");
        }
    }

    #region Private Methods

    private async Task AddActivityNotificationAsync(ActivityNotification activity)
    {
        _recentActivities.Enqueue(activity);
        
        // Maintain size limit
        while (_recentActivities.Count > MAX_ACTIVITY_HISTORY)
        {
            _recentActivities.TryDequeue(out _);
        }
    }

    private async Task TrackUserSessionAsync(string userId, string username, string? ipAddress)
    {
        var session = new UserSession
        {
            UserId = userId,
            Username = username,
            LoginTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IpAddress = ipAddress ?? "Unknown",
            IsActive = true
        };

        _activeSessions.AddOrUpdate(userId, session, (key, existing) => session);
    }

    private async Task CleanupExpiredSessionsAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-8); // Consider sessions expired after 8 hours
        
        var expiredSessions = _activeSessions
            .Where(kvp => kvp.Value.LastActivity < cutoffTime)
            .ToList();

        foreach (var kvp in expiredSessions)
        {
            _activeSessions.TryRemove(kvp.Key, out _);
        }
    }

    private async Task InvalidateActivityCacheAsync()
    {
        // In a real implementation, you might use cache tags or patterns
        // For now, we'll let the cache expire naturally
    }

    private string MaskOtacCode(string otacCode)
    {
        if (string.IsNullOrEmpty(otacCode) || otacCode.Length < 4)
            return "****";
            
        return $"{otacCode[..2]}****{otacCode[^2..]}";
    }

    private List<UserActivitySummary> GetMostActiveUsers(List<ActivityNotification> activities, int count)
    {
        return activities
            .Where(a => !a.IsSystemGenerated && !string.IsNullOrEmpty(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new UserActivitySummary
            {
                UserId = g.Key,
                ActivityCount = g.Count(),
                LastActivity = g.Max(a => a.Timestamp),
                MostCommonActivityType = g.GroupBy(a => a.Type)
                    .OrderByDescending(t => t.Count())
                    .First().Key
            })
            .OrderByDescending(u => u.ActivityCount)
            .Take(count)
            .ToList();
    }

    private Dictionary<int, int> GetActivityByHour(List<ActivityNotification> activities, TimeSpan timeRange)
    {
        var hourlyActivity = new Dictionary<int, int>();
        
        // Initialize all hours in range with 0
        var hours = (int)Math.Ceiling(timeRange.TotalHours);
        for (int i = 0; i < Math.Min(hours, 24); i++)
        {
            hourlyActivity[i] = 0;
        }

        // Count activities by hour
        foreach (var activity in activities)
        {
            var hour = activity.Timestamp.Hour;
            if (hourlyActivity.ContainsKey(hour))
            {
                hourlyActivity[hour]++;
            }
        }

        return hourlyActivity;
    }

    #endregion
}

/// <summary>
/// Real-time activity notification model
/// </summary>
public class ActivityNotification
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Severity { get; set; } = "Info"; // Info, Success, Warning, Error, Critical
    public string Category { get; set; } = string.Empty; // OTAC, Registration, Authentication, System
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsSystemGenerated { get; set; }
    public bool RequiresUserAttention { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>
/// User session tracking model
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public DateTime LastActivity { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Activity statistics model
/// </summary>
public class ActivityStatistics
{
    public TimeSpan TimeRange { get; set; }
    public int TotalActivities { get; set; }
    public int OtacGenerated { get; set; }
    public int OtacValidated { get; set; }
    public int OtacValidationFailed { get; set; }
    public int RegistrationsCompleted { get; set; }
    public int RegistrationsFailed { get; set; }
    public int UserLogins { get; set; }
    public int LoginFailures { get; set; }
    public int SystemHealthEvents { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<UserActivitySummary> MostActiveUsers { get; set; } = new();
    public Dictionary<int, int> ActivityByHour { get; set; } = new();
}

/// <summary>
/// User activity summary model
/// </summary>
public class UserActivitySummary
{
    public string UserId { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
    public DateTime LastActivity { get; set; }
    public string MostCommonActivityType { get; set; } = string.Empty;
}