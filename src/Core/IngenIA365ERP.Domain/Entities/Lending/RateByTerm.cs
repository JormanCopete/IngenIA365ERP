using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_RatesByTerm].</summary>
public class RateByTerm : AuditableEntity
{
    public int CreditLineId { get; set; }
    public int TermStart { get; set; }
    public int TermEnd { get; set; }
    [MaxLength(2)]
    public string DiscountType { get; set; } = string.Empty;
    public decimal RateValue { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
