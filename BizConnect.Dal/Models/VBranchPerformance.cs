using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VBranchPerformance
{
    public int? BranchId { get; set; }

    public string? BranchCode { get; set; }

    public string? BranchNameEn { get; set; }

    public string? BranchNameTh { get; set; }

    public bool? IsActive { get; set; }

    public long? TotalRegistrations { get; set; }

    public long? SuccessfulRegistrations { get; set; }

    public long? FailedRegistrations { get; set; }

    public long? PendingRegistrations { get; set; }

    public decimal? SuccessRate { get; set; }

    public long? TodayCount { get; set; }

    public long? WeekCount { get; set; }

    public long? MonthCount { get; set; }

    public decimal? AvgOtacAttempts { get; set; }

    public long? LockedOtacCount { get; set; }
}
