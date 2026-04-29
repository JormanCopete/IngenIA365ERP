using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AuxAppInstallments] (cop_solauxcuota).</summary>
public class AuxAppInstallment : AuditableEntityLong
{
    public int ApplicationId { get; set; }
    public int InstallmentNumber { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(5)]
    public string LineCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? IsGenerated { get; set; }
    public DateOnly? DeathDate { get; set; }
}
