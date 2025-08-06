using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class MvMonthlyStatistic
{
    public DateOnly? Month { get; set; }

    public long? TotalRegistrations { get; set; }

    public long? UniqueUsers { get; set; }

    public long? BranchesUsed { get; set; }

    public long? ApprovedCount { get; set; }

    public long? RejectedCount { get; set; }

    public long? PendingCount { get; set; }

    public decimal? AvgOtacAttempts { get; set; }

    public int? MaxOtacAttempts { get; set; }

    public long? LockedOtacCount { get; set; }

    public decimal? SuccessRate { get; set; }
}
