using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Security.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for comprehensive security monitoring and threat detection.
/// Provides advanced security analytics, threat intelligence, and automated response capabilities.
/// </summary>
public interface ISecurityMonitoringService
{
    /// <summary>
    /// Analyzes a security event to determine threat level and recommended actions.
    /// </summary>
    /// <param name="securityEvent">Security event to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Threat analysis result with scoring and recommendations</returns>
    Task<ThreatAnalysisResult> AnalyzeThreatAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects suspicious patterns across multiple security events.
    /// </summary>
    /// <param name="timeRange">Time range to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected suspicious patterns</returns>
    Task<List<SuspiciousPattern>> DetectSuspiciousPatternsAsync(TimeSpan timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors for threats and triggers automated responses.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected threats requiring action</returns>
    Task<List<DetectedThreat>> MonitorForThreatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes security response for a detected threat.
    /// </summary>
    /// <param name="threat">Detected threat to respond to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Security response execution result</returns>
    Task<SecurityResponseResult> ExecuteSecurityResponseAsync(DetectedThreat threat, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive security dashboard data for monitoring interface.
    /// </summary>
    /// <param name="timeRange">Time range for data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Security dashboard data</returns>
    Task<SecurityDashboardData> GetSecurityDashboardAsync(TimeSpan timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed threat intelligence for a specific IP address.
    /// </summary>
    /// <param name="ipAddress">IP address to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Threat intelligence data</returns>
    Task<ThreatIntelligence> GetThreatIntelligenceAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an IP address to the security watchlist.
    /// </summary>
    /// <param name="ipAddress">IP address to watch</param>
    /// <param name="reason">Reason for watching</param>
    /// <param name="severity">Severity level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task AddToWatchlistAsync(string ipAddress, string reason, ThreatSeverity severity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an IP address from the security watchlist.
    /// </summary>
    /// <param name="ipAddress">IP address to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task RemoveFromWatchlistAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets security monitoring statistics and performance metrics.
    /// </summary>
    /// <param name="timeRange">Time range for statistics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Security monitoring statistics</returns>
    Task<SecurityMonitoringStatistics> GetSecurityStatisticsAsync(TimeSpan timeRange, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active security alerts requiring attention.
    /// </summary>
    /// <param name="severityFilter">Optional severity filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active security alerts</returns>
    Task<List<SecurityAlert>> GetActiveAlertsAsync(ThreatSeverity? severityFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a security alert with resolution information.
    /// </summary>
    /// <param name="alertId">Alert ID to resolve</param>
    /// <param name="resolution">Resolution information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task ResolveAlertAsync(string alertId, SecurityAlertResolution resolution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates security response rules configuration.
    /// </summary>
    /// <param name="rules">Response rules to configure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task ConfigureResponseRulesAsync(List<SecurityResponseRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets security monitoring statistics and performance metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Security monitoring statistics</returns>
    Task<BizConnect.Services.Common.Result<SecurityMonitoringStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a security event for audit and monitoring purposes.
    /// </summary>
    /// <param name="eventType">Type of security event</param>
    /// <param name="ipAddress">IP address associated with the event</param>
    /// <param name="description">Description of the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task LogSecurityEventAsync(string eventType, string ipAddress, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks rate limiting for a specific operation and IP address.
    /// </summary>
    /// <param name="operation">Operation being rate limited</param>
    /// <param name="ipAddress">IP address to check</param>
    /// <param name="timeWindow">Time window for rate limiting</param>
    /// <param name="maxAttempts">Maximum attempts allowed in time window</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit check result</returns>
    Task<RateLimitResult> CheckRateLimitAsync(string operation, string ipAddress, TimeSpan timeWindow, int maxAttempts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks rate limiting using a structured request object.
    /// </summary>
    /// <param name="request">Rate limit request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit check result</returns>
    Task<RateLimitResult> CheckRateLimitAsync(RateLimitRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a security event for monitoring and analysis.
    /// </summary>
    /// <param name="securityEvent">Security event to log</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task LogSecurityEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a security event with category, action, and details.
    /// </summary>
    /// <param name="category">Event category</param>
    /// <param name="action">Action performed</param>
    /// <param name="details">Event details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    Task LogSecurityEventAsync(string category, string action, object details, CancellationToken cancellationToken = default);
}