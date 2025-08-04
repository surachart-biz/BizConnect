using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class _lock
{
    public string resource { get; set; } = null!;

    public int updatecount { get; set; }

    public DateTime? acquired { get; set; }
}
