using System;
using System.Collections.Generic;

namespace BizConnect.Services.DTOs
{

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