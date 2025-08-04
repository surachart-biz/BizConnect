using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Tracks state changes for background jobs
/// </summary>
public partial class Jobstate
{
    public long Id { get; set; }

    public long Jobid { get; set; }

    public string Name { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime Createdat { get; set; }

    public string? Data { get; set; }

    public virtual Job Job { get; set; } = null!;

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
