using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.Common;

/// <summary>
/// Lo que pasa cuando alguien acaba de inscribir su PRIMER autenticador viniendo
/// de una inscripción forzada: tenía un token de <c>purpose=mfa-enroll</c>, que no
/// sirve para nada más, y ahora ya cumple lo que su cooperativa exige. Se le emite
/// la sesión ahí mismo para que no tenga que volver a escribir la contraseña.
///
/// <para>
/// No es lo mismo que <c>IEmisorDeSesionTrasSegundoFactor</c>, aunque se parezcan.
/// Aquel corre tras <b>demostrar</b> el segundo factor durante un ingreso, y sabe
/// devolver un desafío de selección de cooperativa o la salida del maestro global.
/// Éste corre tras <b>inscribirlo</b>, y es deliberadamente más corto: si la
/// persona tiene más de una cooperativa, no eleva nada y el cliente vuelve por el
/// login. Fusionarlos escondería que son dos momentos distintos con dos
/// respuestas distintas.
/// </para>
///
/// <para>
/// Existe porque la inscripción con passkey necesita exactamente lo mismo que la
/// inscripción con código. Sin esto, quien no pueda usar una app de autenticación
/// —que es una de las razones por las que hay passkeys— quedaría obligado a usar
/// una para poder entrar cuando su cooperativa exige segundo factor.
/// </para>
/// </summary>
public interface IElevadorDeSesionTrasInscripcion
{
    /// <summary>
    /// Devuelve <c>null</c> cuando no se puede elevar: la persona ya no está, o
    /// tiene un número de cooperativas distinto de una. No es un error — el
    /// autenticador quedó inscrito igual, que es lo que se pidió.
    /// </summary>
    Task<SesionElevada?> ElevarAsync(Guid centralUserId, DateTime ahora, CancellationToken ct);
}

public sealed record SesionElevada(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    Guid ActiveTenantPublicId,
    string ActiveTenantName);

public sealed class ElevadorDeSesionTrasInscripcion(
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    ILogger<ElevadorDeSesionTrasInscripcion> logger)
    : IElevadorDeSesionTrasInscripcion
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<SesionElevada?> ElevarAsync(
        Guid centralUserId, DateTime ahora, CancellationToken ct)
    {
        var usuario = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (usuario is null)
        {
            // Desapareció justo después de inscribir. El autenticador quedó
            // guardado; lo único que se pierde es el atajo.
            logger.LogWarning(
                "CentralUser {Id} desapareció tras inscribir su autenticador.", centralUserId);
            return null;
        }

        var activas = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        if (activas.Count != 1)
        {
            // Con varias cooperativas hace falta elegir una, y esa pantalla se
            // alimenta de un token de selección que aquí no se emite. Con cero,
            // no hay ninguna sesión que emitir. En ambos casos el cliente
            // reinicia el ingreso.
            return null;
        }

        var membresia = activas[0];

        var access = jwtIssuer.IssueAccessToken(
            centralUserId: centralUserId,
            email: usuario.Email,
            isGlobalMasterAdmin: usuario.IsGlobalMasterAdmin,
            activeTenantId: membresia.TenantId,
            tenantAdmin: membresia.IsTenantAdmin,
            mfaVerified: true);

        var refresh = jwtIssuer.IssueRefreshToken();

        await refreshStore.StoreAsync(
            refresh.HashHex,
            new CentralRefreshSession(
                CentralUserId: centralUserId,
                ActiveTenantPublicId: membresia.TenantId,
                FamilyId: Guid.NewGuid(),
                IssuedAt: ahora,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null,
                SecurityStamp: usuario.SecurityStamp),
            RefreshTokenTtl, ct);

        return new SesionElevada(
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ActiveTenantPublicId: membresia.TenantId,
            ActiveTenantName: membresia.TenantName);
    }
}
