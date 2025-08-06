using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VQueryPerformance
{
    public string? ViewName { get; set; }

    public string? ObjectName { get; set; }

    public long? RecordCount { get; set; }

    public DateTime? LastChecked { get; set; }
}
