using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.RefreshToken;

/// <summary>
/// T068 — Refresh con family rotation y detección de reuso.
///
/// <para>Algoritmo:</para>
/// <list type="number">
///   <item>Hash hex del refresh recibido → buscar sesión en Redis.</item>
///   <item>Si <c>ReplacedByTokenHashHex != null</c> ⇒ alguien lo reutilizó:
///         invalidar familia entera y rechazar.</item>
///   <item>Validar que el CentralUser sigue activo y su membresía con
///         <c>session.ActiveTenantPublicId</c> también.</item>
///   <item>Emitir nuevo access + refresh con MISMA familyId.</item>
///   <item>MarkRotatedAsync(old → new) + Store(new).</item>
/// </list>
/// </summary>
public sealed class RefreshTokenCommandHandler(
    ICentralRefreshTokenStore refreshStore,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    IDateTimeService clock,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<Result<RefreshTokenResult>> Handle(
        RefreshTokenCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.Invalid", "Refresh token vacío.");
        }

        var oldHash = HashHex(request.RefreshToken);
        var session = await refreshStore.GetAsync(oldHash, ct);
        if (session is null)
        {
            // No existe, expirado, o familia invalidada — todos comparten respuesta
            // genérica para no diferenciar entre causas.
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.Invalid", "Refresh token inválido o expirado.");
        }

        // Detección de reuso — el token ya fue rotado y alguien lo intenta otra vez.
        if (session.ReplacedByTokenHashHex is not null)
        {
            logger.LogWarning(
                "Refresh token reusado detectado para CentralUser {UserId} familia {Family}. Invalidando familia.",
                session.CentralUserId, session.FamilyId);
            await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.Reused",
                "Token reutilizado detectado; toda la sesión fue invalidada por seguridad.");
        }

        // Verificar usuario sigue activo.
        var user = await centralIdentity.FindByIdAsync(session.CentralUserId, ct);
        if (user is null)
        {
            await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.Invalid", "Refresh token inválido.");
        }

        // SecurityStamp regenerado (cambio/reset de contraseña, force-mfa-reset)
        // ⇒ toda sesión emitida antes del cambio muere aquí. Un stamp null en la
        // sesión (formato previo a este campo) también se rechaza — fail-secure.
        if (!string.Equals(session.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.Invalid", "Refresh token inválido o expirado.");
        }

        // Verificar membresía con el tenant activo del refresh sigue active.
        bool? membershipAdmin = null;
        if (session.ActiveTenantPublicId.HasValue)
        {
            var active = await memberships.GetActiveMembershipsAsync(user.Id, ct);
            var current = active.FirstOrDefault(m => m.TenantId == session.ActiveTenantPublicId.Value);
            if (current is null)
            {
                await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
                return Result.Failure<RefreshTokenResult>(
                    "Membership.NotActive",
                    "Tu membresía con la empresa activa ya no es vigente.");
            }
            membershipAdmin = current.IsTenantAdmin;
        }

        // Emitir nuevos tokens — preservando la familyId del refresh anterior.
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: session.ActiveTenantPublicId,
            tenantAdmin: membershipAdmin,
            mfaVerified: user.TwoFactorEnabled);

        var newRefresh = jwtIssuer.IssueRefreshToken();
        var newSession = session with
        {
            IssuedAt = clock.UtcNow,
            ReplacedByTokenHashHex = null,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
        };

        await refreshStore.StoreAsync(newRefresh.HashHex, newSession, RefreshTokenTtl, ct);
        await refreshStore.MarkRotatedAsync(oldHash, newRefresh.HashHex, ct);

        return Result.Success(new RefreshTokenResult(
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: newRefresh.Token,
            RefreshTokenExpiresAt: newRefresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - clock.UtcNow).TotalSeconds));
    }

    private static string HashHex(string plainToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }
}
