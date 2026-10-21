using System.Diagnostics;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Common.Messaging;

/// <summary>
/// El registro de los canales de mensajería (feature 013, T0083; T71; contracts/adaptadores-y-archivos.md §3.1): todos los
/// <see cref="ICanalDeMensajeria"/> del contenedor —el correo, que registra <c>AddApplicationServices</c>, y los de
/// <c>AddExternalChannels</c>—, por <c>ChannelCode</c> sin distinguir mayúsculas.
/// <list type="bullet">
/// <item>Dos adaptadores con el mismo código son un defecto de despliegue: lanza al construirse (además, la composición ya lo rechazó).</item>
/// <item>Un código sin adaptador devuelve <see cref="CanalNoInstalado"/>, que responde <c>ChannelUnavailable</c>: nunca lanza (regla 7).</item>
/// <item>SMS y WhatsApp pasan por el circuito de la cooperativa (§3.5); el correo de la plataforma no, porque no se configura por
/// cooperativa.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CanalesDeMensajeria : ICanalesDeMensajeria
{
    private static readonly ExternalChannelKind[] ClasesAtendidas = [ExternalChannelKind.Sms, ExternalChannelKind.WhatsApp];

    private readonly Dictionary<string, ICanalDeMensajeria> _porCodigo;
    private readonly ICircuitoDeCanalExterno _circuito;

    public CanalesDeMensajeria(IEnumerable<ICanalDeMensajeria> canales, ICircuitoDeCanalExterno circuito)
    {
        ArgumentNullException.ThrowIfNull(canales);
        _circuito = circuito ?? throw new ArgumentNullException(nameof(circuito));
        _porCodigo = new Dictionary<string, ICanalDeMensajeria>(StringComparer.OrdinalIgnoreCase);
        foreach (var canal in canales)
        {
            if (string.IsNullOrWhiteSpace(canal.ChannelCode))
                throw new InvalidOperationException($"El adaptador {canal.GetType().Name} no declara su ChannelCode.");
            if (!_porCodigo.TryAdd(canal.ChannelCode.Trim(), canal))
                throw new InvalidOperationException($"Dos adaptadores de mensajería declaran el canal «{canal.ChannelCode}».");
        }
        Codigos = _porCodigo.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyCollection<string> Codigos { get; }

    public IReadOnlyCollection<ExternalChannelKind> Clases => ClasesAtendidas;

    public ICanalDeMensajeria Resolver(string channelCode)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || !_porCodigo.TryGetValue(channelCode.Trim(), out var canal))
            return new CanalNoInstalado(channelCode?.Trim() ?? string.Empty);
        if (string.Equals(canal.ChannelCode, CodigosDeCanalExterno.Correo, StringComparison.OrdinalIgnoreCase)) return canal;
        return new CanalVigilado(canal, _circuito);
    }

    public IReadOnlyCollection<string> CodigosPara(ExternalChannelKind clase)
    {
        var canal = clase switch
        {
            ExternalChannelKind.Sms => MessageChannel.Sms,
            ExternalChannelKind.WhatsApp => MessageChannel.WhatsApp,
            _ => (MessageChannel?)null,
        };
        if (canal is null) return [];
        return _porCodigo.Values
            .Where(c => c.Capacidades.Atiende(canal.Value))
            .Select(c => c.ChannelCode)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ExternalChannelKind clase, string channelCode, ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Resolver(channelCode).ProbarAsync(contexto, ct);

    /// <summary>La clase del circuito para un canal (la primera que atiende entre SMS y WhatsApp).</summary>
    private static ExternalChannelKind ClaseDe(ICanalDeMensajeria canal) =>
        canal.Capacidades.Atiende(MessageChannel.Sms) ? ExternalChannelKind.Sms : ExternalChannelKind.WhatsApp;

    /// <summary>Un código sellado sin adaptador en este despliegue (§3.1 regla 7). (nuevo)</summary>
    public sealed class CanalNoInstalado(string channelCode) : ICanalDeMensajeria
    {
        public string ChannelCode { get; } = channelCode;

        public CapacidadesDeMensajeria Capacidades { get; } = new(new HashSet<MessageChannel>(), false, false, null, false);

        public Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<EnvioDeMensaje>.NoInstalado(ChannelCode));

        public Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<EstadoDeEntrega>.NoInstalado(ChannelCode));

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<bool>.NoInstalado(ChannelCode));
    }

    /// <summary>
    /// Un canal detrás del circuito (§3.5): con el circuito abierto responde <c>ChannelUnavailable</c> sin llamar al proveedor, y le avisa al
    /// circuito cada resultado. El circuito nunca lanza; si lo hiciera, la llamada sigue. (nuevo)
    /// </summary>
    private sealed class CanalVigilado(ICanalDeMensajeria canal, ICircuitoDeCanalExterno circuito) : ICanalDeMensajeria
    {
        public string ChannelCode => canal.ChannelCode;

        public CapacidadesDeMensajeria Capacidades => canal.Capacidades;

        public async Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct)
        {
            if (!await PuedeLlamarAsync(contexto, ct))
                return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, Simulado(),
                    $"El circuito del canal {ChannelCode} está abierto: no se llamó al proveedor.");
            var r = await canal.EnviarAsync(mensaje, contexto, ct);
            await circuito.RegistrarAsync(ClaseDe(canal), ChannelCode, r.Outcome, ct);
            return r;
        }

        public async Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto,
            CancellationToken ct)
        {
            if (!await PuedeLlamarAsync(contexto, ct))
                return ResultadoExterno<EstadoDeEntrega>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, Simulado(),
                    $"El circuito del canal {ChannelCode} está abierto: no se llamó al proveedor.");
            var r = await canal.ConsultarEntregaAsync(referenciaExterna, contexto, ct);
            await circuito.RegistrarAsync(ClaseDe(canal), ChannelCode, r.Outcome, ct);
            return r;
        }

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) => canal.ProbarAsync(contexto, ct);

        private Task<bool> PuedeLlamarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            circuito.PuedeLlamarAsync(ClaseDe(canal), ChannelCode, async c => (await canal.ProbarAsync(contexto, c)).Outcome, ct);

        private bool Simulado() => CodigosDeCanalExterno.EsSimulado(ChannelCode);
    }
}

/// <summary>
/// El canal de correo de la plataforma sobre <c>IEmailSender</c> (feature 013, T0083; T74; §3.2). Código <c>CORREO</c>, siempre disponible:
/// lo registra <c>AddApplicationServices</c> y no se configura por cooperativa. Cumple §3.1: una dirección inválida es <c>InvalidData</c>,
/// un tiempo agotado <c>InProcess</c> (pudo haber salido) y cualquier otra falla del envío <c>ChannelUnavailable</c>. El correo no informa
/// entregas: la consulta responde <c>Unknown</c>. El cuerpo llega ya renderizado y escapado (<c>RenderizadorDePlantillasDeMensaje</c>).
/// (nuevo)
/// </summary>
public sealed class CanalDeCorreo(Interfaces.Notifications.IEmailSender correo, Interfaces.IDateTimeService reloj) : ICanalDeMensajeria
{
    public string ChannelCode => CodigosDeCanalExterno.Correo;

    public CapacidadesDeMensajeria Capacidades { get; } =
        new(new HashSet<MessageChannel> { MessageChannel.Email }, AdmiteAdjuntos: true, ExigePlantillaDelProveedor: false, LargoMaximo: null,
            ConsultaEntrega: false);

    /// <summary>La referencia del mensaje ante el correo (no hay proveedor que la devuelva).</summary>
    public static string ReferenciaDe(Guid mensajePublicId) => $"CORREO-{mensajePublicId:N}";

    public async Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var reloj0 = Stopwatch.StartNew();
        if (!EsCorreoValido(mensaje.Destino))
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InvalidData, ChannelCode, false, "La dirección de correo no es válida.",
                duracionMs: reloj0.ElapsedMilliseconds);

        try
        {
            var adjuntos = mensaje.Adjuntos.Select(a => new Interfaces.Notifications.EmailAttachment(a.NombreDeArchivo, a.ContentType, a.Bytes)).ToList();
            await correo.SendAsync(new Interfaces.Notifications.EmailMessage(
                mensaje.Destino.Trim(), mensaje.Asunto ?? string.Empty, mensaje.Cuerpo, Attachments: adjuntos.Count == 0 ? null : adjuntos), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InProcess, ChannelCode, false,
                "El servidor de correo no respondió a tiempo: el mensaje pudo haber salido.", ReferenciaDe(mensaje.MensajePublicId),
                duracionMs: reloj0.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InvalidData, ChannelCode, false,
                $"El servidor de correo rechazó el mensaje antes de enviarlo ({ex.GetType().Name}).", duracionMs: reloj0.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, false,
                $"El correo no salió ({ex.GetType().Name}).", duracionMs: reloj0.ElapsedMilliseconds);
        }

        return ResultadoExterno<EnvioDeMensaje>.Ok(new EnvioDeMensaje(ReferenciaDe(mensaje.MensajePublicId), reloj.UtcNow), ChannelCode, false,
            ReferenciaDe(mensaje.MensajePublicId), reloj0.ElapsedMilliseconds);
    }

    public Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<EstadoDeEntrega>.Ok(
            new EstadoDeEntrega(DeliveryState.Unknown, null, "El correo no informa la entrega."), ChannelCode, false, referenciaExterna));

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, ChannelCode, false));

    private static bool EsCorreoValido(string? destino)
    {
        if (string.IsNullOrWhiteSpace(destino)) return false;
        var d = destino.Trim();
        var arroba = d.IndexOf('@', StringComparison.Ordinal);
        return arroba > 0 && arroba == d.LastIndexOf('@') && arroba < d.Length - 3 && d.IndexOf('.', arroba) > arroba + 1
               && !d.Any(char.IsWhiteSpace);
    }
}
