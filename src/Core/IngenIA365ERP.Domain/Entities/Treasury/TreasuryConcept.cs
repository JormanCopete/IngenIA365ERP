using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury;

/// <summary>Maps to [dbo].[TRS_Concepts] (TES_CPTOS).</summary>
public class TreasuryConcept : AuditableEntity
{
    [MaxLength(5)]
    public string ConceptCode { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ShortName { get; set; }

    [MaxLength(5)]
    public string? ConceptType { get; set; }
}
