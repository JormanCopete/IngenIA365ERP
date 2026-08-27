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
    /// Resuelve en lote el estado de seguridad de un conjunto de usuarios: si
    /// tienen segundo factor y cuándo entraron por última vez.
    ///
    /// <para>
    /// <b>Por qué hace falta.</b> La pantalla de usuarios de una cooperativa lee
    /// <c>SEC_Users</c>, y esas dos cosas ya no viven ahí: el segundo factor se
    /// inscribe sobre la identidad central y el último acceso lo sella ella. Las
    /// columnas de <c>SEC_Users</c> quedaron sin nadie que las escribiera, así
    /// que la pantalla mostraba «MFA: No» y último acceso vacío para todo el
    /// mundo — con aspecto de dato real.
    /// </para>
    ///
    /// <para>
    /// En lote y no una consulta por fila: son bases distintas y el listado
    /// pagina de veinte en veinte. Ids inexistentes o eliminados simplemente no
    /// aparecen en el diccionario, y quien llama distingue así «no» de «no se
    /// sabe».
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, CentralUserSecuritySnapshot>> GetSecuritySnapshotsAsync(
        IReadOnlyCollection<Guid> centralUserIds, CancellationToken ct);

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

    /// <summary>
    /// Rota el SecurityStamp: cierra TODAS las sesiones de la persona, en todos
    /// sus dispositivos, sin tocar su contraseña.
    ///
    /// <para>
    /// Es lo que sostiene <c>POST /api/auth/logout-all</c>, y el runbook lo
    /// prescribe ante sospecha de fuga de la clave RSA. No hace falta recorrer
    /// las sesiones una por una: el refresh compara el stamp guardado en la
    /// sesión contra el actual del usuario y rechaza si difieren, así que un
    /// stamp nuevo las invalida todas de golpe — incluidas las que ya estaban
    /// emitidas y las que no conocemos.
    /// </para>
    /// </summary>
    Task<bool> RotateSecurityStampAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Reset administrativo (master admin). NO requiere currentPassword.
    /// Genera nuevo SecurityStamp → invalida todos los refresh tokens.</summary>
    Task<ChangePasswordResult> AdminResetPasswordAsync(
        Guid centralUserId,
        string newPassword,
        CancellationToken ct);

    // -------------------- MFA --------------------

    /// <summary>Genera el secret TOTP para una configuración pendiente (no se
    /// persiste hasta <see cref="ConfirmMfaSetupAsync"/>). NO devuelve códigos
    /// de recuperación: los válidos los emite el confirm, y entregarlos aquí
    /// significaba darle al usuario diez códigos que nunca se iban a canjear.</summary>
    Task<MfaEnrollmentSetup> BeginMfaEnrollmentAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// Verifica un TOTP contra un secret pendiente. Si acierta, AÑADE una
    /// credencial en <c>ADM_MfaCredentials</c> —no reemplaza las que ya haya— y
    /// activa el segundo factor.
    ///
    /// <para>
    /// Los códigos de recuperación se emiten SÓLO con el primer autenticador. Con
    /// el segundo la lista viene vacía a propósito: regenerarlos invalidaría los
    /// que la persona guardó al inscribir el primero.
    /// </para>
    /// </summary>
    /// <param name="label">Nombre que la persona le da al dispositivo. Opcional.</param>
    Task<MfaConfirmResult> ConfirmMfaSetupAsync(
        Guid centralUserId,
        string base32Secret,
        string code,
        string? label,
        CancellationToken ct);

    /// <summary>Verifica un TOTP contra el secret YA persistido del usuario (login con MFA).</summary>
    Task<bool> VerifyMfaCodeAsync(Guid centralUserId, string code, CancellationToken ct);

    /// <summary>Desactiva MFA del usuario. La validación de "ninguna política de
    /// tenant lo exige" es responsabilidad del caller (handler de DisableMfaCommand).</summary>
    Task DisableMfaAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Canjea un recovery code one-shot (FR-108). Si el código es válido
    /// queda invalidado permanentemente; retorna <c>false</c> con código inválido,
    /// ya usado, o usuario sin MFA activo.</summary>
    Task<bool> RedeemRecoveryCodeAsync(Guid centralUserId, string code, CancellationToken ct);

    /// <summary>Cantidad de recovery codes sin usar del usuario (FR-110).</summary>
    Task<int> CountRecoveryCodesAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Regenera el juego completo de recovery codes (FR-111): invalida
    /// todos los anteriores y retorna los nuevos en claro (única vez que se muestran).</summary>
    Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(Guid centralUserId, CancellationToken ct);

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

/// <param name="QrPngDataUri">
/// El <c>otpauth://</c> ya convertido en imagen, listo para un <c>&lt;img src&gt;</c>
/// («data:image/png;base64,…»). Va como data URI y no como una ruta a descargar
/// para que el secreto no acabe en el registro de accesos de nadie: una URL con el
/// QR sería una URL que contiene, en la práctica, el segundo factor.
/// </param>
public sealed record MfaEnrollmentSetup(
    string SecretBase32,
    string OtpAuthUri,
    string QrPngDataUri,
    int ExpiresInSeconds);

public sealed record MfaConfirmResult(
    bool Succeeded,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string>? RecoveryCodes = null);

/// <summary>
/// Estado de seguridad de una persona, tal y como lo sabe la identidad central.
/// </summary>
public sealed record CentralUserSecuritySnapshot(bool TwoFactorEnabled, DateTime? LastLoginAt);
