using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ExtraPaymentBalances].</summary>
public class ExtraPaymentBalance : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int ExtraNumber { get; set; }
    public int Period { get; set; }
    public decimal Balance { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
