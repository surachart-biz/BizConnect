using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{
    /// <summary>
    /// Trend analysis result for performance monitoring
    /// </summary>
    public class TrendAnalysis
    {
        public string MetricType { get; set; } = string.Empty;
        public TimeSpan TimeWindow { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal PreviousValue { get; set; }
        public decimal ChangePercent { get; set; }
        public string TrendDirection { get; set; } = "Stable"; // Improving, Declining, Stable
        public List<TrendDataPoint> DataPoints { get; set; } = new();
        public string? Recommendation { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Public system status for guest users
    /// </summary>
    public class PublicSystemStatus
    {
        public string Status { get; set; } = "Unknown"; // Operational, Maintenance, Limited, Offline
        public string Message { get; set; } = string.Empty;
        public bool IsOtacGenerationAvailable { get; set; }
        public bool IsRegistrationAvailable { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public List<ServiceStatus> Services { get; set; } = new();
    }

    /// <summary>
    /// Individual service status
    /// </summary>
    public class ServiceStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
        public string? Description { get; set; }
    }

    /// <summary>
    /// Trust indicator for guest confidence
    /// </summary>
    public class TrustIndicator
    {
        public string Type { get; set; } = string.Empty; // Security, Reliability, Performance, Certification
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "primary";
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Security badge for trust display
    /// </summary>
    public class SecurityBadge
    {
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VerificationUrl { get; set; } = string.Empty;
        public DateTime IssuedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
    }
}