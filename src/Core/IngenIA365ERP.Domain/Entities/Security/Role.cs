using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>Maps to [dbo].[SEC_Roles].</summary>
public class Role : AuditableEntity
{
    /// <summary>
    /// Código corto inmutable, usado como identidad de máquina (claims, gates).
    /// Único por tenant. Para built-ins: CompanyAdmin, Auditor, Operator, ReadOnly.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Null = rol SaaS-global (operador del producto). Distinto null = scope tenant.
    /// </summary>
    public int? TenantId { get; set; }

    /// <summary>
    /// Rol provisto por el sistema. <c>CompanyAdmin</c> y <c>Auditor</c> built-in
    /// no se pueden eliminar (regla del dominio aplicada en DeleteRoleCommand).
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// false = rol del sistema interno no asignable a usuarios (p. ej. System.Background).
    /// </summary>
    public bool IsAssignable { get; set; } = true;

    /// <summary>Compatibilidad con DDL existente — equivalente a IsBuiltIn para legacy.</summary>
    public bool IsSystemRole { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<User> Users { get; set; } = [];
}
