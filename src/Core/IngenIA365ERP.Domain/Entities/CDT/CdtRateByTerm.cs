using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_RatesByTerm] (cdt_tasasplazos).</summary>
public class CdtRateByTerm : AuditableEntity
{
    public int CreditLineId { get; set; }
    public decimal AmountRangeStart { get; set; }
    public decimal AmountRangeEnd { get; set; }
    public int TermStart { get; set; }
    public int TermEnd { get; set; }
    public decimal? InterestRate { get; set; }
    public DateTime? LastUpdated { get; set; }
}
