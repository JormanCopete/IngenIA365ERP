using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AuxiliaryApplications] (cop_solaux).</summary>
public class AuxiliaryApplication : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string LineCode { get; set; } = string.Empty;
    public DateOnly ApplicationDate { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    public DateOnly? PaymentDate { get; set; }
    [MaxLength(2)]
    public string IsApproved { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string? LineRemarks { get; set; }
    public string? ApprovalRemarks { get; set; }
    public string? ApplicationRemarks { get; set; }
    [MaxLength(2)]
    public string IsClosed { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId { get; set; } = string.Empty;
    public int? LegacyIdSolAux { get; set; }
}
