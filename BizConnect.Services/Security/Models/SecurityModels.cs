using System;
using System.Collections.Generic;

namespace BizConnect.Services.Security.Models;

/// <summary>
/// Comprehensive security model definitions for the BizConnect security system.
/// Contains all data structures used across security services for consistency and maintainability.
/// </summary>

#region Core Security Models

/// <summary>
/// Represents a comprehensive security event with all relevant metadata.
/// </summary>
public class SecurityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? UserAgent { get; set; }
    public string? Endpoint { get; set; }
    public string? Details { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Represents threat analysis results with detailed scoring.
/// </summary>
public class ThreatAnalysisResult
{
    public bool IsThreat { get; set; }
    public ThreatSeverity Severity { get; set; }
    public double ThreatScore { get; set; }
    public List<string> ThreatIndicators { get; set; } = new();
    public string? RecommendedAction { get; set; }
    public Dictionary<string, double> AnalysisScores { get; set; } = new();
    public DateTime AnalyzedAt { get; set; }
    public string? AnalysisContext { get; set; }
    public bool RequiresManualReview { get; set; }
}

/// <summary>
/// Represents a detected suspicious pattern with evidence.
/// </summary>
public class SuspiciousPattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PatternType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int EventCount { get; set; }
    public ThreatSeverity Severity { get; set; }
    public double Confidence { get; set; }
    public Dictionary<string, object> Evidence { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionAction { get; set; }
}

/// <summary>
/// Represents a detected threat requiring immediate response.
/// </summary>
public class DetectedThreat
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ThreatType { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTime DetectedAt { get; set; }
    public List<SecurityEvent> RelatedEvents { get; set; } = new();
    public List<string> ThreatIndicators { get; set; } = new();
    public bool RequiresImmediateAction { get; set; }
    public string? Context { get; set; }
    public Dictionary<string, object> ThreatData { get; set; } = new();
}

#endregion

#region Response and Action Models

/// <summary>
/// Represents the result of security response execution.
/// </summary>
public class SecurityResponseResult
{
    public bool Success { get; set; }
    public List<string> ActionsExecuted { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ResponseId { get; set; }
    public Dictionary<string, object> ResponseMetadata { get; set; } = new();
}

/// <summary>
/// Represents a comprehensive response action taken by the system.
/// </summary>
public class ResponseAction
{
    public string ResponseId { get; set; } = Guid.NewGuid().ToString();
    public string ThreatId { get; set; } = string.Empty;
    public ThreatLevel ThreatLevel { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<IndividualAction> Actions { get; set; } = new();
    public ResponsePriority Priority { get; set; }
    public string? InitiatedBy { get; set; }
    public bool IsActive => !CompletedAt.HasValue || CompletedAt.Value.AddHours(1) > DateTime.UtcNow;
}

/// <summary>
/// Represents an individual action within a response.
/// </summary>
public class IndividualAction
{
    public string ActionType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public Dictionary<string, object> ActionParameters { get; set; } = new();
    public string? ActionResult { get; set; }
}

#endregion

#region Security Metrics and Dashboard Models

/// <summary>
/// Comprehensive security dashboard data.
/// </summary>
public class SecurityDashboardData
{
    public DateTime GeneratedAt { get; set; }
    public TimeSpan TimeRange { get; set; }
    public SecurityMetrics Metrics { get; set; } = new();
    public List<SecurityAlert> ActiveAlerts { get; set; } = new();
    public List<TopThreatIp> TopThreatIps { get; set; } = new();
    public List<SecurityEventSummary> RecentEvents { get; set; } = new();
    public Dictionary<string, int> EventTypeCounts { get; set; } = new();
    public Dictionary<string, int> ThreatTrendData { get; set; } = new();
    public List<SuspiciousPattern> ActivePatterns { get; set; } = new();
    public SecurityLevelStatus SecurityLevel { get; set; } = new();
    public List<ResponseAction> RecentResponses { get; set; } = new();
    
    // Critical missing properties for dashboard functionality
    public int TotalSecurityEvents { get; set; }
    public int ThreatCount { get; set; }
    public int BlockedIPs { get; set; }
    public ThreatLevel CurrentThreatLevel { get; set; }
    public List<SecurityAlert> RecentAlerts { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    
    // Additional properties for enhanced dashboard functionality
    public List<string> BlockedIpAddresses { get; set; } = new();
    public List<ComplianceViolation> ComplianceViolations { get; set; } = new();
    public Dictionary<string, int> EventsByCategory { get; set; } = new();
    public Dictionary<ThreatSeverity, int> EventsBySeverity { get; set; } = new();
    public Dictionary<int, int> HourlyTrend { get; set; } = new(); // Hour (0-23) to count
    public List<TopActiveIp> TopActiveIps { get; set; } = new();
}

/// <summary>
/// Represents a compliance violation.
/// </summary>
public class ComplianceViolation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public DateTime DetectedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? Username { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Represents a top active IP address with statistics.
/// </summary>
public class TopActiveIp
{
    public string IpAddress { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int ThreatCount { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public ThreatSeverity HighestThreatLevel { get; set; }
    public bool IsBlocked { get; set; }
}

/// <summary>
/// Security metrics for monitoring and analysis.
/// </summary>
public class SecurityMetrics
{
    public int TotalEvents { get; set; }
    public int ThreatEvents { get; set; }
    public int BlockedAttempts { get; set; }
    public int ActiveAlerts { get; set; }
    public int WatchlistedIps { get; set; }
    public double ThreatDetectionRate { get; set; }
    public double FalsePositiveRate { get; set; }
    public int AutomatedResponses { get; set; }
    public double AverageResponseTime { get; set; }
    public int ComplianceViolations { get; set; }
    public SecurityHealthScore HealthScore { get; set; } = new();
}

/// <summary>
/// Security health score breakdown.
/// </summary>
public class SecurityHealthScore
{
    public double OverallScore { get; set; }
    public double ThreatDetectionScore { get; set; }
    public double ResponseEffectivenessScore { get; set; }
    public double ComplianceScore { get; set; }
    public double SystemResilienceScore { get; set; }
    public DateTime CalculatedAt { get; set; }
    public List<string> ImprovementAreas { get; set; } = new();
}

#endregion

#region Threat Intelligence Models

/// <summary>
/// Comprehensive threat intelligence for an IP address.
/// </summary>
public class ThreatIntelligence
{
    public string IpAddress { get; set; } = string.Empty;
    public double ThreatScore { get; set; }
    public ThreatSeverity RiskLevel { get; set; }
    public List<string> ThreatCategories { get; set; } = new();
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int EventCount { get; set; }
    public int FailedAttempts { get; set; }
    public List<string> AssociatedUsernames { get; set; } = new();
    public List<string> TargetedEndpoints { get; set; } = new();
    public GeolocationInfo? Geolocation { get; set; }
    public ReputationInfo? Reputation { get; set; }
    public List<AttackPattern> DetectedPatterns { get; set; } = new();
    public ThreatHistory History { get; set; } = new();
}

/// <summary>
/// Geolocation information for threat analysis.
/// </summary>
public class GeolocationInfo
{
    public string Country { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Isp { get; set; }
    public string? Organization { get; set; }
    public bool IsAnonymousProxy { get; set; }
    public bool IsTorNode { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// IP reputation information.
/// </summary>
public class ReputationInfo
{
    public bool IsKnownMalicious { get; set; }
    public bool IsProxy { get; set; }
    public bool IsTor { get; set; }
    public bool IsVpn { get; set; }
    public List<string> BlacklistSources { get; set; } = new();
    public double ReputationScore { get; set; }
    public DateTime LastChecked { get; set; }
    public List<MalwareFamily> AssociatedMalware { get; set; } = new();
    public List<ThreatActor> AssociatedActors { get; set; } = new();
}

/// <summary>
/// Attack pattern information.
/// </summary>
public class AttackPattern
{
    public string PatternId { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime FirstObserved { get; set; }
    public DateTime LastObserved { get; set; }
    public int ObservationCount { get; set; }
    public List<string> Indicators { get; set; } = new();
}

/// <summary>
/// Threat history tracking.
/// </summary>
public class ThreatHistory
{
    public List<ThreatEvent> Events { get; set; } = new();
    public List<ResponseEvent> Responses { get; set; } = new();
    public ThreatTrend Trend { get; set; } = new();
    public DateTime? LastEscalation { get; set; }
    public int EscalationCount { get; set; }
    public bool IsRecurring { get; set; }
}

/// <summary>
/// Individual threat event in history.
/// </summary>
public class ThreatEvent
{
    public DateTime Timestamp { get; set; }
    public ThreatSeverity Severity { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> EventData { get; set; } = new();
}

/// <summary>
/// Response event in threat history.
/// </summary>
public class ResponseEvent
{
    public DateTime Timestamp { get; set; }
    public string ResponseType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Details { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Threat trend analysis.
/// </summary>
public class ThreatTrend
{
    public TrendDirection Direction { get; set; }
    public double TrendScore { get; set; }
    public TimeSpan AnalysisPeriod { get; set; }
    public List<TrendDataPoint> DataPoints { get; set; } = new();
    public string? TrendAnalysis { get; set; }
    public List<string> PredictedActions { get; set; } = new();
}

/// <summary>
/// Data point in trend analysis.
/// </summary>
public class TrendDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string? Context { get; set; }
}

#endregion

#region Alert and Notification Models

/// <summary>
/// Security alert with comprehensive details.
/// </summary>
public class SecurityAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public SecurityAlertStatus Status { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public List<string> RelatedEventIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public AlertPriority Priority { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    public List<AlertAction> Actions { get; set; } = new();
    public string? ResolutionNotes { get; set; }
    public TimeSpan? TimeToResolution { get; set; }
}

/// <summary>
/// Action taken on an alert.
/// </summary>
public class AlertAction
{
    public DateTime Timestamp { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Dictionary<string, object> ActionData { get; set; } = new();
}

/// <summary>
/// Alert resolution information.
/// </summary>
public class SecurityAlertResolution
{
    public string Resolution { get; set; } = string.Empty;
    public string ResolvedBy { get; set; } = string.Empty;
    public DateTime ResolvedAt { get; set; }
    public string? Notes { get; set; }
    public ResolutionType Type { get; set; }
    public bool PreventRecurrence { get; set; }
    public List<string> FollowUpActions { get; set; } = new();
}

/// <summary>
/// Admin notification for security events.
/// </summary>
public class AdminNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ThreatId { get; set; } = string.Empty;
    public ThreatLevel ThreatLevel { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public NotificationPriority Priority { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadBy { get; set; }
    public List<string> Recipients { get; set; } = new();
    public NotificationChannel Channel { get; set; }
    public bool RequiresAcknowledgment { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

#endregion

#region OTAC Security Models

/// <summary>
/// OTAC security event details.
/// </summary>
public class OtacSecurityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PhoneNumber { get; set; } = string.Empty;
    public string MaskedPhoneNumber { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public OtacEventType EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public TimeSpan? ResponseTime { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// OTAC rate limiting status.
/// </summary>
public class OtacRateLimitStatus
{
    public bool IsLimited { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTime? NextAttemptAllowed { get; set; }
    public TimeSpan? CooldownPeriod { get; set; }
    public string? LimitType { get; set; }
    public int TotalAttempts { get; set; }
    public DateTime? WindowStart { get; set; }
    public Dictionary<string, object> LimitDetails { get; set; } = new();
}

/// <summary>
/// OTAC validation context for security analysis.
/// </summary>
public class OtacValidationContext
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public DateTime RequestTime { get; set; }
    public int CurrentAttemptCount { get; set; }
    public List<DateTime> RecentAttempts { get; set; } = new();
    public bool IsFromTrustedNetwork { get; set; }
    public GeolocationInfo? RequestGeolocation { get; set; }
    public Dictionary<string, object> RequestMetadata { get; set; } = new();
}

#endregion

#region Configuration and Rule Models

/// <summary>
/// Security response rule configuration.
/// </summary>
public class SecurityResponseRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ThreatType { get; set; } = string.Empty;
    public ThreatSeverity MinSeverity { get; set; }
    public List<SecurityResponseAction> Actions { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; }
    public List<RuleCondition> Conditions { get; set; } = new();
    public RuleEvaluationMode EvaluationMode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Individual security response action.
/// </summary>
public class SecurityResponseAction
{
    public string ActionType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int DelaySeconds { get; set; }
    public bool IsCritical { get; set; }
    public int MaxRetries { get; set; } = 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public List<string> Prerequisites { get; set; } = new();
}

/// <summary>
/// Rule condition for security response.
/// </summary>
public class RuleCondition
{
    public string Property { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; }
    public object Value { get; set; } = new();
    public bool IsCaseSensitive { get; set; } = true;
    public LogicalOperator LogicalOperator { get; set; } = LogicalOperator.And;
}

/// <summary>
/// Security level escalation information.
/// </summary>
public class SecurityLevelEscalation
{
    public ThreatLevel PreviousLevel { get; set; }
    public ThreatLevel NewLevel { get; set; }
    public DateTime EscalatedAt { get; set; }
    public DateTime EscalationEnd { get; set; }
    public TimeSpan Duration { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? EscalatedBy { get; set; }
    public bool IsAutomatic { get; set; }
    public List<string> AffectedSystems { get; set; } = new();
    public Dictionary<string, object> EscalationMetadata { get; set; } = new();
}

/// <summary>
/// Current security level status.
/// </summary>
public class SecurityLevelStatus
{
    public ThreatLevel CurrentLevel { get; set; }
    public DateTime LevelSetAt { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
    public string? Reason { get; set; }
    public bool IsEscalated { get; set; }
    public List<SecurityLevelEscalation> RecentEscalations { get; set; } = new();
    public Dictionary<string, bool> SystemStatus { get; set; } = new();
}

#endregion

#region Supporting Data Models

/// <summary>
/// Top threat IP information for dashboard.
/// </summary>
public class TopThreatIp
{
    public string IpAddress { get; set; } = string.Empty;
    public double ThreatScore { get; set; }
    public int EventCount { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> ThreatTypes { get; set; } = new();
    public ThreatSeverity HighestSeverity { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsWatchlisted { get; set; }
    public GeolocationInfo? Location { get; set; }
}

/// <summary>
/// Security event summary for reporting.
/// </summary>
public class SecurityEventSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public ThreatSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool IsResolved { get; set; }
    public string? Category { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Malware family information.
/// </summary>
public class MalwareFamily
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> Indicators { get; set; } = new();
}

/// <summary>
/// Threat actor information.
/// </summary>
public class ThreatActor
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> KnownAliases { get; set; } = new();
    public List<string> TargetSectors { get; set; } = new();
    public List<string> TechniquesUsed { get; set; } = new();
    public double Confidence { get; set; }
}

/// <summary>
/// Threat information for security response.
/// </summary>
public class ThreatInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ThreatLevel Level { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Description { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int TotalRequests { get; set; }
    public int FailedAttempts { get; set; }
}

/// <summary>
/// User lockout information.
/// </summary>
public class UserLockoutInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty; // Critical missing property
    public string Reason { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; }
    public DateTime LockoutEnd { get; set; }
    public TimeSpan LockoutDuration { get; set; }
    public bool IsActive { get; set; }
    public int FailedAttempts { get; set; }
    public int AttemptCount { get; set; } // Total attempt count leading to lockout
    public string? LastFailedIpAddress { get; set; }
    public DateTime? LastFailedAttempt { get; set; }
    public LockoutType LockoutType { get; set; }
    public bool IsAutomatic { get; set; }
}

/// <summary>
/// IP block information.
/// </summary>
public class IpBlockInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
    public DateTime BlockEnd { get; set; }
    public TimeSpan BlockDuration { get; set; }
    public bool IsActive { get; set; }
    public BlockType BlockType { get; set; }
    public ThreatSeverity ThreatLevel { get; set; }
    public bool IsAutomatic { get; set; }
    public int ViolationCount { get; set; }
    public List<string> RelatedEvents { get; set; } = new();
}

#endregion

#region Rate Limiting Models

/// <summary>
/// Request for rate limiting operations.
/// </summary>
public class RateLimitRequest
{
    public string Operation { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? Username { get; set; }
    public string Context { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsFailedAttempt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Status information for rate limiting operations.
/// </summary>
public class RateLimitStatus
{
    public bool IsLocked { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? LockoutEndTime { get; set; }
    public TimeSpan? TimeUntilUnlock { get; set; }
    public int RemainingAttempts { get; set; }
    public DateTime ResetTime { get; set; }
    
    // Additional properties for compatibility and comprehensive status
    public int TotalAttempts { get; set; }
    public bool IsAllowed { get; set; } = true;
    public string StatusDetails { get; set; } = string.Empty;
    
    // Legacy properties for backward compatibility
    public bool IsLimited 
    { 
        get => IsLocked; 
        set => IsLocked = value; 
    }
    public TimeSpan? RetryAfter 
    { 
        get => TimeUntilUnlock; 
        set => TimeUntilUnlock = value; 
    }
    public string? LimitType { get; set; }
    public DateTime? WindowStart { get; set; }
    public Dictionary<string, object> AdditionalDetails { get; set; } = new();
}

#endregion

#region Enumerations

/// <summary>
/// Threat severity levels.
/// </summary>
public enum ThreatSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Threat levels for response classification.
/// </summary>
public enum ThreatLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Security alert status.
/// </summary>
public enum SecurityAlertStatus
{
    New,
    InProgress,
    Resolved,
    Dismissed,
    Escalated
}

/// <summary>
/// Security monitoring statistics.
/// </summary>
public class SecurityMonitoringStatistics
{
    public DateTime GeneratedAt { get; set; }
    public int EventsProcessed { get; set; }
    public int ThreatsDetected { get; set; }
    public int AlertsGenerated { get; set; }
    public int ResponsesExecuted { get; set; }
    public double AverageResponseTime { get; set; }
    public Dictionary<string, int> ThreatTypeBreakdown { get; set; } = new();
    public Dictionary<string, int> ResponseActionBreakdown { get; set; } = new();
}

/// <summary>
/// Alert priority levels.
/// </summary>
public enum AlertPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

/// <summary>
/// Notification priority levels.
/// </summary>
public enum NotificationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

/// <summary>
/// Response priority levels.
/// </summary>
public enum ResponsePriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// OTAC event types.
/// </summary>
public enum OtacEventType
{
    Generation,
    Validation,
    Expiration,
    RateLimit,
    Lockout
}

/// <summary>
/// Rule evaluation modes.
/// </summary>
public enum RuleEvaluationMode
{
    All,        // All conditions must be true
    Any,        // Any condition must be true
    Majority,   // Majority of conditions must be true
    Custom      // Custom evaluation logic
}

/// <summary>
/// Comparison operators for rule conditions.
/// </summary>
public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Matches,        // Regex match
    NotMatches,     // Regex not match
    In,             // Value in list
    NotIn           // Value not in list
}

/// <summary>
/// Logical operators for combining conditions.
/// </summary>
public enum LogicalOperator
{
    And,
    Or,
    Not
}

/// <summary>
/// Trend directions.
/// </summary>
public enum TrendDirection
{
    Stable,
    Increasing,
    Decreasing,
    Volatile,
    Unknown
}

/// <summary>
/// Resolution types for alerts.
/// </summary>
public enum ResolutionType
{
    Resolved,
    FalsePositive,
    Duplicate,
    NotApplicable,
    Escalated,
    WorkInProgress
}

/// <summary>
/// Notification channels.
/// </summary>
public enum NotificationChannel
{
    Dashboard,
    Email,
    Sms,
    Push,
    Webhook,
    Slack,
    Teams
}

/// <summary>
/// Lockout types.
/// </summary>
public enum LockoutType
{
    FailedAttempts,
    SecurityThreat,
    Manual,
    Compliance,
    Suspicious
}

/// <summary>
/// Block types for IP addresses.
/// </summary>
public enum BlockType
{
    Temporary,
    Permanent,
    Manual,
    Automatic,
    Compliance
}

#endregion