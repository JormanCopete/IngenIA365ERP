using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ExtraPayments] (cop_extras).</summary>
public class ExtraPayment : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int ExtraNumber { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(2)]
    public string PaymentForm { get; set; } = string.Empty;
    public DateOnly PaymentDate { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal ChargesAmount { get; set; }
    public decimal PaymentsAmount { get; set; }
    public int PaymentCycle { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(3)]
    public string ExtraType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
