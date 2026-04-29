using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Diseases] (sys_enfermedades).
/// </summary>
public class Disease : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
