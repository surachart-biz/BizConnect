using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VOtacTrend
{
    public DateOnly? Date { get; set; }

    public long? TotalGenerated { get; set; }

    public long? TotalValidated { get; set; }

    public long? TotalUsed { get; set; }

    public long? TotalExpired { get; set; }

    public decimal? AvgAttempts { get; set; }

    public int? MaxAttempts { get; set; }

    public long? LockedCodes { get; set; }
}
