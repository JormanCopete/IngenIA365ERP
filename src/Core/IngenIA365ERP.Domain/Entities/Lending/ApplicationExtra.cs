using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ApplicationExtras] (cop_extrasoli).</summary>
public class ApplicationExtra : AuditableEntityLong
{
    public int ApplicationNumber { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(2)]
    public string PaymentForm { get; set; } = string.Empty;
    [MaxLength(3)]
    public string ExtraType { get; set; } = string.Empty;
}
