using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class ActiveOddRegistration
{
    public int? Id { get; set; }

    public string? ExternalReference { get; set; }

    public string? RegId { get; set; }

    public string? EspaId { get; set; }

    public string? Status { get; set; }

    public string? StatusMessageTh { get; set; }

    public string? StatusMessageEn { get; set; }

    public string? ErrorMessageTh { get; set; }

    public string? ErrorMessageEn { get; set; }

    public string? IdType { get; set; }

    public string? IdValue { get; set; }

    public string? NationalId { get; set; }

    public string? FullName { get; set; }

    public string? MobileNo { get; set; }

    public string? MobileNumber { get; set; }

    public string? AccountNo { get; set; }

    public string? OtacCode { get; set; }

    public string? OtacState { get; set; }

    public int? AttemptCount { get; set; }

    public bool? IsLocked { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? BranchId { get; set; }

    public string? BranchCode { get; set; }

    public string? BranchName { get; set; }

    public string? BranchNameTh { get; set; }

    public string? BranchNameEn { get; set; }

    public string? GeneratedByUsername { get; set; }
}
