using System.Net;
using System.Net.Sockets;
using IngenIA365ERP.Domain.Connectors;

namespace IngenIA365ERP.ExternalChannels.Connectors;

/// <summary>
/// La guarda de cada conexión de los conectores (feature 020, R17; FR-038; T142): el <c>ConnectCallback</c> del <c>SocketsHttpHandler</c>
/// resuelve el host y se conecta <b>sólo a las IP que acaba de revisar</b> con <see cref="DireccionSegura.EsIpPermitida"/>. Así se cierra el
/// <i>DNS rebinding</i>: la URL se validó al publicar, pero el nombre puede resolver distinto al enviar. Si alguna IP del nombre es interna o
/// reservada, no se conecta a ninguna. Con <c>Conectores:PermitirRedesPrivadas</c> (sólo pruebas; el arranque falla en Production) no
/// revisa.
/// </summary>
public static class GuardaDeDireccion
{
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Conectar(bool permitirRedesPrivadas) =>
        async (contexto, ct) =>
        {
            var host = contexto.DnsEndPoint.Host;
            var ips = IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
            if (ips.Length == 0) throw new DireccionNoPermitidaException(host);
            if (!permitirRedesPrivadas && ips.Any(ip => !DireccionSegura.EsIpPermitida(ip))) throw new DireccionNoPermitidaException(host);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(ips, contexto.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
}

/// <summary>El host resolvió a una red interna o reservada: no se envía (no es una falla de red, no se reintenta).</summary>
public sealed class DireccionNoPermitidaException(string host)
    : IOException($"«{host}» resolvió a una dirección interna o reservada: los conectores sólo hablan con direcciones públicas.");
