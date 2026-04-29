using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ContributionReductionParams].</summary>
public class ContributionReductionParam : AuditableEntity
{
    public int CutoffPeriod { get; set; }
    public int AccumulationConcept { get; set; }
    public decimal ExcessAmount { get; set; }
    [MaxLength(60)]
    public string SignatoryName { get; set; } = string.Empty;
    [MaxLength(40)]
    public string Position { get; set; } = string.Empty;
    public string MemoDetail { get; set; } = string.Empty;
    [MaxLength(5)]
    public string ReductionVoucher { get; set; } = string.Empty;
    public int WithholdingAccumConcept { get; set; }
}
