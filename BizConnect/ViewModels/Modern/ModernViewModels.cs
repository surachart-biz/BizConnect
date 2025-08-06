using BizConnect.Services.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace BizConnect.ViewModels.Modern;

#region Landing Page ViewModels

/// <summary>
/// Modern landing page view model with enhanced UI support
/// </summary>
public class ModernLandingPageViewModel
{
    /// <summary>
    /// Whether to show the OTAC entry form
    /// </summary>
    public bool ShowOtacForm { get; set; } = true;

    /// <summary>
    /// Localized welcome message for the user
    /// </summary>
    public string WelcomeMessage { get; set; } = string.Empty;

    /// <summary>
    /// Trust indicators to display for user confidence
    /// </summary>
    public List<TrustIndicator> TrustIndicators { get; set; } = new();

    /// <summary>
    /// Public system status for guest users
    /// </summary>
    public PublicSystemStatus SystemStatus { get; set; } = new();

    /// <summary>
    /// Feature highlights for landing page
    /// </summary>
    public List<FeatureHighlight> Features { get; set; } = new();

    /// <summary>
    /// Security badges to display
    /// </summary>
    public List<SecurityBadge> SecurityBadges { get; set; } = new();

    /// <summary>
    /// Support information
    /// </summary>
    public SupportInfo SupportInfo { get; set; } = new();
}

/// <summary>
/// Enhanced OTAC verification view model with real-time feedback
/// </summary>
public class ModernOtacVerificationViewModel
{
    [Required(ErrorMessage = "กรุณากรอกรหัส OTAC")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "รหัส OTAC ต้องมี 8 ตัวอักษรเท่านั้น")]
    [Display(Name = "รหัส OTAC")]
    public string OtacCode { get; set; } = string.Empty;

    /// <summary>
    /// Number of validation attempts remaining
    /// </summary>
    public int AttemptsRemaining { get; set; } = 5;

    /// <summary>
    /// Whether the code is currently locked
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Lockout time remaining in seconds
    /// </summary>
    public int LockoutTimeRemaining { get; set; }

    /// <summary>
    /// Validation result message
    /// </summary>
    public string? ValidationMessage { get; set; }

    /// <summary>
    /// Whether validation is in progress
    /// </summary>
    public bool IsValidating { get; set; }

    /// <summary>
    /// Security level indicator
    /// </summary>
    public string SecurityLevel { get; set; } = "High";

    /// <summary>
    /// Help information for OTAC entry
    /// </summary>
    public OtacHelpInfo HelpInfo { get; set; } = new();
}

/// <summary>
/// Enhanced registration form view model with modern UI features
/// </summary>
public class ModernRegistrationViewModel
{
    [Required]
    public string OtacCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล")]
    [StringLength(200, ErrorMessage = "ชื่อ-นามสกุลต้องไม่เกิน 200 ตัวอักษร")]
    [Display(Name = "ชื่อ-นามสกุล")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเลขที่เอกสาร")]
    [StringLength(50, ErrorMessage = "เลขที่เอกสารต้องไม่เกิน 50 ตัวอักษร")]
    [Display(Name = "เลขที่เอกสาร")]
    public string IdValue { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเบอร์มือถือ")]
    [RegularExpression(@"^(08|09|\+66)[0-9]{8,9}$", ErrorMessage = "รูปแบบเบอร์มือถือไม่ถูกต้อง")]
    [Display(Name = "เบอร์มือถือ")]
    public string MobileNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเลขที่บัญชี")]
    [RegularExpression(@"^[0-9]{10,15}$", ErrorMessage = "เลขที่บัญชีต้องเป็นตัวเลข 10-15 หลัก")]
    [Display(Name = "เลขที่บัญชี")]
    public string AccountNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณาเลือกสาขา")]
    [Display(Name = "สาขา")]
    public int BranchId { get; set; }

    public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Progress indicator for multi-step process
    /// </summary>
    public RegistrationProgress Progress { get; set; } = new();

    /// <summary>
    /// Form validation status
    /// </summary>
    public FormValidationStatus ValidationStatus { get; set; } = new();

    /// <summary>
    /// Security indicators for the form
    /// </summary>
    public FormSecurityInfo SecurityInfo { get; set; } = new();

    /// <summary>
    /// Estimated processing time
    /// </summary>
    public string EstimatedProcessingTime { get; set; } = "2-3 minutes";

    /// <summary>
    /// Terms and conditions acceptance
    /// </summary>
    [Required(ErrorMessage = "กรุณายอมรับข้อกำหนดและเงื่อนไข")]
    public bool AcceptTerms { get; set; }

    /// <summary>
    /// Privacy policy acceptance
    /// </summary>
    [Required(ErrorMessage = "กรุณายอมรับนโยบายความเป็นส่วนตัว")]
    public bool AcceptPrivacy { get; set; }
}

#endregion

#region Dashboard ViewModels

/// <summary>
/// Modern admin dashboard view model with real-time capabilities
/// </summary>
public class ModernAdminDashboardViewModel
{
    /// <summary>
    /// Real-time dashboard statistics
    /// </summary>
    public DashboardRealTimeStats Stats { get; set; } = new();

    /// <summary>
    /// Recent activities for admin monitoring
    /// </summary>
    public List<RecentActivityDto> RecentActivities { get; set; } = new();

    /// <summary>
    /// System alerts and notifications
    /// </summary>
    public List<AlertMessage> SystemAlerts { get; set; } = new();

    /// <summary>
    /// Performance metrics summary
    /// </summary>
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// System health status
    /// </summary>
    public SystemHealthStatus SystemHealth { get; set; } = new();

    /// <summary>
    /// Quick action buttons for admin
    /// </summary>
    public List<QuickAction> QuickActions { get; set; } = new();

    /// <summary>
    /// Dashboard widgets configuration
    /// </summary>
    public List<DashboardWidget> Widgets { get; set; } = new();

    /// <summary>
    /// User permissions for UI rendering
    /// </summary>
    public UserPermissions UserPermissions { get; set; } = new();
}

/// <summary>
/// Modern analytics dashboard view model
/// </summary>
public class ModernAnalyticsViewModel
{
    /// <summary>
    /// Chart data for various analytics
    /// </summary>
    public Dictionary<string, ChartData> ChartData { get; set; } = new();

    /// <summary>
    /// KPI metrics summary
    /// </summary>
    public KpiSummary KpiMetrics { get; set; } = new();

    /// <summary>
    /// Trend analysis results
    /// </summary>
    public Dictionary<string, TrendAnalysis> TrendAnalysis { get; set; } = new();

    /// <summary>
    /// Time range selection options
    /// </summary>
    public List<SelectListItem> TimeRangeOptions { get; set; } = new();

    /// <summary>
    /// Selected time range
    /// </summary>
    public string SelectedTimeRange { get; set; } = "24h";

    /// <summary>
    /// Export options for analytics data
    /// </summary>
    public List<ExportOption> ExportOptions { get; set; } = new();

    /// <summary>
    /// Filter options for analytics
    /// </summary>
    public AnalyticsFilters Filters { get; set; } = new();
}

#endregion

#region Landing Page Supporting Classes

/// <summary>
/// Trust indicator for building user confidence
/// </summary>
public class TrustIndicator
{
    public string IconClass { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "success";
}

/// <summary>
/// Public system status for guest users
/// </summary>
public class PublicSystemStatus
{
    public bool IsOnline { get; set; } = true;
    public string Status { get; set; } = "Operational";
    public string Message { get; set; } = "All systems operational";
    public DateTime LastCheck { get; set; } = DateTime.UtcNow;
    public int ResponseTimeMs { get; set; }
}

/// <summary>
/// Security badge for trust building
/// </summary>
public class SecurityBadge
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = "fas fa-shield-alt";
    public string BadgeColor { get; set; } = "success";
    public bool IsVerified { get; set; } = true;
}

#endregion

#region Analytics Supporting Classes

/// <summary>
/// Trend analysis for analytics dashboard
/// </summary>
public class TrendAnalysis
{
    public string MetricName { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = "stable";
    public double ChangePercentage { get; set; }
    public string Period { get; set; } = "7days";
    public List<TrendDataPoint> DataPoints { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Individual data point for trend analysis
/// </summary>
public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

#endregion

#region Supporting Classes

/// <summary>
/// Feature highlight for landing page
/// </summary>
public class FeatureHighlight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
    public bool IsNew { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Support information for user assistance
/// </summary>
public class SupportInfo
{
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string HelpDeskHours { get; set; } = string.Empty;
    public List<FaqItem> FrequentlyAskedQuestions { get; set; } = new();
    public List<SupportChannel> SupportChannels { get; set; } = new();
}

/// <summary>
/// FAQ item for support
/// </summary>
public class FaqItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ViewCount { get; set; }
}

/// <summary>
/// Support channel information
/// </summary>
public class SupportChannel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactInfo { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string AvailabilityHours { get; set; } = string.Empty;
}

/// <summary>
/// OTAC help information
/// </summary>
public class OtacHelpInfo
{
    public string WhatIsOtac { get; set; } = "OTAC คือรหัสยืนยันตัวตนชั่วคราว 8 หลัก";
    public string HowToGetOtac { get; set; } = "ติดต่อธนาคารเพื่อขอรับรหัส OTAC";
    public string ValidityPeriod { get; set; } = "รหัส OTAC มีอายุ 30 นาที";
    public List<string> ImportantNotes { get; set; } = new();
    public string ContactInfo { get; set; } = string.Empty;
}

/// <summary>
/// Registration progress tracking
/// </summary>
public class RegistrationProgress
{
    public int CurrentStep { get; set; } = 1;
    public int TotalSteps { get; set; } = 3;
    public List<ProgressStep> Steps { get; set; } = new();
    public decimal PercentComplete { get; set; }
}

/// <summary>
/// Individual progress step
/// </summary>
public class ProgressStep
{
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, active, completed, error
    public string? Description { get; set; }
}

/// <summary>
/// Form validation status
/// </summary>
public class FormValidationStatus
{
    public bool IsValid { get; set; }
    public Dictionary<string, List<string>> FieldErrors { get; set; } = new();
    public List<string> GeneralErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int ValidationScore { get; set; } = 100;
}

/// <summary>
/// Form security information
/// </summary>
public class FormSecurityInfo
{
    public string SecurityLevel { get; set; } = "High";
    public bool IsEncrypted { get; set; } = true;
    public string CsrfToken { get; set; } = string.Empty;
    public List<SecurityIndicator> SecurityIndicators { get; set; } = new();
}

/// <summary>
/// Security indicator for forms
/// </summary>
public class SecurityIndicator
{
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
}

/// <summary>
/// Quick action for admin dashboard
/// </summary>
public class QuickAction
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
    public bool IsEnabled { get; set; } = true;
    public string? Permission { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Dashboard widget configuration
/// </summary>
public class DashboardWidget
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // chart, metric, table, custom
    public string Size { get; set; } = "medium"; // small, medium, large
    public bool IsVisible { get; set; } = true;
    public int Position { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// User permissions for UI customization
/// </summary>
public class UserPermissions
{
    public bool CanViewAnalytics { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanManageOtac { get; set; }
    public bool CanManageRegistrations { get; set; }
    public bool CanExportData { get; set; }
    public bool CanViewSystemHealth { get; set; }
    public bool CanManageSystem { get; set; }
    public List<string> Roles { get; set; } = new();
    public Dictionary<string, bool> FeatureFlags { get; set; } = new();
}

/// <summary>
/// Export option for analytics data
/// </summary>
public class ExportOption
{
    public string Format { get; set; } = string.Empty; // excel, csv, pdf
    public string DisplayName { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>
/// Analytics filters configuration
/// </summary>
public class AnalyticsFilters
{
    public string TimeRange { get; set; } = "24h";
    public List<string> SelectedBranches { get; set; } = new();
    public List<string> SelectedStatuses { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, object> CustomFilters { get; set; } = new();
}

#endregion

#region Supporting Dashboard Classes

/// <summary>
/// Real-time dashboard statistics
/// </summary>
public class DashboardRealTimeStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalOddRegistrations { get; set; }
    public int PendingOddRegistrations { get; set; }
    public int CompletedOddRegistrations { get; set; }
    public int TotalOtacCodes { get; set; }
    public int ActiveOtacCodes { get; set; }
    public double SuccessRate { get; set; }
    public double OtacUsageRate { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Recent activity item
/// </summary>
public class RecentActivityDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
}

/// <summary>
/// System alert message
/// </summary>
public class AlertMessage
{
    public int Id { get; set; }
    public string Type { get; set; } = "info"; // info, warning, error, success
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string Severity { get; set; } = "low"; // low, medium, high, critical
    public string ActionUrl { get; set; } = string.Empty;
}

/// <summary>
/// Performance metrics summary
/// </summary>
public class PerformanceMetrics
{
    public double ResponseTimeMs { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DatabaseLatencyMs { get; set; }
    public int RequestsPerMinute { get; set; }
    public int ErrorRate { get; set; }
    public DateTime LastMeasured { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealthStatus
{
    public bool IsHealthy { get; set; } = true;
    public string Status { get; set; } = "Healthy";
    public List<HealthCheckResult> HealthChecks { get; set; } = new();
    public DateTime LastCheck { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Chart data for analytics
/// </summary>
public class ChartData
{
    public string ChartType { get; set; } = "line";
    public List<string> Labels { get; set; } = new();
    public List<ChartDataset> Datasets { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Chart dataset
/// </summary>
public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public List<double> Data { get; set; } = new();
    public string BackgroundColor { get; set; } = "#007bff";
    public string BorderColor { get; set; } = "#007bff";
    public int BorderWidth { get; set; } = 2;
    public bool Fill { get; set; }
}

/// <summary>
/// KPI metrics summary
/// </summary>
public class KpiSummary
{
    public List<KpiMetric> Metrics { get; set; } = new();
    public string Period { get; set; } = "24h";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual KPI metric
/// </summary>
public class KpiMetric
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double PreviousValue { get; set; }
    public double ChangePercentage { get; set; }
    public string TrendDirection { get; set; } = "stable"; // up, down, stable
    public string Unit { get; set; } = string.Empty;
    public string Format { get; set; } = "number";
    public string Color { get; set; } = "primary";
}

#endregion

#region API Request/Response Models

/// <summary>
/// OTAC verification request for API
/// </summary>
public class VerifyOtacRequest
{
    [Required]
    public string OtacCode { get; set; } = string.Empty;
    
    public string? ClientIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Language { get; set; } = "th";
}

/// <summary>
/// OTAC verification response from API
/// </summary>
public class VerifyOtacResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AttemptsRemaining { get; set; }
    public bool IsLocked { get; set; }
    public TimeSpan? LockoutTimeRemaining { get; set; }
    public string? RedirectUrl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Registration submission request for API
/// </summary>
public class SubmitRegistrationRequest
{
    [Required]
    public string OtacCode { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string IdValue { get; set; } = string.Empty;

    [Required]
    public string MobileNo { get; set; } = string.Empty;

    [Required]
    public string AccountNo { get; set; } = string.Empty;

    [Required]
    public int BranchId { get; set; }

    public bool AcceptTerms { get; set; }
    public bool AcceptPrivacy { get; set; }
    public string Language { get; set; } = "th";
}

/// <summary>
/// Registration submission response from API
/// </summary>
public class SubmitRegistrationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TrackingId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

#endregion