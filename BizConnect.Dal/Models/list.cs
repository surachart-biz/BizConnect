using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Stores list data for Hangfire operations
/// </summary>
public partial class List
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public DateTime? Expireat { get; set; }

    public int Updatecount { get; set; }
}
