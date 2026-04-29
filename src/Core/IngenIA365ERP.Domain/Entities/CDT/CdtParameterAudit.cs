using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_ParameterAudit] (cdt_paramaud).</summary>
public class CdtParameterAudit : AuditableEntityLong
{
    [MaxLength(1)]
    public string Action { get; set; } = string.Empty;

    public DateTime ActionDate { get; set; }

    [MaxLength(50)]
    public string? UserName { get; set; }

    public int? CreditLineId { get; set; }

    [MaxLength(100)]
    public string? DescriptionOld { get; set; }

    [MaxLength(100)]
    public string? DescriptionNew { get; set; }

    public decimal? MinRateOld { get; set; }
    public decimal? MinRateNew { get; set; }
    public decimal? AnnualRateOld { get; set; }
    public decimal? AnnualRateNew { get; set; }
    public decimal? WithholdingRateOld { get; set; }
    public decimal? WithholdingRateNew { get; set; }
    public decimal? MinWithholdingAmtOld { get; set; }
    public decimal? MinWithholdingAmtNew { get; set; }
    public int? PaymentMethodOld { get; set; }
    public int? PaymentMethodNew { get; set; }
    public int? InterestConceptOld { get; set; }
    public int? InterestConceptNew { get; set; }
    public int? WithholdingConceptOld { get; set; }
    public int? WithholdingConceptNew { get; set; }
    public decimal? MonthlyIncrementOld { get; set; }
    public decimal? MonthlyIncrementNew { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
