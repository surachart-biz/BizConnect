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
}