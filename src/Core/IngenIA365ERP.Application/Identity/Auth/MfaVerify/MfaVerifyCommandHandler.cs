using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Login;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.MfaVerify;

/// <summary>
/// T067 — Verifica el TOTP del segundo factor y, si OK, decide auto-select
/// vs TenantSelection (misma lógica que el final del <c>LoginCommandHandler</c>).
/// </summary>
public sealed class MfaVerifyCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<MfaVerifyCommandHandler> logger)
    : IRequestHandler<MfaVerifyCommand, Result<LoginResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ChallengeTokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LoginResult>> Handle(MfaVerifyCommand request, CancellationToken ct)
    {
        // 1) Validar el purpose del challengeToken (defensa en profundidad —
        //    el endpoint también monta el filter RequirePurpose("mfa-verify")).
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<LoginResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        if (!string.Equals(currentUser.Purpose, CentralJwtPurposes.MfaVerify, StringComparison.Ordinal))
        {
            return Result.Failure<LoginResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=mfa-verify, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var now = clock.UtcNow;

        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            return Result.Failure<LoginResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }
        if (!user.TwoFactorEnabled)
        {
            return Result.Failure<LoginResult>(
                "Identity.MfaNotEnabled",
                "El usuario no tiene MFA habilitado; reinicia el login.");
        }

        // 2) Verificar el segundo factor: TOTP o recovery code one-shot (FR-108/FR-109).
        //    El error hacia el cliente es genérico en ambas ramas; la auditoría distingue.
        int? recoveryCodesRemaining = null;
        if (request.UseRecoveryCode)
        {
            var redeemed = await centralIdentity.RedeemRecoveryCodeAsync(centralUserId, request.Code, ct);
            if (!redeemed)
            {
                await EmitAuditAsync(user.Id, user.Email,
                    AuditEventTypes.CentralUserMfaRecoveryCodeFailed, request, now, ct);
                return Result.Failure<LoginResult>(
                    "Identity.MfaInvalid", "Código MFA inválido.");
            }

            recoveryCodesRemaining = await centralIdentity.CountRecoveryCodesAsync(centralUserId, ct);
            await EmitAuditAsync(user.Id, user.Email,
                AuditEventTypes.CentralUserMfaRecoveryCodeUsed, request, now, ct);
        }
        else
        {
            var ok = await centralIdentity.VerifyMfaCodeAsync(centralUserId, request.Code, ct);
            if (!ok)
            {
                await EmitAuditAsync(user.Id, user.Email,
                    AuditEventTypes.CentralUserMfaFailed, request, now, ct);
                return Result.Failure<LoginResult>(
                    "Identity.MfaInvalid", "Código MFA inválido.");
            }

            await EmitAuditAsync(user.Id, user.Email,
                AuditEventTypes.CentralUserMfaSuccess, request, now, ct);
        }

        // 3) Cargar membresías y decidir auto-select vs TenantSelection.
        //    Misma lógica que LoginCommandHandler.IssueOperationalOrSelectorAsync —
        //    duplicación controlada hasta que un tercer caller justifique extraer.
        var active = await memberships.GetActiveMembershipsAsync(user.Id, ct);

        if (active.Count == 0)
        {
            // Edge: el usuario perdió todas las membresías entre login y mfa/verify.
            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.NoActiveMembership,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                Message: "No tienes acceso a ninguna empresa. Solicita una invitación."));
        }

        if (active.Count == 1)
        {
            return await IssueOperationalAsync(user, active[0], isAutoSelected: true, recoveryCodesRemaining, ct);
        }

        if (user.DefaultTenantId.HasValue)
        {
            var defaultMembership = active.FirstOrDefault(m => m.TenantId == user.DefaultTenantId.Value);
            if (defaultMembership is not null)
            {
                return await IssueOperationalAsync(user, defaultMembership, isAutoSelected: true, recoveryCodesRemaining, ct);
            }
            // DefaultTenantId zombi → limpiar.
            await centralIdentity.SetDefaultTenantAsync(user.Id, null, ct);
        }

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
            DefaultTenantPublicId: null,
            AutoSelected: false,
            RecoveryCodesRemaining: recoveryCodesRemaining));
    }

    private async Task<Result<LoginResult>> IssueOperationalAsync(
        Domain.Entities.Admin.CentralUser user,
        ActiveMembershipInfo membership,
        bool isAutoSelected,
        int? recoveryCodesRemaining,
        CancellationToken ct)
    {
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: membership.TenantId,
            tenantAdmin: membership.IsTenantAdmin,
            mfaVerified: true);

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
                ReplacedByTokenHashHex: null,
                SecurityStamp: user.SecurityStamp),
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
            },
            RecoveryCodesRemaining: recoveryCodesRemaining));
    }

    private async Task EmitAuditAsync(
        Guid centralUserId,
        string email,
        string action,
        MfaVerifyCommand request,
        DateTime now,
        CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
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
                Endpoint: "/api/auth/mfa/verify",
                HttpMethod: "POST",
                HttpStatusCode: action == AuditEventTypes.CentralUserMfaSuccess ? 200 : 401,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para acción {Action}", action);
        }
    }
}
