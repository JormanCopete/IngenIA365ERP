using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Auth.Common;

/// <summary>
/// Lo que pasa DESPUÉS de superar el segundo factor: cargar membresías y decidir
/// entre emitir la sesión directamente o pedir que se elija cooperativa.
///
/// <para>
/// Vivía dentro de <c>MfaVerifyCommandHandler</c>, con un comentario que decía
/// «duplicación controlada hasta que un tercer caller justifique extraer». El
/// ingreso con passkey es ese tercer llamador: verifica una firma en vez de un
/// código, pero a partir de ahí tiene que pasar exactamente lo mismo. Copiarlo
/// habría significado que arreglar un caso límite en un sitio dejara el otro
/// roto — y el caso límite de aquí es la salida del administrador maestro, que ya
/// se perdió una vez.
/// </para>
/// </summary>
public interface IEmisorDeSesionTrasSegundoFactor
{
    Task<Result<LoginResult>> EmitirAsync(
        CentralUser user, int? codigosDeRecuperacionRestantes, CancellationToken ct);
}

public sealed class EmisorDeSesionTrasSegundoFactor(
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IDateTimeService clock)
    : IEmisorDeSesionTrasSegundoFactor
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan ChallengeTokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<LoginResult>> EmitirAsync(
        CentralUser user, int? codigosDeRecuperacionRestantes, CancellationToken ct)
    {
        var activas = await memberships.GetActiveMembershipsAsync(user.Id, ct);

        if (activas.Count == 0)
        {
            // El maestro global no tiene membresías por diseño: gobierna el
            // conjunto de cooperativas, no pertenece a ninguna.
            //
            // Esta rama es LA SALIDA. Sin ella el maestro superaba el segundo
            // factor y recibía «no tienes acceso a ninguna empresa» sin token:
            // quedaba encerrado fuera de su propio sistema justo por haber
            // inscrito el MFA.
            if (user.IsGlobalMasterAdmin)
            {
                return await EmitirParaElMaestroAsync(user, codigosDeRecuperacionRestantes, ct);
            }

            // Perdió todas sus membresías entre el login y la verificación.
            return Result.Success(new LoginResult(
                Challenge: LoginChallenges.NoActiveMembership,
                CentralUserId: user.Id,
                Email: user.Email,
                IsGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                Message: "No tienes acceso a ninguna empresa. Solicita una invitación."));
        }

        if (activas.Count == 1)
        {
            return await EmitirOperativaAsync(
                user, activas[0], autoSeleccionada: true, codigosDeRecuperacionRestantes, ct);
        }

        if (user.DefaultTenantId.HasValue)
        {
            var porDefecto = activas.FirstOrDefault(m => m.TenantId == user.DefaultTenantId.Value);
            if (porDefecto is not null)
            {
                return await EmitirOperativaAsync(
                    user, porDefecto, autoSeleccionada: true, codigosDeRecuperacionRestantes, ct);
            }

            // La cooperativa por defecto ya no está entre las suyas: se limpia en
            // vez de dejarla apuntando a un sitio al que no puede entrar.
            await centralIdentity.SetDefaultTenantAsync(user.Id, null, ct);
        }

        var desafio = jwtIssuer.IssueChallengeToken(
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
            ChallengeToken: desafio.Jwt,
            ChallengeTokenPurpose: CentralJwtPurposes.TenantSelect,
            ExpiresInSeconds: (int)ChallengeTokenLifetime.TotalSeconds,
            ActiveTenants: activas
                .Select(m => new ActiveTenantSummary(m.TenantId, m.TenantName, m.IsTenantAdmin))
                .ToList(),
            DefaultTenantPublicId: null,
            AutoSelected: false,
            RecoveryCodesRemaining: codigosDeRecuperacionRestantes));
    }

    /// <summary>
    /// Sesión del maestro global. El token sale con <c>active_tenant_id=null</c> y
    /// <c>is_global_master_admin=true</c>, igual que el del login; la diferencia es
    /// que aquí sólo se llega tras verificar el segundo factor, que es el punto.
    /// </summary>
    private async Task<Result<LoginResult>> EmitirParaElMaestroAsync(
        CentralUser user, int? codigosRestantes, CancellationToken ct)
    {
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: true,
            activeTenantId: null,
            tenantAdmin: null,
            mfaVerified: true);

        var refresh = jwtIssuer.IssueRefreshToken();
        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: user.Id,
                ActiveTenantPublicId: null,
                FamilyId: Guid.NewGuid(),
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
            IsGlobalMasterAdmin: true,
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - clock.UtcNow).TotalSeconds,
            AutoSelected: false,
            RecoveryCodesRemaining: codigosRestantes));
    }

    private async Task<Result<LoginResult>> EmitirOperativaAsync(
        CentralUser user,
        ActiveMembershipInfo membresia,
        bool autoSeleccionada,
        int? codigosRestantes,
        CancellationToken ct)
    {
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: membresia.TenantId,
            tenantAdmin: membresia.IsTenantAdmin,
            mfaVerified: true);

        var refresh = jwtIssuer.IssueRefreshToken();
        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: user.Id,
                ActiveTenantPublicId: membresia.TenantId,
                FamilyId: Guid.NewGuid(),
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
            AutoSelected: autoSeleccionada,
            ActiveTenantPublicId: membresia.TenantId,
            ActiveTenantName: membresia.TenantName,
            DefaultTenantPublicId: user.DefaultTenantId,
            ActiveTenants: new[]
            {
                new ActiveTenantSummary(membresia.TenantId, membresia.TenantName, membresia.IsTenantAdmin)
            },
            RecoveryCodesRemaining: codigosRestantes));
    }
}
