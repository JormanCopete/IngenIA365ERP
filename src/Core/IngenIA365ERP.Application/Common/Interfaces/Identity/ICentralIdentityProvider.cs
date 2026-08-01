using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Abstracción del proveedor de identidad central (FR-001 a FR-006). Implementación
/// inicial: <c>AspNetCoreIdentityProvider</c> envolviendo <c>UserManager&lt;CentralUserIdentity&gt;</c>;
/// sustituible en el futuro por un adaptador a Entra External ID sin tocar Application.
///
/// <para>
/// Cumple Clean Architecture: ningún handler invoca <c>UserManager</c> directamente.
/// Toda lógica de credenciales pasa por esta interfaz. Domain ni conoce la existencia
/// de ASP.NET Identity.
/// </para>
/// </summary>
public interface ICentralIdentityProvider
{
    /// <summary>Busca por correo (case-insensitive). Devuelve null si no existe o está eliminado.</summary>
    Task<CentralUser?> FindByEmailAsync(string email, CancellationToken ct);

    /// <summary>Busca por Id. Devuelve null si no existe.</summary>
    Task<CentralUser?> FindByIdAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// Resuelve en lote el email de un conjunto de usuarios (listados, p.ej.
    /// miembros de un tenant) en una sola consulta. Ids inexistentes o
    /// eliminados simplemente no aparecen en el diccionario resultante.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsByIdsAsync(
        IReadOnlyCollection<Guid> centralUserIds,
        CancellationToken ct);

    /// <summary>
    /// Crea una identidad nueva con email + password (rama "usuario nuevo" en
    /// <c>AcceptInvitationCommand</c>). Internamente:
    /// (1) verifica password contra <see cref="IPwnedPasswordService"/>;
    /// (2) hashea con <c>BcryptPasswordHasher</c> (cost 11);
    /// (3) persiste en <c>ADM_CentralUsers</c>.
    /// </summary>
    Task<CreateCentralUserResult> CreateUserAsync(
        string email,
        string password,
        bool emailConfirmed,
        CancellationToken ct);

    /// <summary>Valida un password en texto plano contra el hash del usuario.
    /// Retorna <c>true</c> si coincide y la cuenta no está bloqueada/deshabilitada.</summary>
    Task<bool> ValidatePasswordAsync(Guid centralUserId, string password, CancellationToken ct);

    /// <summary>Cambia el password del usuario (post-validación de currentPassword).
    /// Regenera SecurityStamp → invalida refresh tokens existentes.</summary>
    Task<ChangePasswordResult> ChangePasswordAsync(
        Guid centralUserId,
        string currentPassword,
        string newPassword,
        CancellationToken ct);

    /// <summary>Reset administrativo (master admin). NO requiere currentPassword.
    /// Genera nuevo SecurityStamp → invalida todos los refresh tokens.</summary>
    Task<ChangePasswordResult> AdminResetPasswordAsync(
        Guid centralUserId,
        string newPassword,
        CancellationToken ct);

    // -------------------- MFA --------------------

    /// <summary>Genera secret TOTP + recovery codes para configuración pendiente
    /// (no se persiste el secret hasta <see cref="ConfirmMfaSetupAsync"/>).</summary>
    Task<MfaEnrollmentSetup> BeginMfaEnrollmentAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Verifica un TOTP contra un secret pendiente. Si OK, persiste
    /// <c>TwoFactorEnabled = true</c>, almacena <c>MfaSecret</c> cifrado y
    /// genera los recovery codes en <c>ADM_CentralUserTokens</c>.</summary>
    Task<MfaConfirmResult> ConfirmMfaSetupAsync(
        Guid centralUserId,
        string base32Secret,
        string code,
        CancellationToken ct);

    /// <summary>Verifica un TOTP contra el secret YA persistido del usuario (login con MFA).</summary>
    Task<bool> VerifyMfaCodeAsync(Guid centralUserId, string code, CancellationToken ct);

    /// <summary>Desactiva MFA del usuario. La validación de "ninguna política de
    /// tenant lo exige" es responsabilidad del caller (handler de DisableMfaCommand).</summary>
    Task DisableMfaAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Reset administrativo de MFA (master admin, FR procedimiento operativo
    /// del spec). Limpia secret + recovery codes + flag; el siguiente login con
    /// política de tenant activa forzará re-enrollment.</summary>
    Task ResetMfaAsync(Guid centralUserId, CancellationToken ct);

    // -------------------- Telemetría --------------------

    /// <summary>Marca un login exitoso (actualiza LastLoginAt) — separado del
    /// registro append-only en <c>ADM_CentralUserLoginAttempts</c>.</summary>
    Task RecordSuccessfulLoginAsync(Guid centralUserId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Actualiza <c>DefaultTenantId</c> del usuario. Pasa <c>null</c> para limpiar
    /// la preferencia (FR-016 — invalidación silenciosa cuando la membresía con
    /// la empresa por defecto ya no es Active, evitando preferencias zombi en BD).
    /// </summary>
    Task SetDefaultTenantAsync(Guid centralUserId, Guid? defaultTenantPublicId, CancellationToken ct);
}

// Result records — outcomes tipados para que el handler decida qué responder al cliente
// sin propagar excepciones del provider.

public sealed record CreateCentralUserResult(
    bool Succeeded,
    Guid? CentralUserId,
    IReadOnlyList<string> ErrorCodes);

public sealed record ChangePasswordResult(
    bool Succeeded,
    IReadOnlyList<string> ErrorCodes);

public sealed record MfaEnrollmentSetup(
    string SecretBase32,
    string OtpAuthUri,
    IReadOnlyList<string> RecoveryCodes,
    int ExpiresInSeconds);

public sealed record MfaConfirmResult(
    bool Succeeded,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string>? RecoveryCodes = null);
