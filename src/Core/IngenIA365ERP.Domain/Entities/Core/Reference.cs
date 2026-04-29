using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_References] (sys_referencia).
/// Personal and commercial references for a person.
/// </summary>
public class Reference : AuditableEntity
{
    public int PersonId { get; set; }

    [MaxLength(2)]
    public string ReferenceType { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public int? CityId { get; set; }

    [MaxLength(120)]
    public string? ContactName { get; set; }

    [MaxLength(2)]
    public string? ProductType { get; set; }

    public int? ProductNumber { get; set; }

    [MaxLength(40)]
    public string? Mobile { get; set; }

    [MaxLength(10)]
    public string? RelationshipCode { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
    public City? City { get; set; }
}
