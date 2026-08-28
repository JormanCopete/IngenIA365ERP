using System.Text;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OtpNet;

namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Implementación de <see cref="ICentralIdentityProvider"/> sobre ASP.NET Core
/// Identity (T041). Envuelve <see cref="UserManager{TUser}"/> para que ningún
/// handler de Application toque directamente el framework.
///
/// <para>Características:</para>
/// <list type="bullet">
///   <item>Password hashing: <c>BcryptPasswordHasher</c> (BCrypt cost 11).</item>
///   <item>Pwned check: en <c>CreateUserAsync</c>, <c>ChangePasswordAsync</c>,
///         <c>AdminResetPasswordAsync</c>. Fail-open por design (research D-04).</item>
///   <item>MFA: TOTP via Otp.NET, secret base32 cifrado con
///         <see cref="IDataProtectionProvider"/>. El purpose es
///         <c>central-identity:mfa-secret</c> — la constante de abajo, no el
///         "mfa-secret" que este comentario decía antes. Equivocarse de purpose
///         deja a toda persona con segundo factor fuera de su cuenta, y en
///         silencio: Unprotect lanza, el catch lo registra, y la respuesta es
///         "código inválido".</item>
///   <item>Recovery codes: <c>UserManager.GenerateNewTwoFactorRecoveryCodesAsync</c>
///         (almacenamiento nativo en <c>ADM_CentralUserTokens</c>).</item>
/// </list>
/// </summary>
internal sealed class AspNetCoreIdentityProvider : ICentralIdentityProvider
{
    private const string DataProtectorPurpose = "central-identity:mfa-secret";
    private const int RecoveryCodeCount = 10;
    private const string TotpIssuer = "IngenIA365ERP";

    private readonly UserManager<CentralUserIdentity> _userManager;
    private readonly IPwnedPasswordService _pwned;
    private readonly IDataProtector _protector;
    private readonly IMfaDirectory _credenciales;
    private readonly ILogger<AspNetCoreIdentityProvider> _log;

    public AspNetCoreIdentityProvider(
        UserManager<CentralUserIdentity> userManager,
        IPwnedPasswordService pwned,
        IDataProtectionProvider dataProtection,
        IMfaDirectory credenciales,
        ILogger<AspNetCoreIdentityProvider> log)
    {
        _userManager = userManager;
        _pwned = pwned;
        _protector = dataProtection.CreateProtector(DataProtectorPurpose);
        _credenciales = credenciales;
        _log = log;
    }

    // -------------------- Lookup --------------------

    public async Task<CentralUser?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var identity = await _userManager.FindByEmailAsync(email);
        return identity is null || identity.IsDeleted ? null : ToDomain(identity);
    }

    public async Task<CentralUser?> FindByIdAsync(Guid centralUserId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        return identity is null || identity.IsDeleted ? null : ToDomain(identity);
    }

    // -------------------- Create / passwords --------------------

    public async Task<CreateCentralUserResult> CreateUserAsync(
        string email, string password, bool emailConfirmed, CancellationToken ct)
    {
        var pwnedCheck = await _pwned.IsPwnedAsync(password, ct);
        if (pwnedCheck.ServiceAvailable && pwnedCheck.IsPwned)
        {
            return new CreateCentralUserResult(false, null, ["Identity.Password.Pwned"]);
        }

        var identity = new CentralUserIdentity
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            Status = (int)CentralUserStatus.Active,
        };

        var result = await _userManager.CreateAsync(identity, password);
        if (!result.Succeeded)
        {
            return new CreateCentralUserResult(false, null, [.. result.Errors.Select(e => $"Identity.{e.Code}")]);
        }

        return new CreateCentralUserResult(true, identity.Id, []);
    }

    public async Task<bool> ValidatePasswordAsync(Guid centralUserId, string password, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted || identity.Status == (int)CentralUserStatus.Disabled)
            return false;

        return await _userManager.CheckPasswordAsync(identity, password);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(
        Guid centralUserId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted)
            return new ChangePasswordResult(false, ["Identity.UserNotFound"]);

        var pwnedCheck = await _pwned.IsPwnedAsync(newPassword, ct);
        if (pwnedCheck.ServiceAvailable && pwnedCheck.IsPwned)
            return new ChangePasswordResult(false, ["Identity.Password.Pwned"]);

        var result = await _userManager.ChangePasswordAsync(identity, currentPassword, newPassword);
        if (!result.Succeeded)
            return new ChangePasswordResult(false, [.. result.Errors.Select(e => $"Identity.{e.Code}")]);

        // ChangePasswordAsync ya regenera SecurityStamp internamente — invalida refresh tokens.
        return new ChangePasswordResult(true, []);
    }

    public async Task<ChangePasswordResult> AdminResetPasswordAsync(
        Guid centralUserId, string newPassword, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted)
            return new ChangePasswordResult(false, ["Identity.UserNotFound"]);

        var pwnedCheck = await _pwned.IsPwnedAsync(newPassword, ct);
        if (pwnedCheck.ServiceAvailable && pwnedCheck.IsPwned)
            return new ChangePasswordResult(false, ["Identity.Password.Pwned"]);

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(identity);
        var result = await _userManager.ResetPasswordAsync(identity, resetToken, newPassword);
        if (!result.Succeeded)
            return new ChangePasswordResult(false, [.. result.Errors.Select(e => $"Identity.{e.Code}")]);

        await _userManager.UpdateSecurityStampAsync(identity);
        return new ChangePasswordResult(true, []);
    }

    public async Task<bool> RotateSecurityStampAsync(Guid centralUserId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted) return false;

        // Un stamp nuevo invalida todas las sesiones sin recorrerlas: el refresh
        // compara el guardado en la sesión con el actual y rechaza si difieren.
        var result = await _userManager.UpdateSecurityStampAsync(identity);
        return result.Succeeded;
    }

    public async Task<IReadOnlyDictionary<Guid, CentralUserSecuritySnapshot>> GetSecuritySnapshotsAsync(
        IReadOnlyCollection<Guid> centralUserIds, CancellationToken ct)
    {
        if (centralUserIds.Count == 0)
            return new Dictionary<Guid, CentralUserSecuritySnapshot>();

        var ids = centralUserIds.ToArray();
        var rows = await _userManager.Users
            .Where(u => ids.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new { u.Id, u.TwoFactorEnabled, u.LastLoginAt })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => new CentralUserSecuritySnapshot(r.TwoFactorEnabled, r.LastLoginAt));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsByIdsAsync(
        IReadOnlyCollection<Guid> centralUserIds, CancellationToken ct)
    {
        if (centralUserIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = centralUserIds.ToArray();
        var rows = await _userManager.Users
            .Where(u => ids.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.Email ?? string.Empty);
    }

    // -------------------- MFA --------------------

    public async Task<MfaEnrollmentSetup> BeginMfaEnrollmentAsync(Guid centralUserId, CancellationToken ct)
    {
        // El secret pendiente NO se persiste aquí — vive en Redis hasta confirm
        // (cache key 'mfa-pending:{id}' gestionada por Application — Chunk D/4b).
        var secretBytes = KeyGeneration.GenerateRandomKey(20);   // 160 bits, recomendado por RFC 6238
        var base32 = Base32Encoding.ToString(secretBytes);

        // Etiqueta de cuenta: el email — es lo que la app de autenticación
        // muestra al usuario. El GUID queda solo como fallback defensivo.
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        var accountLabel = string.IsNullOrWhiteSpace(identity?.Email)
            ? centralUserId.ToString("N")
            : identity!.Email!;

        // otpauth URI — hoy la pantalla lo imprime como texto: no se genera QR
        // en ninguna capa. Había un TotpService con QRCoder que nadie llamaba y
        // se retiró; la pantalla nunca lo usó.
        var otpAuthUri =
            $"otpauth://totp/{Uri.EscapeDataString(TotpIssuer)}:{Uri.EscapeDataString(accountLabel)}" +
            $"?secret={base32}" +
            $"&issuer={Uri.EscapeDataString(TotpIssuer)}" +
            $"&algorithm=SHA1&digits=6&period=30";

        // Aquí NO se generan códigos de recuperación, y antes sí.
        //
        // Este método devolvía diez códigos con formato AB12-CD34 que se
        // guardaban en Redis y se descartaban: los válidos los produce ASP.NET
        // Identity en ConfirmMfaSetupAsync, con otro alfabeto. Quien anotara los
        // del begin y cerrara la pantalla antes de confirmar se quedaba con diez
        // códigos que nunca iban a canjearse — justo el papelito que uno guarda
        // para el día que pierde el teléfono.
        return new MfaEnrollmentSetup(
            SecretBase32: base32,
            OtpAuthUri: otpAuthUri,
            QrPngDataUri: GenerarQrPngDataUri(otpAuthUri),
            ExpiresInSeconds: 600);
    }

    public async Task<MfaConfirmResult> ConfirmMfaSetupAsync(
        Guid centralUserId, string base32Secret, string code, string? label, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted)
            return new MfaConfirmResult(false, ["Identity.UserNotFound"]);

        // Validar el código TOTP contra el secret pendiente.
        var secretBytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secretBytes);
        if (!totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay))
            return new MfaConfirmResult(false, ["Profile.Mfa.InvalidCode"]);

        var protegido = _protector.Protect(base32Secret);

        // ¿Es su PRIMER autenticador? Hay que saberlo antes de inscribir el nuevo.
        var esElPrimero = await _credenciales.ContarActivasAsync(identity.Id, ct) == 0;

        // La credencial PRIMERO, la bandera DESPUÉS. Si algo falla en medio, la
        // persona queda con credencial y sin bandera: el login no le pide segundo
        // factor y entra. Al revés —bandera sin credencial— le pediría un código
        // que no puede acertar, y sólo la sacaría de ahí un reseteo administrativo.
        // Todo fallo a medias tiene que caer del lado seguro.
        //
        // AÑADE, no reemplaza: agregar el segundo autenticador no puede borrar el
        // primero.
        await _credenciales.InscribirTotpAsync(
            identity.Id, protegido, label, utcNow: DateTime.UtcNow, ct);

        // La columna vieja se sigue escribiendo durante el traslado: es la red por
        // si hay que volver a la versión anterior, que sólo sabe leer de ahí.
        identity.MfaSecret = protegido;
        identity.TwoFactorEnabled = true;
        var updateResult = await _userManager.UpdateAsync(identity);
        if (!updateResult.Succeeded)
            return new MfaConfirmResult(false, [.. updateResult.Errors.Select(e => $"Identity.{e.Code}")]);

        // Los códigos de recuperación se emiten SÓLO con el primer autenticador.
        //
        // GenerateNewTwoFactorRecoveryCodesAsync invalida los anteriores. Antes daba
        // igual porque inscribir era siempre la primera vez; ahora, agregar un
        // segundo teléfono habría dejado sin valor los diez códigos que la persona
        // guardó en un papel el día que inscribió el primero — sin avisarle, y
        // descubriéndolo justo el día que los necesita. Para renovarlos está
        // /api/profile/mfa/recovery-codes/regenerate, que sí lo dice.
        if (!esElPrimero)
        {
            return new MfaConfirmResult(true, [], []);
        }

        // GenerateNewTwoFactorRecoveryCodesAsync persiste en ADM_CentralUserTokens.
        var recovery = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(identity, RecoveryCodeCount);
        var codes = recovery?.ToList() ?? new List<string>();

        return new MfaConfirmResult(true, [], codes);
    }

    public async Task<IReadOnlyList<string>> ActivarSegundoFactorAsync(
        Guid centralUserId, bool esLaPrimeraCredencial, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted) return [];

        // NO se toca MfaSecret. Esa columna es la red de rollback del TOTP: si
        // una passkey la escribiera, la versión anterior —que sólo sabe leer de
        // ahí— intentaría verificar un secreto TOTP que no existe.
        if (!identity.TwoFactorEnabled)
        {
            identity.TwoFactorEnabled = true;
            await _userManager.UpdateAsync(identity);
        }

        if (!esLaPrimeraCredencial) return [];

        var recovery = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(identity, RecoveryCodeCount);
        return recovery?.ToList() ?? [];
    }

    public async Task<bool> VerifyMfaCodeAsync(Guid centralUserId, string code, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted || !identity.TwoFactorEnabled)
            return false;

        var credenciales = await _credenciales.ListarCifradosTotpActivosAsync(centralUserId, ct);

        if (credenciales.Count == 0)
        {
            return await VerificarPorCompatibilidadAsync(identity, code, centralUserId, ct);
        }

        // Se prueban TODAS, sin cortar en la primera que acierta.
        //
        // Cortar filtraría por el reloj: acertar con la primera respondería antes
        // que acertar con la tercera, y eso le dice a quien mida los tiempos
        // cuántos autenticadores tiene la víctima. Probarlas todas cuesta N
        // descifrados fijos —de ahí el tope por persona— y no dice nada.
        Guid? acertada = null;
        foreach (var credencial in credenciales)
        {
            var acierta = CodigoValidoContra(credencial.SecretProtected, code, centralUserId);
            if (acierta && acertada is null)
            {
                acertada = credencial.PublicId;
            }
        }

        if (acertada is null) return false;

        await _credenciales.MarcarUsoAsync(centralUserId, acertada.Value, DateTime.UtcNow, ct);
        return true;
    }

    /// <summary>
    /// Modo compatibilidad: la columna heredada <c>ADM_CentralUsers.MfaSecret</c>.
    ///
    /// <para>
    /// La condición es «NUNCA tuvo credencial», no «la lista vino vacía», y la
    /// diferencia no es de estilo. Revocar es baja lógica: el filtro global saca la
    /// fila de la lista pero la columna heredada conserva su valor, porque sólo la
    /// limpiaba la baja total. Con la condición ingenua, quien revocara su última
    /// credencial desde la pantalla de gestión seguiría entrando con el
    /// autenticador que acaba de dar de baja.
    /// </para>
    /// </summary>
    private async Task<bool> VerificarPorCompatibilidadAsync(
        CentralUserIdentity identity, string code, Guid centralUserId, CancellationToken ct)
    {
        if (identity.MfaSecret is null) return false;

        if (await _credenciales.HuboAlgunaVezTotpAsync(centralUserId, ct))
        {
            _log.LogWarning(
                "[Mfa.CredencialesRevocadas] {CentralUserId} no tiene credenciales activas pero " +
                "ADM_CentralUsers.MfaSecret sigue con valor. Se rechaza: la columna heredada no puede " +
                "resucitar un autenticador revocado. Revisar por qué no se limpió.",
                centralUserId);
            return false;
        }

        _log.LogWarning(
            "[Mfa.LecturaHeredada] {CentralUserId} verificó con ADM_CentralUsers.MfaSecret: " +
            "no tiene credencial en ADM_MfaCredentials. Si esto aparece después del traslado, " +
            "el traslado no la cubrió.",
            centralUserId);

        return CodigoValidoContra(identity.MfaSecret, code, centralUserId);
    }

    /// <summary>
    /// Convierte el <c>otpauth://</c> en un PNG listo para un <c>&lt;img src&gt;</c>.
    ///
    /// <para>
    /// Se devuelve incrustado como data URI, no como una ruta que el navegador
    /// pida aparte: una URL que sirva este QR es una URL que contiene el segundo
    /// factor, y acabaría en el registro de accesos del servidor, del proxy y de
    /// cualquier intermediario. Incrustado, viaja sólo por la respuesta que ya
    /// lleva el secreto de todos modos.
    /// </para>
    ///
    /// <para>
    /// El <c>otpauth://</c> se sigue devolviendo aparte: quien use un lector de
    /// pantalla, o tenga la cámara rota, necesita poder escribir la clave.
    /// </para>
    /// </summary>
    private static string GenerarQrPngDataUri(string otpAuthUri)
    {
        using var generador = new QRCoder.QRCodeGenerator();
        using var datos = generador.CreateQrCode(otpAuthUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var png = new QRCoder.PngByteQRCode(datos);

        // 10 píxeles por módulo: se escanea sin esfuerzo desde una pantalla y el
        // PNG sigue pesando unos pocos kilobytes.
        var bytes = png.GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Descifra UN texto protegido y prueba el código.
    ///
    /// <para>
    /// Un llavero roto en una credencial no puede abortar el barrido de las demás.
    /// El catch de antes hacía <c>return false</c> del método entero: trasladado
    /// tal cual a un bucle, habría dejado fuera a quien tuviera una credencial
    /// vieja ilegible y su teléfono actual correcto. Aquí falla sólo esa
    /// credencial, y queda registrado.
    /// </para>
    /// </summary>
    private bool CodigoValidoContra(string secretoProtegido, string code, Guid centralUserId)
    {
        string unprotected;
        try
        {
            unprotected = _protector.Unprotect(secretoProtegido);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _log.LogError(ex,
                "MfaSecret unprotect falló para una credencial de {CentralUserId}. " +
                "El barrido continúa con las demás.",
                centralUserId);
            return false;
        }

        var secretBytes = Base32Encoding.ToBytes(unprotected);
        var totp = new Totp(secretBytes);
        return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    public async Task<bool> RedeemRecoveryCodeAsync(Guid centralUserId, string code, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted || !identity.TwoFactorEnabled)
            return false;

        // RedeemTwoFactorRecoveryCodeAsync invalida el código en ADM_CentralUserTokens (one-shot).
        var result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(identity, code.Trim());
        return result.Succeeded;
    }

    public async Task<int> CountRecoveryCodesAsync(Guid centralUserId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted)
            return 0;

        return await _userManager.CountRecoveryCodesAsync(identity);
    }

    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(Guid centralUserId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted || !identity.TwoFactorEnabled)
            return [];

        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(identity, RecoveryCodeCount);
        return codes?.ToList() ?? [];
    }

    public async Task DisableMfaAsync(Guid centralUserId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted) return;

        // Al dar de baja, el orden se invierte respecto al alta: credenciales
        // PRIMERO, bandera después. Así un fallo a medias deja la bandera en true
        // sin credencial —el login pide un código que no vale, molesto pero
        // recuperable— y nunca al revés, que sería dejar la cuenta sin segundo
        // factor creyendo que lo tiene.
        await _credenciales.RevocarTodasAsync(centralUserId, DateTime.UtcNow, ct);

        identity.MfaSecret = null;
        identity.TwoFactorEnabled = false;
        await _userManager.UpdateAsync(identity);
    }

    public async Task ResetMfaAsync(Guid centralUserId, CancellationToken ct)
    {
        // Idéntico a Disable pero documentado como "reset por master".
        await DisableMfaAsync(centralUserId, ct);
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is not null)
            await _userManager.UpdateSecurityStampAsync(identity);
    }

    // -------------------- Telemetry --------------------

    public async Task RecordSuccessfulLoginAsync(Guid centralUserId, DateTime utcNow, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null) return;

        identity.LastLoginAt = utcNow;
        await _userManager.UpdateAsync(identity);
    }

    public async Task SetDefaultTenantAsync(
        Guid centralUserId, Guid? defaultTenantPublicId, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null) return;

        if (identity.DefaultTenantId == defaultTenantPublicId) return; // no-op idempotente

        identity.DefaultTenantId = defaultTenantPublicId;
        await _userManager.UpdateAsync(identity);
    }

    // -------------------- Helpers --------------------

    private static CentralUser ToDomain(CentralUserIdentity identity) => new()
    {
        Id = identity.Id,
        Email = identity.Email ?? string.Empty,
        NormalizedEmail = identity.NormalizedEmail ?? string.Empty,
        EmailConfirmed = identity.EmailConfirmed,
        PasswordHash = identity.PasswordHash ?? string.Empty,
        SecurityStamp = identity.SecurityStamp ?? string.Empty,
        ConcurrencyStamp = identity.ConcurrencyStamp ?? string.Empty,
        TwoFactorEnabled = identity.TwoFactorEnabled,
        MfaSecret = identity.MfaSecret,
        LockoutEnd = identity.LockoutEnd?.UtcDateTime,
        LockoutEnabled = identity.LockoutEnabled,
        AccessFailedCount = identity.AccessFailedCount,
        DefaultTenantId = identity.DefaultTenantId,
        IsGlobalMasterAdmin = identity.IsGlobalMasterAdmin,
        Status = (CentralUserStatus)identity.Status,
        CreatedAt = identity.CreatedAt,
        CreatedBy = identity.CreatedBy,
        UpdatedAt = identity.UpdatedAt,
        UpdatedBy = identity.UpdatedBy,
        IsDeleted = identity.IsDeleted,
        DeletedAt = identity.DeletedAt,
        DeletedBy = identity.DeletedBy,
        LastLoginAt = identity.LastLoginAt,
    };

}
