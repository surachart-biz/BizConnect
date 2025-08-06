using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class vw_OtacLifecycleStat
{
    public string? OtacState { get; set; }

    public long? RecordCount { get; set; }

    public DateTime? OldestRecord { get; set; }

    public DateTime? NewestRecord { get; set; }

    public string? StateDescription { get; set; }
}
