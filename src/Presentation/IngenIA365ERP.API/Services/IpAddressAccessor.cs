using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// Lee la IP del cliente honrando los proxies estándar antes de caer al
/// <c>RemoteIpAddress</c> del socket. Para el deployment SaaS detrás de un
/// reverse proxy (Nginx/Cloudflare), el primer valor de <c>X-Forwarded-For</c>
/// es el cliente real.
/// </summary>
internal sealed class IpAddressAccessor(IHttpContextAccessor httpContextAccessor) : IIpAddressAccessor
{
    public string? IpAddress
    {
        get
        {
            var ctx = httpContextAccessor.HttpContext;
            if (ctx is null) return null;

            // X-Forwarded-For puede traer una lista "client, proxy1, proxy2"
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd))
            {
                var first = fwd.ToString().Split(',', 2)[0].Trim();
                if (!string.IsNullOrEmpty(first)) return first;
            }

            if (ctx.Request.Headers.TryGetValue("X-Real-IP", out var real))
            {
                var v = real.ToString().Trim();
                if (!string.IsNullOrEmpty(v)) return v;
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }
}
