using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// La IP del visitante, en este orden: <c>CF-Connecting-IP</c>, el primer valor de
/// <c>X-Forwarded-For</c>, y por ultimo <c>Connection.RemoteIpAddress</c>.
///
/// <para>
/// El orden no es de gusto. En produccion el trafico entra SOLO por el tunel de
/// Cloudflare (cloudflared → Traefik → API) y Cloudflare pone en
/// <c>CF-Connecting-IP</c> la IP del visitante; un cliente no puede falsificarla
/// porque el origen no es alcanzable sin pasar por el borde que la sobrescribe.
/// <c>X-Forwarded-For</c> queda para un despliegue detras de otro proxy que la
/// reescriba; su primer valor solo vale lo que valga ese proxy, porque el
/// cliente puede traerla puesta. <c>RemoteIpAddress</c> ya viene corregida por
/// <c>UseForwardedHeaders</c>, el primer middleware, asi que en el tunel las tres
/// coinciden y en local, sin proxies, es el socket.
/// </para>
///
/// <para>
/// Ya no se lee <c>X-Real-IP</c>. Hecho medido en produccion: Traefik la pone con
/// la IP de su par, no la del visitante, y toda la tabla de intentos de ingreso
/// tenia la misma IP interna (10.42.0.25, el pod de cloudflared). Cualquier
/// limite o bloqueo «por IP» era en realidad para toda la plataforma junta.
/// </para>
///
/// <para>
/// Sin petición —un trabajo de fondo con <c>ContextoAmbiental</c> (feature 012, T5)— es
/// <b>nula</b>, a propósito: el proceso no tiene IP, y ponerle la del servidor la haría pasar por la
/// de una persona en la auditoría.
/// </para>
/// </summary>
internal sealed class IpAddressAccessor(IHttpContextAccessor httpContextAccessor) : IIpAddressAccessor
{
    public string? IpAddress
    {
        get
        {
            var ctx = httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cf))
            {
                var v = cf.ToString().Trim();
                if (!string.IsNullOrEmpty(v)) return v;
            }

            // X-Forwarded-For puede traer una lista "cliente, proxy1, proxy2".
            // UseForwardedHeaders ya consumio los saltos conocidos; lo que queda es
            // lo que ningun proxy conocido puso.
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd))
            {
                var first = fwd.ToString().Split(',', 2)[0].Trim();
                if (!string.IsNullOrEmpty(first)) return first;
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }
}
