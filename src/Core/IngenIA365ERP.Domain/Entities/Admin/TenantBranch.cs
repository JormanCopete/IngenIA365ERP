using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Sucursal/agencia de una cooperativa multi-tenant. Mapea a
/// <c>ADM_Branches</c>. Una cooperativa siempre tiene exactamente una
/// sucursal con <see cref="IsHeadquarters"/> = true (la matriz). El par
/// <c>(TenantId, Code)</c> es único.
///
/// Nombre <c>TenantBranch</c> (en lugar del más natural <c>Branch</c>)
/// para evitar colisión con la entidad legacy <c>Core.Branch</c>
/// (<c>COR_Branches</c>, ex <c>sys_agencia</c>) ya presente en el modelo.
/// </summary>
public class TenantBranch : AuditableEntity
{
    public int TenantId { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public bool IsHeadquarters { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
