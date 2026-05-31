using Microsoft.AspNetCore.Identity;

namespace IngenIA365ERP.Persistence.Identity;

/// <summary>
/// Bridge entre ASP.NET Core Identity y la entidad de dominio
/// <c>IngenIA365ERP.Domain.Entities.Admin.CentralUser</c>.
///
/// <para>
/// La clase hereda <see cref="IdentityUser{TKey}"/> con <c>TKey = Guid</c> y se
/// almacena en la tabla <c>ADM_CentralUsers</c>. Los campos custom (
/// <see cref="DefaultTenantId"/>, <see cref="IsGlobalMasterAdmin"/>,
/// <see cref="Status"/>, etc.) viven aquí porque ASP.NET Identity requiere que
/// todas las columnas del usuario estén en una sola fila — no se puede dividir
/// el entity sin re-implementar IUserStore.
/// </para>
///
/// <para>
/// El POCO <c>Domain.Entities.Admin.CentralUser</c> es el espejo "lado dominio"
/// que la capa Application consume vía <c>ICentralIdentityProvider</c> (Chunk C);
/// un Store custom (Chunk C) convierte entre ambos. Esto cumple Clean Architecture
/// (Domain no referencia ASP.NET Identity) sin duplicar persistencia.
/// </para>
///
/// <para>
/// <b>Convención de defaults</b>: el constructor inicializa <see cref="IdentityUser{TKey}.SecurityStamp"/>
/// y <see cref="IdentityUser{TKey}.ConcurrencyStamp"/> con GUIDs nuevos para evitar
/// errores en escrituras directas (no vía <c>UserManager</c>) que de otro modo
/// dejarían esos campos null y romperían operaciones siguientes.
/// </para>
/// </summary>
public class CentralUserIdentity : IdentityUser<Guid>
{
    public CentralUserIdentity()
    {
        Id = Guid.NewGuid();
        SecurityStamp = Guid.NewGuid().ToString("N");
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }

    /// <summary>Secreto TOTP base32 cifrado en BD con IDataProtectionProvider. NULL si MFA no configurado.</summary>
    public string? MfaSecret { get; set; }

    /// <summary>Empresa por defecto al iniciar sesión (FR-016).</summary>
    public Guid? DefaultTenantId { get; set; }

    /// <summary>Administrador master del producto (FR-037 a FR-039).</summary>
    public bool IsGlobalMasterAdmin { get; set; }

    /// <summary>0=Active, 1=Pending, 2=Disabled (alineado con <c>CentralUserStatus</c> de Domain).</summary>
    public int Status { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // Audit + soft-delete (manuales — CentralUser no hereda AuditableEntity por restricción de Identity).
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
