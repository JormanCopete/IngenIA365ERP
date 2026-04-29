using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Countries] (sys_paises).
/// Simple country catalog.
/// </summary>
public class Country : AuditableEntity
{
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Department> Departments { get; set; } = [];
}
