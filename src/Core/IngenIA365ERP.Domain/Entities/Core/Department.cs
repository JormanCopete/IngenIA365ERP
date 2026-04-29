using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Departments] (nueva — no existe tabla legacy).
/// Departamentos/Estados/Provincias.
/// </summary>
public class Department : AuditableEntity
{
    public int CountryId { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public Country Country { get; set; } = null!;
    public ICollection<City> Cities { get; set; } = [];
}
