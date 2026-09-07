using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.LogoutAll;

/// <summary>
/// Cierra TODAS las sesiones de quien llama, en todos sus dispositivos.
///
/// <para>
/// <b>Por qué existe.</b> Es el paso 4 del procedimiento de rotación de claves
/// RSA ante sospecha de fuga (<c>docs/operaciones/runbook-fase0.md</c>), y la
/// identidad central no tiene otro: <c>/api/auth/logout</c> revoca una sola
/// sesión. Su cliente es una persona operando con <c>curl</c>, que es por lo que
/// ninguna búsqueda de llamadores lo encuentra y por poco se retira por muerto.
/// </para>
///
/// <para>
/// <b>Estaba roto.</b> Leía <c>ICurrentUserService.UserId</c>, que sale del
/// claim <c>uid</c> —un entero de Fase 0— o de <c>NameIdentifier</c> pasado por
/// <c>int.TryParse</c>. El emisor central no pone <c>uid</c> y su <c>sub</c> es
/// un GUID, así que con cualquier sesión de hoy devolvía «No autenticado». Un
/// endpoint de contención de incidentes que no responde justo cuando hay una
/// sospecha de fuga es peor que no tenerlo: se descubre en el peor momento.
/// </para>
///
/// <para>
/// Ahora rota el <c>SecurityStamp</c>. No recorre sesiones una por una porque no
/// hace falta: el refresh compara el stamp guardado en la sesión contra el
/// actual del usuario, así que un stamp nuevo invalida todas de golpe —incluidas
/// las que ya estaban emitidas y las que no conocemos—. Y no toca la contraseña:
/// cerrar sesiones y cambiar credenciales son dos decisiones distintas.
/// </para>
/// </summary>
public sealed class LogoutAllCommandHandler(
    ICurrentCentralUserContext usuarioActual,
    ICentralIdentityProvider identidadCentral)
    : IRequestHandler<LogoutAllCommand, Result>
{
    public async Task<Result> Handle(LogoutAllCommand request, CancellationToken ct)
    {
        if (usuarioActual.CentralUserId is null || !usuarioActual.IsAuthenticated)
        {
            return Result.Failure("Identity.Unauthenticated", "No autenticado.");
        }

        var rotado = await identidadCentral.RotateSecurityStampAsync(
            usuarioActual.CentralUserId.Value, ct);

        return rotado
            ? Result.Success()
            : Result.Failure("Generic.NotFound", "La cuenta no existe o está dada de baja.");
    }
}
