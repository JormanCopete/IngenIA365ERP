using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AuxAppInstallmentBeneficiaries] (cop_solauxcuotabenef).</summary>
public class AuxAppInstallmentBeneficiary : AuditableEntityLong
{
    public int ApplicationId { get; set; }
    public int InstallmentNumber { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryCode { get; set; } = string.Empty;
    public DateOnly PaymentDate { get; set; }
    [MaxLength(5)]
    public string LineCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [MaxLength(2)]
    public string? Status { get; set; }
}
