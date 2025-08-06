using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VDashboardStat
{
    public long? TodayTotal { get; set; }

    public long? TodaySuccess { get; set; }

    public long? TodayFailed { get; set; }

    public long? MonthTotal { get; set; }

    public long? MonthSuccess { get; set; }

    public long? OtacGenerated { get; set; }

    public long? OtacValidated { get; set; }

    public long? OtacUsed { get; set; }

    public long? ActiveOtac { get; set; }
}
