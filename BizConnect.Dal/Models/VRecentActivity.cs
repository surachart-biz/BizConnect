using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VRecentActivity
{
    public int? Id { get; set; }

    public string? ExternalReference { get; set; }

    public string? OtacCode { get; set; }

    public string? Status { get; set; }

    public string? OtacState { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? AttemptCount { get; set; }

    public string? GeneratedByUsername { get; set; }

    public string? BranchNameEn { get; set; }

    public string? BranchNameTh { get; set; }

    public string? BranchCode { get; set; }

    /// <summary>
    /// Computed branch name column that prefers English (NameEn), falls back to Thai (NameTh), then &quot;Unknown&quot;. 
    /// Added for DashboardService.cs compatibility.
    /// </summary>
    public string? BranchName { get; set; }

    public string? ActivityType { get; set; }

    public int? PrioritySort { get; set; }
}
