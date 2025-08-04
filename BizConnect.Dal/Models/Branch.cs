using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Bank branch information for ODD registration management
/// </summary>
public partial class Branch
{
    /// <summary>
    /// Primary key, auto-incrementing branch identifier
    /// </summary>
    public int BranchId { get; set; }

    /// <summary>
    /// Human-readable branch name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Unique branch code for identification
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Physical address of the branch
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Whether the branch is currently active and accepting registrations
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Timestamp when branch was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when branch was last updated (auto-updated by trigger)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<KbankOddRegistration> KbankOddRegistrations { get; set; } = new List<KbankOddRegistration>();
}
