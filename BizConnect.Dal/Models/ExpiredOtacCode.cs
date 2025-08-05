using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class ExpiredOtacCode
{
    public int? Id { get; set; }

    public string? ExternalReference { get; set; }

    public string? OtacCode { get; set; }

    public string? OtacState { get; set; }

    public string? Status { get; set; }

    public string? StatusMessageTh { get; set; }

    public string? StatusMessageEn { get; set; }

    public string? ErrorMessageTh { get; set; }

    public string? ErrorMessageEn { get; set; }

    public DateTime? OtacExpiresAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? BranchId { get; set; }

    public string? BranchCode { get; set; }

    public string? BranchName { get; set; }

    public string? BranchNameTh { get; set; }

    public string? BranchNameEn { get; set; }

    public string? BranchAddress { get; set; }

    public string? BranchAddressTh { get; set; }

    public string? BranchAddressEn { get; set; }

    public string? GeneratedByUsername { get; set; }

    public decimal? MinutesExpired { get; set; }

    public string? ExpiryCategory { get; set; }
}
