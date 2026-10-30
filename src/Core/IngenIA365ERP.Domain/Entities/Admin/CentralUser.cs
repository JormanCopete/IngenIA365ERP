using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Identidad central única (FR-001 a FR-006). Maps to [dbo].[ADM_CentralUsers].
/// <para>
/// <b>Excepción justificada al principio constitucional VI</b>: usa <see cref="Id"/> <see cref="Guid"/>
/// como PK única (no patrón "int Id + Guid PublicId") por convención de ASP.NET Core Identity
/// (<c>IdentityUser&lt;Guid&gt;</c>). Documentado en <c>specs/002-identidad-central-federada/plan.md</c>
/// (Complexity Tracking). El Guid no es secuencial → cumple el objetivo anti-enumeración.
/// </para>
/// <para>
/// No hereda <see cref="AuditableEntity"/> (que asume int Id). Los campos de auditoría y soft-delete
/// se declaran manualmente para conservar la misma semántica del principio VII.
/// </para>
/// </summary>
public class CentralUser
{
    public Guid Id { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Email en UPPER-invariant para lookup case-insensitive (FR-002).</summary>
    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(64)]
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    public bool TwoFactorEnabled { get; set; }

    /// <summary>Secreto TOTP (base32) cifrado en BD por <c>IDataProtectionProvider</c>.</summary>
    [MaxLength(512)]
    public string? MfaSecret { get; set; }

    /// <summary>
    /// Lockout interno de ASP.NET Identity. Deshabilitado (ver T043 / research D-11);
    /// el lockout progresivo se gestiona desde <c>LoginAttemptCounter</c> (Redis).
    /// </summary>
    public DateTime? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }

    /// <summary>Empresa por defecto al iniciar sesión (FR-016, FR-017).</summary>
    public Guid? DefaultTenantId { get; set; }

    /// <summary>Administrador master del producto (FR-037 a FR-039).</summary>
    public bool IsGlobalMasterAdmin { get; set; }

    public CentralUserStatus Status { get; set; } = CentralUserStatus.Active;

    public DateTime CreatedAt { get; set; }
    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }
    [MaxLength(256)]
    public string? UpdatedBy { get; set; }

    /// <summary>Soft-delete (principio VII).</summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [MaxLength(256)]
    public string? DeletedBy { get; set; }

    public DateTime? LastLoginAt { get; set; }

    // ── Feature 017: identidad anclada al documento (D-01, research R1) ────────

    /// <summary>Código de acceso del documento (CC, CE, PPT, PEP, TI, PA). Nulo si aún no lo registra.</summary>
    [MaxLength(3)]
    public string? DocumentType { get; set; }

    /// <summary>Número normalizado (sólo <c>[0-9A-Z]</c>).</summary>
    [MaxLength(20)]
    public string? DocumentNumber { get; set; }

    /// <summary>
    /// Usuario derivado del documento: con él se entra sin correo. Lo escribe sólo
    /// <see cref="RegistrarDocumento"/>; nunca se digita.
    /// </summary>
    [MaxLength(24)]
    public string? DocumentUsername { get; set; }

    [MaxLength(60)] public string? FirstName { get; set; }
    [MaxLength(60)] public string? OtherNames { get; set; }
    [MaxLength(60)] public string? LastName { get; set; }
    [MaxLength(60)] public string? SecondLastName { get; set; }

    /// <summary>Celular; en la tabla es la columna <c>PhoneNumber</c> de Identity.</summary>
    [MaxLength(30)]
    public string? MobilePhone { get; set; }

    /// <summary>Cuándo se verificó el correo. La regla del ingreso lee <see cref="EmailConfirmed"/>; esto es auditoría.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>
    /// Fija el documento ya validado y deriva el usuario (feature 017). Es el único que escribe
    /// <see cref="DocumentUsername"/>.
    /// </summary>
    public void RegistrarDocumento(IngenIA365ERP.Domain.Common.Identidad.DocumentoValidado documento)
    {
        DocumentType = documento.Tipo.Codigo;
        DocumentNumber = documento.Numero;
        DocumentUsername = documento.Usuario;
    }

    /// <summary>Nombre para mostrar: nombres y apellidos si los hay, si no el correo.</summary>
    public string NombreParaMostrar =>
        string.Join(' ', new[] { FirstName, OtherNames, LastName, SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } n
            ? n
            : Email;

    // No EF navigation collection: ASP.NET Identity persiste el bridge
    // CentralUserIdentity (Infrastructure), no este POCO de Domain.
    // Las membresías se cargan vía ITenantMembershipReader (Application).
}
