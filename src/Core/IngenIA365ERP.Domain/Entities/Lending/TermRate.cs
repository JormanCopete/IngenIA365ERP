using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_TermRates].</summary>
public class TermRate : AuditableEntity
{
    public int CreditLineId { get; set; }
    public decimal AmountStart { get; set; }
    public decimal AmountEnd { get; set; }
    public int TermStart { get; set; }
    public int TermEnd { get; set; }
    public int SeniorityStart { get; set; }
    public int SeniorityEnd { get; set; }
    public decimal Rate { get; set; }
    public DateTime UpdateDate { get; set; }
    public int MaxTerm { get; set; }
    public decimal MaxAmount { get; set; }
    [MaxLength(3)]
    public string GuaranteeType { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
