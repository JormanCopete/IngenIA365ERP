using System.Diagnostics;
using System.Text;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Connectors;
using IngenIA365ERP.ExternalChannels.Circuit;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.ExternalChannels.Connectors;

/// <summary>
/// La única salida HTTP de los conectores (feature 020, R16, R17; T141). Usa el cliente con nombre <see cref="NombreDelCliente"/> —sin
/// redirecciones, sin cookies, sin reintentos, conexión en 5 s y la guarda de direcciones en cada conexión— con el tiempo total de la
/// definición. Lleva un circuito por cooperativa y conector sobre <see cref="EstadoDeLosCircuitosExternos"/>: tras
/// <see cref="UmbralDeFallas"/> fallas seguidas (red, tiempo agotado o 5xx) deja de llamar y responde código 0, y cada cinco minutos deja
/// pasar una llamada de sondeo; la primera respuesta que no es falla lo cierra. <b>Nunca lanza</b> por la red y <b>nunca registra</b>
/// cabeceras ni cuerpos: sólo el host, el código y la duración.
/// </summary>
internal sealed class EjecutorHttpDeConector(
    IHttpClientFactory fabrica,
    EstadoDeLosCircuitosExternos circuitos,
    ICurrentTenantService tenant,
    IDateTimeService reloj,
    ILogger<EjecutorHttpDeConector> logger) : IEjecutorDeConector
{
    public const string NombreDelCliente = "conectores";
    public const int UmbralDeFallas = 5;
    private const int LimiteDelCuerpo = 64 * 1024;

    public async Task<RespuestaHttp> EjecutarAsync(SolicitudHttp solicitud, CancellationToken ct)
    {
        var cooperativa = CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty;
        var canal = "CON:" + solicitud.ClaveDeCircuito;
        var ahora = reloj.UtcNow;
        var estado = circuitos.Leer(cooperativa, canal);
        if (estado.AbiertoDesde is not null)
        {
            if (estado.UltimoSondeo is { } ultimo && ahora - ultimo < EstadoDeLosCircuitosExternos.IntervaloDeSondeo)
                return new RespuestaHttp(0, null, 0, "El sistema externo falló varias veces seguidas: se vuelve a intentar en unos minutos.");
            circuitos.AnotarSondeo(cooperativa, canal, ahora);
        }

        var reloj2 = Stopwatch.StartNew();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(solicitud.SegundosDeEspera, 1, 60)));
        try
        {
            using var mensaje = new HttpRequestMessage(new HttpMethod(solicitud.Metodo), solicitud.Url);
            if (solicitud.Cuerpo is not null) mensaje.Content = new StringContent(solicitud.Cuerpo, Encoding.UTF8, "application/json");
            foreach (var (nombre, valor) in solicitud.Cabeceras)
                if (!mensaje.Headers.TryAddWithoutValidation(nombre, valor)) mensaje.Content?.Headers.TryAddWithoutValidation(nombre, valor);
            mensaje.Headers.Accept.ParseAdd("application/json");

            using var respuesta = await fabrica.CreateClient(NombreDelCliente).SendAsync(mensaje, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            var cuerpo = await LeerAsync(respuesta.Content, limite.Token);
            reloj2.Stop();
            var codigo = (int)respuesta.StatusCode;
            Anotar(cooperativa, canal, falla: codigo >= 500, ahora);
            logger.LogInformation("[Conectores.Llamada] {Host} respondió {Codigo} en {Ms} ms.", solicitud.Url.Host, codigo, reloj2.ElapsedMilliseconds);
            return new RespuestaHttp(codigo, cuerpo, (int)reloj2.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Anotar(cooperativa, canal, falla: true, ahora);
            logger.LogWarning("[Conectores.TiempoAgotado] {Host} no respondió en {Segundos} s.", solicitud.Url.Host, solicitud.SegundosDeEspera);
            return new RespuestaHttp(0, null, (int)reloj2.ElapsedMilliseconds, $"El sistema externo no respondió en {solicitud.SegundosDeEspera} s.");
        }
        catch (HttpRequestException ex) when (Bloqueada(ex) is { } bloqueo)
        {
            logger.LogWarning("[Conectores.DireccionBloqueada] {Host}: {Motivo}", solicitud.Url.Host, bloqueo.Message);
            return new RespuestaHttp(0, null, (int)reloj2.ElapsedMilliseconds, bloqueo.Message, DireccionBloqueada: true);
        }
        catch (HttpRequestException ex)
        {
            Anotar(cooperativa, canal, falla: true, ahora);
            logger.LogWarning("[Conectores.SinRed] {Host}: {Error}", solicitud.Url.Host, ex.HttpRequestError);
            return new RespuestaHttp(0, null, (int)reloj2.ElapsedMilliseconds, $"No se pudo conectar con el sistema externo ({ex.HttpRequestError}).");
        }
    }

    private void Anotar(Guid cooperativa, string canal, bool falla, DateTime ahora)
    {
        if (!falla)
        {
            circuitos.Cerrar(cooperativa, canal);
            return;
        }
        var estado = circuitos.SumarFalla(cooperativa, canal);
        if (estado.AbiertoDesde is null && estado.Fallas >= UmbralDeFallas) circuitos.Abrir(cooperativa, canal, ahora);
    }

    private static DireccionNoPermitidaException? Bloqueada(Exception? ex)
    {
        for (var actual = ex; actual is not null; actual = actual.InnerException)
            if (actual is DireccionNoPermitidaException d) return d;
        return null;
    }

    private static async Task<string?> LeerAsync(HttpContent contenido, CancellationToken ct)
    {
        await using var flujo = await contenido.ReadAsStreamAsync(ct);
        var buffer = new byte[LimiteDelCuerpo];
        var leidos = 0;
        int n;
        while (leidos < buffer.Length && (n = await flujo.ReadAsync(buffer.AsMemory(leidos), ct)) > 0) leidos += n;
        return leidos == 0 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, leidos);
    }
}
