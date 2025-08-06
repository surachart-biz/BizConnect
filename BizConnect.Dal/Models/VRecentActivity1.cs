using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class VRecentActivity1
{
    public int? Id { get; set; }

    public string? ExternalReference { get; set; }

    public string? OtacCode { get; set; }

    public string? OtacState { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? BranchName { get; set; }

    public string? CreatedBy { get; set; }
}
