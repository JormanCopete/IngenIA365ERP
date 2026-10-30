using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Auth;

/// <summary>
/// Lo que escribió la persona en «Correo o usuario», resuelto.
/// </summary>
/// <param name="Cuenta">La identidad, si el texto corresponde a una que puede entrar con él.</param>
/// <param name="Normalizado">El texto normalizado: correo en mayúsculas o usuario de documento.</param>
/// <param name="Tipo">Con qué se identificó.</param>
/// <param name="ClaveDeIntentos">La llave del contador de intentos de contraseña (por cuenta si la hay).</param>
public sealed record IdentificadorResuelto(
    CentralUser? Cuenta,
    string Normalizado,
    LoginIdentifierKind Tipo,
    string ClaveDeIntentos);

/// <summary>
/// El único sitio que convierte «Correo o usuario» en una identidad (feature 017, research R5): con
/// <c>@</c> busca por correo y sólo sirve si está <b>verificado</b>; sin <c>@</c> normaliza y busca el
/// usuario de documento exacto. Lo usan el ingreso y la recuperación de clave; lo vigila
/// <c>NadieBuscaCuentasPorCorreoFueraDelResolutor</c>.
/// </summary>
public sealed class ResolutorDeIdentificador(ICentralIdentityProvider identidad)
{
    public async Task<IdentificadorResuelto> ResolverAsync(string? texto, CancellationToken ct)
    {
        var limpio = texto?.Trim() ?? string.Empty;

        if (limpio.Contains('@'))
        {
            var correo = limpio.ToUpperInvariant();
            var cuenta = await identidad.FindByEmailAsync(limpio, ct);
            // Un correo sin verificar no sirve para entrar (FR-012). Responde igual que uno inexistente
            // y no toca el contador de la cuenta: si lo hiciera, cualquiera podría bloquearla
            // escribiendo un correo que nadie comprobó.
            return cuenta is { EmailConfirmed: true }
                ? new IdentificadorResuelto(cuenta, correo, LoginIdentifierKind.Email, ClaveDeIntentos.DeCuenta(cuenta.Id))
                : new IdentificadorResuelto(null, correo, LoginIdentifierKind.Unknown, ClaveDeIntentos.DeIdentificador(correo));
        }

        var usuario = UsuarioDeDocumento.NormalizarIdentificador(limpio);
        var porDocumento = usuario.Length == 0 ? null : await identidad.FindByDocumentUsernameAsync(usuario, ct);
        return porDocumento is not null
            ? new IdentificadorResuelto(porDocumento, usuario, LoginIdentifierKind.DocumentUsername, ClaveDeIntentos.DeCuenta(porDocumento.Id))
            : new IdentificadorResuelto(null, usuario, LoginIdentifierKind.Unknown, ClaveDeIntentos.DeIdentificador(usuario));
    }
}
