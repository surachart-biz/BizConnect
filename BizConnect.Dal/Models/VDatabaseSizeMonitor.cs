using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VDatabaseSizeMonitor
{
    public string? Size { get; set; }

    public long? SizeBytes { get; set; }
}
