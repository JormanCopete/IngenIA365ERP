using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Cities] (sys_ciudad57).
/// </summary>
public class City : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    public int DepartmentId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public Department Department { get; set; } = null!;
    public ICollection<Person> People { get; set; } = [];
    public ICollection<Beneficiary> Beneficiaries { get; set; } = [];
    public ICollection<Reference> References { get; set; } = [];
}
