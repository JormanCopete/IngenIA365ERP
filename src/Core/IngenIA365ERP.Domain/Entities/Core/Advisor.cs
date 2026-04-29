using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Advisors] (cop_asesores).
/// </summary>
public class Advisor : AuditableEntity
{
    [MaxLength(20)]
    public string? LegacyCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? City { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(120)]
    public string? Email { get; set; }
}
