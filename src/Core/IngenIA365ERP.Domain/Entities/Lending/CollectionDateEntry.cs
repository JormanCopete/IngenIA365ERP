using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionDateEntries] (cop_novfecgestion).</summary>
public class CollectionDateEntry : AuditableEntityLong
{
    public long CollectionCaseId { get; set; }
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly PromiseDate { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
