using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Stores sorted sets for Hangfire operations
/// </summary>
public partial class Set
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public decimal Score { get; set; }

    public string Value { get; set; } = null!;

    public DateTime? Expireat { get; set; }

    public int Updatecount { get; set; }
}
