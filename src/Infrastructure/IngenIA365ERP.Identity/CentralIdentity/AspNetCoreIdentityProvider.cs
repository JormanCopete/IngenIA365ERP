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
///         <see cref="IDataProtectionProvider"/> (purpose "mfa-secret").</item>
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
    private readonly ILogger<AspNetCoreIdentityProvider> _log;

    public AspNetCoreIdentityProvider(
        UserManager<CentralUserIdentity> userManager,
        IPwnedPasswordService pwned,
        IDataProtectionProvider dataProtection,
        ILogger<AspNetCoreIdentityProvider> log)
    {
        _userManager = userManager;
        _pwned = pwned;
        _protector = dataProtection.CreateProtector(DataProtectorPurpose);
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
            ExpiresInSeconds: 600);
    }

    public async Task<MfaConfirmResult> ConfirmMfaSetupAsync(
        Guid centralUserId, string base32Secret, string code, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted)
            return new MfaConfirmResult(false, ["Identity.UserNotFound"]);

        // Validar el código TOTP contra el secret pendiente.
        var secretBytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secretBytes);
        if (!totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay))
            return new MfaConfirmResult(false, ["Profile.Mfa.InvalidCode"]);

        // Persistir secret cifrado + activar 2FA + generar recovery codes nativos.
        identity.MfaSecret = _protector.Protect(base32Secret);
        identity.TwoFactorEnabled = true;
        var updateResult = await _userManager.UpdateAsync(identity);
        if (!updateResult.Succeeded)
            return new MfaConfirmResult(false, [.. updateResult.Errors.Select(e => $"Identity.{e.Code}")]);

        // GenerateNewTwoFactorRecoveryCodesAsync persiste en ADM_CentralUserTokens.
        var recovery = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(identity, RecoveryCodeCount);
        var codes = recovery?.ToList() ?? new List<string>();

        return new MfaConfirmResult(true, [], codes);
    }

    public async Task<bool> VerifyMfaCodeAsync(Guid centralUserId, string code, CancellationToken ct)
    {
        var identity = await _userManager.FindByIdAsync(centralUserId.ToString());
        if (identity is null || identity.IsDeleted || !identity.TwoFactorEnabled || identity.MfaSecret is null)
            return false;

        string unprotected;
        try
        {
            unprotected = _protector.Unprotect(identity.MfaSecret);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _log.LogError(ex, "MfaSecret unprotect falló para {CentralUserId}", centralUserId);
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
