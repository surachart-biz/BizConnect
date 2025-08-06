using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VRealtimeDashboardStat
{
    public long? PendingRegistrations { get; set; }

    public long? ApprovedRegistrations { get; set; }

    public long? RejectedRegistrations { get; set; }

    public long? ActiveOtacCodes { get; set; }

    public long? ValidatedOtacCodes { get; set; }

    public long? UsedOtacCodes { get; set; }

    public long? RegistrationsToday { get; set; }

    public long? RegistrationsWeek { get; set; }

    public long? RegistrationsMonth { get; set; }

    public long? ActiveUsers { get; set; }

    public long? UsersOnlineToday { get; set; }

    public long? UsersActiveWeek { get; set; }

    public decimal? AvgOtacAttempts { get; set; }

    public int? MaxOtacAttempts { get; set; }

    public decimal? OverallSuccessRate { get; set; }

    public DateTime? SnapshotTime { get; set; }
}
