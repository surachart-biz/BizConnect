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

    public string? ActivityType { get; set; }

    public int? PrioritySort { get; set; }

    /// <summary>
    /// Computed property that returns the appropriate branch name
    /// Falls back to English if Thai is not available, then to branch code
    /// </summary>
    public string? BranchName => !string.IsNullOrWhiteSpace(BranchNameEn) ? BranchNameEn : 
                                 !string.IsNullOrWhiteSpace(BranchNameTh) ? BranchNameTh : 
                                 BranchCode;
}
