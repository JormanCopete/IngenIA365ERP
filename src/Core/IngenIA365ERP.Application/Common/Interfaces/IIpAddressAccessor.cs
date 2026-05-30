namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Devuelve la IP del cliente del request en curso. Implementado por el host HTTP
/// (API) leyendo <c>X-Forwarded-For</c> / <c>X-Real-IP</c> y, como fallback,
/// <c>HttpContext.Connection.RemoteIpAddress</c>. Vacío fuera de un request HTTP
/// (workers, tests).
/// </summary>
public interface IIpAddressAccessor
{
    string? IpAddress { get; }
}
