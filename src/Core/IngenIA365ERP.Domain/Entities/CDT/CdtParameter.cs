using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_Parameters] (cdt_parame58).</summary>
public class CdtParameter : AuditableEntity
{
    public int CreditLineId { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }

    public decimal? MinimumRate { get; set; }
    public decimal? AnnualRate { get; set; }
    public decimal? WithholdingRate { get; set; }
    public decimal? MinWithholdingAmount { get; set; }
    public int? InterestConceptId { get; set; }
    public int? WithholdingConceptId { get; set; }
    public decimal? MonthlyIncrement { get; set; }
    public decimal? InterestRate { get; set; }
    public int? Term { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int InterestPaymentType { get; set; }

    [MaxLength(2)]
    public string InterestType { get; set; } = "S";

    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int SourceId { get; set; }

    [MaxLength(15)]
    public string? TreasuryAccount { get; set; }
}
