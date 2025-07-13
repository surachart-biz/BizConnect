using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Tracks applied database migration files
/// </summary>
public partial class _SchemaVersion
{
    public int Id { get; set; }

    /// <summary>
    /// Name of the migration file that was applied
    /// </summary>
    public string Filename { get; set; } = null!;

    /// <summary>
    /// Timestamp when the migration was applied
    /// </summary>
    public DateTime AppliedAt { get; set; }
}
