using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Vincula un <see cref="User"/> con una <see cref="Branch"/> específica de
/// una <see cref="Tenant"/>. Mapea a <c>SEC_UserBranchAssignments</c>.
/// El claim <c>branch_id</c> emitido por el JWT al hacer login se toma de
/// la sucursal por defecto del usuario en la cooperativa activa.
/// </summary>
public class UserBranchAssignment : AuditableEntity
{
    public int UserId { get; set; }
    public int TenantId { get; set; }
    public int BranchId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
    public TenantBranch? Branch { get; set; }
}
