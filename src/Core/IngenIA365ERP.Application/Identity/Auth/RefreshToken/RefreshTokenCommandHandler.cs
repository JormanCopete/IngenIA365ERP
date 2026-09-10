using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

using IngenIA365ERP.Application.Identity.Auth.Common;

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
    /// <summary>
    /// Duración máxima de una sesión desde el ingreso. Es lo que el cliente muestra
    /// como «tu sesión termina en…» y lo que este handler hace cumplir: pasado el
    /// tope no hay rotación posible y hay que volver por el login.
    /// </summary>
    public static readonly TimeSpan DuracionMaximaDeSesion = TimeSpan.FromHours(12);

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

        // El tope absoluto de la sesión. Se comprueba ANTES de mirar al usuario o la
        // membresía: una sesión vencida no tiene nada que renovar, sea quien sea.
        // No se invalida la familia —no es un ataque, es el reloj— pero el token
        // tampoco sirve más: la key expira sola en Redis a la misma hora.
        var ahora = clock.UtcNow;
        var vence = session.SessionExpiresAt ?? session.IssuedAt.Add(DuracionMaximaDeSesion);
        if (ahora >= vence)
        {
            return Result.Failure<RefreshTokenResult>(
                "Identity.RefreshToken.SessionExpired",
                $"La sesión alcanzó su duración máxima de {DuracionMaximaDeSesion.TotalHours:0} horas. " +
                "Iniciá sesión de nuevo para continuar.");
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

            // La politica de metodos se REEVALUA en cada refresh, y esto es lo que
            // hace que llegue a morder antes del tope: el access dura quince minutos
            // y la sesion hasta doce horas, asi que sin esta comprobacion una sesion
            // emitida antes de que la cooperativa restringiera metodos seguiria
            // renovandose el resto del dia y la politica no afectaria a quien ya
            // estaba dentro — que suele ser todo el mundo.
            //
            // No se invalida la familia: no es un token robado ni una sesion
            // ilegitima, es alguien a quien le cambiaron las reglas mientras
            // trabajaba. Se le corta la renovacion y vuelve por el login, donde el
            // emisor le dara el token de inscripcion y le dira que inscribir.
            if (GuardiaDeMetodos.Evaluar(
                    current.IsMfaRequiredByTenant, current.MetodosAceptados, session.MetodoMfa)
                != GuardiaDeMetodos.Veredicto.Admite)
            {
                return Result.Failure<RefreshTokenResult>(
                    "Tenant.MfaMethodNotAccepted",
                    $"'{current.TenantName}' cambio los metodos de segundo factor que acepta. " +
                    "Volve a iniciar sesion para inscribir uno valido.");
            }
        }

        // Emitir nuevos tokens — preservando la familyId del refresh anterior.
        var access = jwtIssuer.IssueAccessToken(
            centralUserId: user.Id,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: session.ActiveTenantPublicId,
            tenantAdmin: membershipAdmin,
            mfaVerified: user.TwoFactorEnabled,
            // Se arrastra el metodo de la sesion original: el refresh no vuelve a
            // demostrar nada, solo prolonga lo que ya se demostro.
            metodoMfa: session.MetodoMfa);

        // El refresh nuevo hereda el tope: mismo vencimiento, TTL de lo que quede.
        // Antes iba un TTL fijo de doce horas y por eso la sesión se deslizaba.
        var newRefresh = jwtIssuer.IssueRefreshToken();
        var newSession = session with
        {
            IssuedAt = ahora,
            ReplacedByTokenHashHex = null,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            SessionExpiresAt = vence,
        };

        await refreshStore.StoreAsync(newRefresh.HashHex, newSession, vence - ahora, ct);
        await refreshStore.MarkRotatedAsync(oldHash, newRefresh.HashHex, ct);

        return Result.Success(new RefreshTokenResult(
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: newRefresh.Token,
            // Lo que el cliente usa para avisar «tu sesión termina en…»: el tope, no
            // el vencimiento nominal del token recién acuñado.
            RefreshTokenExpiresAt: vence,
            ExpiresInSeconds: (int)(access.ExpiresAt - ahora).TotalSeconds));
    }

    private static string HashHex(string plainToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }
}
