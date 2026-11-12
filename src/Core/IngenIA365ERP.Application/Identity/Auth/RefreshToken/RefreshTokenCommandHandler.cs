using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.Extensions.Options;
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
///         invalidar familia entera y rechazar. Salvo la <b>gracia de reuso</b>
///         (<c>Sesion:GraciaDeReusoSegundos</c>): si rotó hace menos que eso y su sucesor sigue
///         siendo la cabeza viva de la familia (nadie lo usó), es el mismo cliente que no recibió
///         la respuesta del canje anterior; se revoca ese sucesor y se emite otro en la misma
///         familia, con el mismo tope.</item>
///   <item>Validar que el CentralUser sigue activo y su membresía con
///         <c>session.ActiveTenantPublicId</c> también.</item>
///   <item>Emitir nuevo access + refresh con MISMA familyId.</item>
///   <item>MarkRotatedAsync(old → new) + Store(new).</item>
/// </list>
/// <para>
/// Todo rechazo queda en el registro como <c>[Sesion.RefreshRechazado] {Codigo}</c> (Warning): hasta el 2026-11-12 sólo se
/// registraba el reuso, y una sesión cortada por inactividad, por el tope o por la membresía no dejaba rastro en el servidor.
/// </para>
/// </summary>
public sealed class RefreshTokenCommandHandler(
    ICentralRefreshTokenStore refreshStore,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    IDateTimeService clock,
    IOptions<PoliticaDeSesionOptions> politica,
    ILogger<RefreshTokenCommandHandler> logger,
    Documento.IExigenciaDeDocumento exigenciaDeDocumento)
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
{

    public async Task<Result<RefreshTokenResult>> Handle(
        RefreshTokenCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Rechazo(null, "Identity.RefreshToken.Invalid", "Refresh token vacío.");
        }

        var oldHash = HashHex(request.RefreshToken);
        var session = await refreshStore.GetAsync(oldHash, ct);
        if (session is null)
        {
            // No existe, expirado, o familia invalidada — todos comparten respuesta
            // genérica para no diferenciar entre causas.
            return Rechazo(null, "Identity.RefreshToken.Invalid", "Refresh token inválido o expirado.");
        }

        var limites = politica.Value;
        var ahora = clock.UtcNow;

        // Detección de reuso — el token ya fue rotado y alguien lo intenta otra vez. Dentro de la
        // gracia, y con su sucesor sin usar, es el mismo cliente que no recibió la respuesta del canje
        // anterior (la red se cortó, la pestaña lo abortó): se le vuelve a emitir, revocando ese sucesor.
        string? sucesorARevocar = null;
        if (session.ReplacedByTokenHashHex is { } sucesor)
        {
            if (await SucesorEnGraciaAsync(session, sucesor, ahora, limites, ct))
            {
                sucesorARevocar = sucesor;
            }
            else
            {
                logger.LogWarning(
                    "Refresh token reusado detectado para CentralUser {UserId} familia {Family}. Invalidando familia.",
                    session.CentralUserId, session.FamilyId);
                await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
                return Rechazo(session,
                    "Identity.RefreshToken.Reused",
                    "Token reutilizado detectado; toda la sesión fue invalidada por seguridad.");
            }
        }

        // Los dos relojes de la sesión, ANTES de mirar al usuario o la membresía: una
        // sesión vencida no tiene nada que renovar, sea quien sea. En ninguno de los
        // dos se invalida la familia —no es un ataque, es el reloj— pero el token
        // tampoco sirve más: la key expira sola en Redis a la misma hora.

        // 1) Tope absoluto desde el ingreso.
        var vence = session.SessionExpiresAt ?? session.IssuedAt.Add(limites.DuracionMaxima);
        if (ahora >= vence)
        {
            return Rechazo(session,
                "Identity.RefreshToken.SessionExpired",
                $"La sesión alcanzó su duración máxima de {limites.DuracionMaxima.TotalHours:0} horas. " +
                "Iniciá sesión de nuevo para continuar.");
        }

        // 2) Inactividad: tiempo desde la última rotación. El servidor no ve cada
        //    petición, ve rotaciones; por eso el cliente rota sola la sesión de una
        //    pestaña activa antes de llegar aquí, y ésta es la red para la que no.
        if (ahora - session.IssuedAt > limites.Inactividad)
        {
            return Rechazo(session,
                "Identity.RefreshToken.InactivityExpired",
                $"La sesión se cerró tras {limites.Inactividad.TotalMinutes:0} minutos sin actividad. " +
                "Iniciá sesión de nuevo para continuar.");
        }

        // Verificar usuario sigue activo.
        var user = await centralIdentity.FindByIdAsync(session.CentralUserId, ct);
        if (user is null)
        {
            await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
            return Rechazo(session, "Identity.RefreshToken.Invalid", "Refresh token inválido.");
        }

        // SecurityStamp regenerado (cambio/reset de contraseña, force-mfa-reset)
        // ⇒ toda sesión emitida antes del cambio muere aquí. Un stamp null en la
        // sesión (formato previo a este campo) también se rechaza — fail-secure.
        if (!string.Equals(session.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            await refreshStore.InvalidateFamilyAsync(session.FamilyId, ct);
            return Rechazo(session, "Identity.RefreshToken.Invalid", "Refresh token inválido o expirado.");
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
                return Rechazo(session,
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
                return Rechazo(session,
                    "Tenant.MfaMethodNotAccepted",
                    $"'{current.TenantName}' cambio los metodos de segundo factor que acepta. " +
                    "Volve a iniciar sesion para inscribir uno valido.");
            }

            // Feature 017 (US5): llegada la fecha de la cooperativa, quien no registró su documento deja de
            // renovar y vuelve por el login, que le da el desafío para registrarlo. No se invalida la familia,
            // por la misma razón que arriba. Con documento no se consulta nada.
            if (await exigenciaDeDocumento.EvaluarAsync(user, [current.TenantId], ct)
                == Documento.GuardiaDeDocumento.Veredicto.FaltaDocumento)
            {
                return Rechazo(session,
                    "Identity.Document.Required", "Su cooperativa ya exige el documento de identidad para entrar. Vuelva a iniciar sesión para registrarlo.");
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
            ReplacedAt = null,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            SessionExpiresAt = vence,
        };

        // Reuso en gracia: el sucesor que el cliente nunca recibió deja de valer y el nuevo toma su
        // lugar en la familia. La rotación conserva la hora de la primera, así la gracia no se estira
        // con cada reintento.
        if (sucesorARevocar is not null)
        {
            await refreshStore.DeleteAsync(sucesorARevocar, ct);
            logger.LogInformation(
                "[Sesion.ReusoEnGracia] Refresh rotado hace {Segundos:0.#} s vuelto a canjear con su sucesor sin usar; " +
                "se revoca el sucesor y se emite otro. CentralUser {UserId} familia {Family}.",
                (ahora - (session.ReplacedAt ?? ahora)).TotalSeconds, session.CentralUserId, session.FamilyId);
        }

        await refreshStore.StoreAsync(newRefresh.HashHex, newSession, vence - ahora, ct);
        await refreshStore.MarkRotatedAsync(oldHash, newRefresh.HashHex, session.ReplacedAt ?? ahora, ct);

        return Result.Success(new RefreshTokenResult(
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: newRefresh.Token,
            // Lo que el cliente usa para avisar «tu sesión termina en…»: el tope, no
            // el vencimiento nominal del token recién acuñado.
            RefreshTokenExpiresAt: vence,
            ExpiresInSeconds: (int)(access.ExpiresAt - ahora).TotalSeconds));
    }

    /// <summary>
    /// ¿Es este reuso el del cliente que no recibió la respuesta? Sí si la gracia está encendida, el token rotó hace menos que
    /// ella (con la hora anotada: los registros anteriores al campo no tienen gracia) y su sucesor sigue siendo la cabeza viva de
    /// la misma familia —existe y no rotó—. Si el sucesor ya se usó, otro tiene el token: es reuso de verdad.
    /// </summary>
    private async Task<bool> SucesorEnGraciaAsync(
        CentralRefreshSession session, string sucesor, DateTime ahora, PoliticaDeSesionOptions limites, CancellationToken ct)
    {
        if (limites.GraciaDeReuso <= TimeSpan.Zero || session.ReplacedAt is not { } rotadoEn) return false;
        if (ahora - rotadoEn >= limites.GraciaDeReuso) return false;
        var cabeza = await refreshStore.GetAsync(sucesor, ct);
        return cabeza is not null && cabeza.FamilyId == session.FamilyId && cabeza.ReplacedByTokenHashHex is null;
    }

    /// <summary>El rechazo, con su código en el registro: <c>[Sesion.RefreshRechazado]</c>.</summary>
    private Result<RefreshTokenResult> Rechazo(CentralRefreshSession? session, string codigo, string mensaje)
    {
        logger.LogWarning(
            "[Sesion.RefreshRechazado] {Codigo} CentralUser {UserId} familia {Family}",
            codigo, session?.CentralUserId, session?.FamilyId);
        return Result.Failure<RefreshTokenResult>(codigo, mensaje);
    }

    private static string HashHex(string plainToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }
}
