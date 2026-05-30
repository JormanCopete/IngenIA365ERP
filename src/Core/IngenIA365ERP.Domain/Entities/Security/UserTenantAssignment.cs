using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Vincula un <see cref="User"/> con un <see cref="Tenant"/> al que puede
/// operar. Mapea a <c>SEC_UserTenantAssignments</c>. Para usuarios marcados
/// como <c>IsSaasOperator</c>, esta tabla controla a qué cooperativas
/// pueden cambiarse desde el selector multi-empresa.
/// </summary>
public class UserTenantAssignment : AuditableEntity
{
    public int UserId { get; set; }
    public int TenantId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
}
