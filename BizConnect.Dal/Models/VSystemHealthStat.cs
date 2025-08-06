using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VSystemHealthStat
{
    public long? DatabaseSizeBytes { get; set; }

    public long? ActiveConnections { get; set; }

    public long? RegistrationsPerHour { get; set; }

    public long? RegistrationsToday { get; set; }

    public decimal? AvgProcessingTimeSeconds { get; set; }

    public long? ExpiredOtacCount { get; set; }

    public decimal? TodaysErrorRate { get; set; }

    public DateTime? SnapshotTime { get; set; }
}
