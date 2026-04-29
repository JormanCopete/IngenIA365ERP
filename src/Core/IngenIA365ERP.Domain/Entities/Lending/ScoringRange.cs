using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ScoringRanges].</summary>
public class ScoringRange : AuditableEntity
{
    [MaxLength(2)]
    public string CriterionCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string SubItemCode { get; set; } = string.Empty;
    [MaxLength(40)]
    public string RangeStart { get; set; } = string.Empty;
    [MaxLength(40)]
    public string RangeEnd { get; set; } = string.Empty;
    public int ScoreValue { get; set; }
    [MaxLength(3)]
    public string Equality { get; set; } = string.Empty;
}
