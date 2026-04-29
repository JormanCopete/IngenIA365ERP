using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ScoringParameters].</summary>
public class ScoringParameter : AuditableEntity
{
    [MaxLength(2)]
    public string CriterionCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string SubItemCode { get; set; } = string.Empty;
    [MaxLength(120)]
    public string CriterionName { get; set; } = string.Empty;
    [MaxLength(120)]
    public string SubItemName { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
}
