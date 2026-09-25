using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IOrigenDeLaPeticion"/> de la API (feature 012, T36, T042). Singleton como los demás
/// accesores: no guarda nada, relee la petición en cada acceso.
///
/// <para>
/// El canal lo declara el cliente con <c>X-Canal</c> (<c>CanalDeOrigenHandler</c> en los tres
/// anfitriones): <c>app</c> es la aplicación MAUI, y cualquier otra cosa —ausente, <c>web</c>, o un
/// valor desconocido— es <c>web</c>. La cabecera no concede nada: sólo describe, para la auditoría,
/// por dónde entró la operación, así que un cliente que mienta no gana ningún permiso. <c>pos</c>
/// no se acepta por cabecera: lo decide <c>AuditBehavior</c> por el tipo del comando.
/// </para>
/// </summary>
internal sealed class OrigenDeLaPeticion(IHttpContextAccessor accessor, IIpAddressAccessor ip) : IOrigenDeLaPeticion
{
    public const string CabeceraDeCanal = "X-Canal";

    private HttpContext? Peticion => accessor.HttpContext;

    public string? Ip => Peticion is null ? null : ip.IpAddress;

    public string? UserAgent
    {
        get
        {
            var valor = Peticion?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }
    }

    public string? Endpoint => Peticion is { } http ? $"{http.Request.Method} {http.Request.Path}" : null;

    public ExecutionChannel Canal => Peticion is { } http ? CanalDeLaCabecera(http) : ExecutionChannel.Process;

    public string? Origen => Peticion is null ? ContextoAmbiental.Origen : Endpoint;

    internal static ExecutionChannel CanalDeLaCabecera(HttpContext http) =>
        string.Equals(http.Request.Headers[CabeceraDeCanal].ToString().Trim(), "app", StringComparison.OrdinalIgnoreCase)
            ? ExecutionChannel.App
            : ExecutionChannel.Web;
}
