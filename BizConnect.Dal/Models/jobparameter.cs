using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Stores parameters for background jobs
/// </summary>
public partial class Jobparameter
{
    public long Id { get; set; }

    public long Jobid { get; set; }

    public string Name { get; set; } = null!;

    public string? Value { get; set; }

    public int Updatecount { get; set; }

    public virtual Job Job { get; set; } = null!;
}
