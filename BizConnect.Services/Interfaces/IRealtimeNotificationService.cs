using BizConnect.Services;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for real-time notification and activity tracking
/// </summary>
public interface IRealtimeNotificationService
{
    /// <summary>
    /// Track new OTAC generation activity
    /// </summary>
    /// <param name="otacCode">Generated OTAC code</param>
    /// <param name="externalReference">External reference</param>
    /// <param name="branchName">Branch name</param>
    /// <param name="userId">User ID who generated</param>
    Task TrackOtacGenerationAsync(string otacCode, string? externalReference, string? branchName, string? userId = null);

    /// <summary>
    /// Track OTAC validation activity
    /// </summary>
    /// <param name="otacCode">Validated OTAC code</param>
    /// <param name="isValid">Whether validation was successful</param>
    /// <param name="externalReference">External reference</param>
    /// <param name="userId">User ID who validated</param>
    Task TrackOtacValidationAsync(string otacCode, bool isValid, string? externalReference, string? userId = null);

    /// <summary>
    /// Track registration completion activity
    /// </summary>
    /// <param name="externalReference">External reference</param>
    /// <param name="status">Registration status (Success, Fail)</param>
    /// <param name="branchName">Branch name</param>
    /// <param name="otacCode">Associated OTAC code</param>
    Task TrackRegistrationCompletionAsync(string externalReference, string status, string? branchName, string? otacCode = null);

    /// <summary>
    /// Track user login activity
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="username">Username</param>
    /// <param name="isSuccess">Whether login was successful</param>
    /// <param name="ipAddress">User IP address</param>
    Task TrackUserLoginAsync(string userId, string username, bool isSuccess, string? ipAddress = null);

    /// <summary>
    /// Track system health events
    /// </summary>
    /// <param name="component">System component</param>
    /// <param name="status">Health status</param>
    /// <param name="message">Status message</param>
    /// <param name="severity">Event severity</param>
    Task TrackSystemHealthEventAsync(string component, string status, string message, string severity = "Info");

    /// <summary>
    /// Get recent activity feed for dashboard
    /// </summary>
    /// <param name="count">Number of activities to return</param>
    /// <param name="userId">Optional user ID filter</param>
    /// <returns>List of recent activities</returns>
    Task<List<ActivityNotification>> GetRecentActivitiesAsync(int count = 20, string? userId = null);

    /// <summary>
    /// Get active user sessions count
    /// </summary>
    /// <returns>Number of active user sessions</returns>
    Task<int> GetActiveSessionsCountAsync();

    /// <summary>
    /// Get activity statistics for dashboard
    /// </summary>
    /// <param name="timeRange">Time range for statistics</param>
    /// <returns>Activity statistics</returns>
    Task<ActivityStatistics> GetActivityStatisticsAsync(TimeSpan? timeRange = null);

    /// <summary>
    /// Clean up old activities to prevent memory growth
    /// </summary>
    Task CleanupOldActivitiesAsync();

    // Real-time Broadcasting Capabilities for Frontend Updates
    
    /// <summary>
    /// Broadcast dashboard statistics update to connected clients
    /// </summary>
    /// <param name="stats">Updated dashboard statistics</param>
    /// <returns>Task representing the broadcast operation</returns>
    Task BroadcastDashboardUpdateAsync(object stats);

    /// <summary>
    /// Broadcast registration status update to specific user groups
    /// </summary>
    /// <param name="registrationId">ID of the updated registration</param>
    /// <param name="status">New status</param>
    /// <param name="targetGroups">User groups to notify (optional, defaults to all admin users)</param>
    /// <returns>Task representing the broadcast operation</returns>
    Task BroadcastRegistrationUpdateAsync(int registrationId, string status, List<string>? targetGroups = null);

    /// <summary>
    /// Broadcast OTAC status update (expiry, validation, usage)
    /// </summary>
    /// <param name="otacCode">OTAC code (masked)</param>
    /// <param name="newState">New OTAC state</param>
    /// <param name="expiresAt">Expiration time for countdown updates</param>
    /// <returns>Task representing the broadcast operation</returns>
    Task BroadcastOtacUpdateAsync(string otacCode, string newState, DateTime? expiresAt = null);

    /// <summary>
    /// Broadcast system health alert to administrators
    /// </summary>
    /// <param name="alertType">Type of alert (Warning, Error, Critical)</param>
    /// <param name="message">Alert message</param>
    /// <param name="metadata">Additional alert data</param>
    /// <returns>Task representing the broadcast operation</returns>
    Task BroadcastSystemAlertAsync(string alertType, string message, Dictionary<string, object>? metadata = null);
}

/// <summary>
/// Real-time update event types for frontend consumption
/// </summary>
public static class RealtimeEventTypes
{
    public const string DashboardStatsUpdated = "dashboard_stats_updated";
    public const string RegistrationStatusChanged = "registration_status_changed";
    public const string OtacStateChanged = "otac_state_changed";
    public const string OtacExpiring = "otac_expiring";
    public const string SystemHealthAlert = "system_health_alert";
    public const string NewRegistration = "new_registration";
    public const string BranchPerformanceUpdated = "branch_performance_updated";
}

/// <summary>
/// Real-time notification payload structure
/// </summary>
public class RealtimeNotificationPayload
{
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public object Data { get; set; } = new();
    public string? TargetGroup { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}