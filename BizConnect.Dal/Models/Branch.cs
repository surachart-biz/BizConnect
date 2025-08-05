using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Bank branch information with multi-language support for ODD registration management
/// </summary>
public partial class Branch
{
    /// <summary>
    /// Primary key, auto-incrementing branch identifier
    /// </summary>
    public int BranchId { get; set; }

    /// <summary>
    /// Default branch name (fallback)
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Branch name in Thai language
    /// </summary>
    public string? NameTh { get; set; }

    /// <summary>
    /// Branch name in English language
    /// </summary>
    public string? NameEn { get; set; }

    /// <summary>
    /// Unique branch code for identification
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Default physical address (fallback)
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Physical address in Thai language
    /// </summary>
    public string? AddressTh { get; set; }

    /// <summary>
    /// Physical address in English language
    /// </summary>
    public string? AddressEn { get; set; }

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
