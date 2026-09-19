using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Branches] (sys_agencia).
/// </summary>
public class Branch : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    // Feature 009 (R7): vinculo con la oficina registrada en ADM_Branches (por PublicId, sin FK entre bases)
    // para traducir las sucursales asignadas a un usuario al alcance contable.
    public Guid? TenantBranchPublicId { get; set; }
}
