using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Login;

/// <summary>
/// T066 — State machine del login centralizado. El handler resuelve TODA
/// la decisión de hacia dónde enviar al usuario tras validar credenciales:
///
/// <para>Pseudo-flow:</para>
/// <list type="number">
///   <item>LockoutCheck → si bloqueado → 423 Identity.Locked.Soft.</item>
///   <item>FindCentralUser → si null → register failure + InvalidCredentials.
///         (mismo mensaje que password mal por FR-041 — antienumeración).</item>
///   <item>ValidatePassword → si false → register failure + InvalidCredentials.</item>
///   <item>Reset lockout counter + RecordSuccessfulLogin + audit append-only.</item>
///   <item>Load active memberships.</item>
///   <item>0 memberships → NoActiveMembership challenge.</item>
///   <item>Cualquier tenant exige MFA + usuario sin MFA → MfaEnrollmentRequired.</item>
///   <item>Usuario con MFA habilitado → MfaRequired (challenge mfa-verify).</item>
///   <item>1 membership → autoSelected, emite access+refresh con active_tenant_id.</item>
///   <item>&gt;1 + DefaultTenantId apunta a membresía active → autoSelected al default.</item>
///   <item>&gt;1 + DefaultTenantId zombi → UPDATE null + audit + caer a TenantSelection.</item>
///   <item>&gt;1 sin default válido → TenantSelection (challenge tenant-select).</item>
/// </list>
/// </summary>
public sealed class LoginCommandHandler(
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ILoginAttemptCounter attemptCounter,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IAdminDbContext adminDb,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ChallengeTokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var now = clock.UtcNow;

        // 1) Lockout check (Redis, por email normalizado).
        var lockState = await attemptCounter.CheckAsync(normalizedEmail, ct);
        if (lockState.IsLocked)
        {
            await RecordAttemptAsync(
                centralUserId: null,
                normalizedEmail: normalizedEmail,
                result: LoginAttemptResult.LockedOut,
                request, now,
                lockoutAppliedSeconds: lockState.RetryAfterSeconds, ct);
            return Result.Failure<LoginResult>(
                "Identity.Locked.Soft",
                $"Cuenta bloqueada temporalmente. Reintenta en {lockState.RetryAfterSeconds} segundos.");
        }

        // 2) Resolver usuario. Email inexistente y password incorrecta deben
        //    devolver el mismo mensaje (FR-041).
        var user = await centralIdentity.FindByEmailAsync(request.Email, ct);
        if (user is null)
        {
            await OnInvalidCredentialsAsync(null, normalizedEmail, request, now, LoginAttemptResult.UserNotFound, ct);
            return InvalidCredentials();
        }

        // 3) Validar password.
        var passwordOk = await centralIdentity.ValidatePasswordAsync(user.Id, request.Password, ct);
        if (!passwordOk)
        {
            await OnInvalidCredentialsAsync(user.Id, normalizedEmail, request, now, LoginAttemptResult.InvalidPassword, ct);
            return InvalidCredentials();
        }

        // 4) Login OK → resetear contador, registrar success, actualizar LastLogin.
        await attemptCounter.ResetAsync(normalizedEmail, ct);
        await centralIdentity.RecordSuccessfulLoginAsync(user.Id, now, ct);
        await RecordAttemptAsync(user.Id, normalizedEmail, LoginAttemptResult.Success, request, now, null, ct);
        await EmitAuditAsync(user.Id, user.Email, AuditEventTypes.CentralUserLoginSuccess, request, now, ct);

        // 5) Cargar membresías activas.
        var active = await memberships.GetActiveMembershipsAsync(user.Id, ct);

        // 6) Cero membresías → NoActiveMembership (sin JWT).
        if (active.Count == 0)
        {
            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.NoActiveMembership,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                Message: "No tienes acceso a ninguna empresa. Solicita una invitación."));
        }

        // 7) Política MFA forzada por algún tenant + usuario sin MFA → MfaEnrollmentRequired.
        var requiringMfa = active.Where(m => m.IsMfaRequiredByTenant).ToList();
        if (requiringMfa.Count > 0 && !user.TwoFactorEnabled)
        {
            var challenge = jwtIssuer.IssueChallengeToken(
                centralUserId: user.Id,
                email: user.Email,
                isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                purpose: CentralJwtPurposes.MfaEnroll,
                lifetime: ChallengeTokenLifetime);

            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.MfaEnrollmentRequired,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                ChallengeToken: challenge.Jwt,
                ChallengeTokenPurpose: CentralJwtPurposes.MfaEnroll,
                ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
                TenantsRequiringMfa: requiringMfa
                    .Select(m => new TenantSummary(m.TenantId, m.TenantName))
                    .ToList()));
        }

        // 8) Usuario con MFA habilitado → MfaRequired (challenge mfa-verify).
        if (user.TwoFactorEnabled)
        {
            var challenge = jwtIssuer.IssueChallengeToken(
                centralUserId: user.Id,
                email: user.Email,
                isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                purpose: CentralJwtPurposes.MfaVerify,
                lifetime: ChallengeTokenLifetime);

            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.MfaRequired,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                ChallengeToken: challenge.Jwt,
                ChallengeTokenPurpose: CentralJwtPurposes.MfaVerify,
                ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds));
        }

        // 9-11) Sin MFA pendiente → decidir auto-select vs TenantSelection.
        return await IssueOperationalOrSelectorAsync(user, active, request, now, ct);
    }

    /// <summary>
    /// Después del happy path de credenciales+MFA, decide si emite tokens
    /// operativos (autoSelected) o un challenge tenant-select.
    /// </summary>
    private async Task<Result<LoginResult>> IssueOperationalOrSelectorAsync(
        Domain.Entities.Admin.CentralUser user,
        IReadOnlyList<ActiveMembershipInfo> active,
        LoginCommand request,
        DateTime now,
        CancellationToken ct)
    {
        // Caso 1 tenant → autoSelected al único.
        if (active.Count == 1)
        {
            var m = active[0];
            return await IssueOperationalAsync(user, m, isAutoSelected: true, ct);
        }

        // Caso >1 tenant: ver si DefaultTenantId apunta a una membresía Active.
        if (user.DefaultTenantId.HasValue)
        {
            var defaultMembership = active.FirstOrDefault(m => m.TenantId == user.DefaultTenantId.Value);
            if (defaultMembership is not null)
            {
                return await IssueOperationalAsync(user, defaultMembership, isAutoSelected: true, ct);
            }

            // DefaultTenantId zombi → limpiar silenciosamente + audit.
            await centralIdentity.SetDefaultTenantAsync(user.Id, null, ct);
            await EmitAuditAsync(
                user.Id, user.Email,
                AuditEventTypes.ProfileDefaultTenantInvalidatedCleared,
                request, now, ct);
            logger.LogInformation(
                "DefaultTenantId zombi {DefaultId} limpiado para CentralUser {UserId}.",
                user.DefaultTenantId.Value, user.Id);
        }

        // Caer a TenantSelection: emitir challenge purpose=tenant-select.
        var challenge = jwtIssuer.IssueChallengeToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            purpose: CentralJwtPurposes.TenantSelect,
            lifetime: ChallengeTokenLifetime);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.TenantSelection,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            ChallengeToken: challenge.Jwt,
            ChallengeTokenPurpose: CentralJwtPurposes.TenantSelect,
            ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
            ActiveTenants: active
                .Select(m => new ActiveTenantSummary(m.TenantId, m.TenantName, m.IsTenantAdmin))
                .ToList(),
            DefaultTenantPublicId: null, // si llegamos aquí, no había default válido
            AutoSelected: false));
    }

    /// <summary>
    /// Emite access+refresh JWT con <c>active_tenant_id</c> resuelto y persiste
    /// el refresh token en Redis. Llamado en autoSelected.
    /// </summary>
    private async Task<Result<LoginResult>> IssueOperationalAsync(
        Domain.Entities.Admin.CentralUser user,
        ActiveMembershipInfo membership,
        bool isAutoSelected,
        CancellationToken ct)
    {
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: membership.TenantId,
            tenantAdmin: membership.IsTenantAdmin,
            mfaVerified: user.TwoFactorEnabled);

        var refresh = jwtIssuer.IssueRefreshToken();

        var familyId = Guid.NewGuid();
        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: user.Id,
                ActiveTenantPublicId: membership.TenantId,
                FamilyId: familyId,
                IssuedAt: clock.UtcNow,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null),
            ttl: RefreshTokenTtl,
            ct: ct);

        return Result.Success(new LoginResult(
            Challenge: LoginChallenges.None,
            CentralUserId: user.Id,
            Email: user.Email,
            IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - clock.UtcNow).TotalSeconds,
            AutoSelected: isAutoSelected,
            ActiveTenantPublicId: membership.TenantId,
            ActiveTenantName: membership.TenantName,
            DefaultTenantPublicId: user.DefaultTenantId,
            ActiveTenants: new[]
            {
                new ActiveTenantSummary(membership.TenantId, membership.TenantName, membership.IsTenantAdmin)
            }));
    }

    // -------------------- Helpers --------------------

    private async Task OnInvalidCredentialsAsync(
        Guid? centralUserId,
        string normalizedEmail,
        LoginCommand request,
        DateTime now,
        LoginAttemptResult result,
        CancellationToken ct)
    {
        var verdict = await attemptCounter.RecordFailureAsync(normalizedEmail, ct);
        await RecordAttemptAsync(
            centralUserId, normalizedEmail, result, request, now,
            lockoutAppliedSeconds: verdict.ShouldLock ? verdict.LockSeconds : null, ct);

        if (centralUserId.HasValue)
        {
            // Solo emitimos audit del usuario real — los intentos contra
            // email inexistente no se atribuyen a ningún CentralUser.
            await EmitAuditAsync(
                centralUserId.Value, normalizedEmail,
                AuditEventTypes.CentralUserLoginFailed, request, now, ct);
        }
    }

    private async Task RecordAttemptAsync(
        Guid? centralUserId,
        string normalizedEmail,
        LoginAttemptResult result,
        LoginCommand request,
        DateTime now,
        int? lockoutAppliedSeconds,
        CancellationToken ct)
    {
        try
        {
            var record = CentralUserLoginAttempt.Record(
                centralUserId, normalizedEmail, result,
                request.IpAddress, request.UserAgent, now,
                lockoutAppliedSeconds);
            adminDb.CentralUserLoginAttempts.Add(record);
            await adminDb.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Telemetría no debe tumbar el flow.
            logger.LogWarning(ex, "Fallo persistiendo CentralUserLoginAttempt ({Result})", result);
        }
    }

    private async Task EmitAuditAsync(
        Guid centralUserId,
        string email,
        string action,
        LoginCommand request,
        DateTime now,
        CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty, // Login no está scoped a un tenant
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: request.UserAgent,
                Endpoint: "/api/auth/login",
                HttpMethod: "POST",
                HttpStatusCode: action == AuditEventTypes.CentralUserLoginSuccess ? 200 : 401,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para acción {Action}", action);
        }
    }

    private static Result<LoginResult> InvalidCredentials() =>
        Result.Failure<LoginResult>(
            "Identity.InvalidCredentials",
            "Credenciales inválidas.");
}
