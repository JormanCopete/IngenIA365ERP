using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_LoanRestructurings].</summary>
public class LoanRestructuring : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(20)]
    public string OriginalPersonCode { get; set; } = string.Empty;
    public int OriginalCreditLineId { get; set; }
    public long OriginalPortfolioNumber { get; set; }
    [MaxLength(2)]
    public string Category { get; set; } = string.Empty;
    public DateOnly? RestructureDate { get; set; }
    public decimal? Amount { get; set; }
    public DateOnly? SystemDate { get; set; }
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;
    public int TimesRestructured { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
