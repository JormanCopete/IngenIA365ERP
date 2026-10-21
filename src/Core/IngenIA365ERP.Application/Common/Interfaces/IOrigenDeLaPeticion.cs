using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// De dónde viene la operación en curso, para la auditoría (feature 012, T36). En una petición:
/// la IP (la misma de <see cref="IIpAddressAccessor"/>), el User-Agent, el endpoint y el canal
/// <c>web</c>/<c>app</c> que declara la cabecera <c>X-Canal</c>. En segundo plano: canal
/// <c>Process</c>, sin IP ni User-Agent, y el origen del <c>ContextoAmbiental</c>.
///
/// <para>
/// El canal <c>Pos</c> no sale de aquí: lo decide <c>AuditBehavior</c> cuando el comando
/// implementa <c>IOperacionDePuntoDeVenta</c>.
/// </para>
/// </summary>
public interface IOrigenDeLaPeticion
{
    string? Ip { get; }

    string? UserAgent { get; }

    /// <summary><c>MÉTODO /ruta</c> en una petición; nulo en segundo plano.</summary>
    string? Endpoint { get; }

    ExecutionChannel Canal { get; }

    /// <summary>En una petición, el endpoint; en segundo plano, <c>Mensaje:{id}</c>, <c>Lote:{número}</c> o <c>Tarea:{nombre}</c>.</summary>
    string? Origen { get; }

    /// <summary>
    /// Feature 013 (T67, F10): el dispositivo que declara la aplicación MAUI en la cabecera opcional <c>X-Dispositivo</c>; nulo en el
    /// navegador y en segundo plano. Junto con el <see cref="UserAgent"/> forma el metadato <c>Device</c> del evento. Como
    /// <c>X-Canal</c>, describe y no concede nada.
    /// </summary>
    string? Dispositivo { get; }
}
