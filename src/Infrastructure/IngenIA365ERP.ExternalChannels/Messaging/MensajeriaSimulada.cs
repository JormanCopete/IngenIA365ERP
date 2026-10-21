using System.Collections.Concurrent;
using System.Diagnostics;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ExternalChannels.Messaging;

/// <summary>
/// Lo que el canal simulado de mensajería recuerda por cooperativa (§3.6: «recuerda lo que respondió para que la consulta siguiente sea
/// coherente»): cada mensaje aceptado, con el dígito que decidió su entrega. Singleton del proceso. (nuevo)
/// </summary>
public sealed class MemoriaDeMensajeriaSimulada
{
    private readonly ConcurrentDictionary<(Guid Cooperativa, string Referencia), MensajeRecordado> _mensajes = new();

    internal MensajeRecordado Recordar(Guid cooperativa, string referencia, char? digito, DateTimeOffset aceptadoEn) =>
        _mensajes.GetOrAdd((cooperativa, referencia), _ => new MensajeRecordado(digito, aceptadoEn));

    internal MensajeRecordado? Buscar(Guid cooperativa, string referencia) =>
        _mensajes.TryGetValue((cooperativa, referencia), out var m) ? m : null;

    /// <summary>¿El simulado tiene este mensaje de esta cooperativa? (para las pruebas). (nuevo)</summary>
    public bool Conoce(Guid cooperativa, string referencia) => _mensajes.ContainsKey((cooperativa, referencia));

    internal sealed record MensajeRecordado(char? Digito, DateTimeOffset AceptadoEn);
}

/// <summary>
/// El canal <c>SIMULADO</c> de mensajería (feature 013, T0081; T71; contracts/adaptadores-y-archivos.md §3.6, §3.7; DT §2.22). Atiende SMS y
/// WhatsApp, existe en todo ambiente y marca todo como simulado (<c>Simulado = true</c>, referencias <c>SIM-…</c>): en producción la guardia
/// le quita el efecto legal y el mensaje no sale (T73). Decide por el <b>último dígito del documento</b> del titular
/// (<see cref="MensajeSaliente.DocumentoDelTitular"/>; sin él, el del destino):
/// <list type="table">
/// <item><term>1</term><description><c>InvalidData</c> (número o correo inválido).</description></item>
/// <item><term>2</term><description><c>Ok</c>, y <c>Delivered</c> en la consulta siguiente.</description></item>
/// <item><term>3</term><description><c>Ok</c>, y <c>Failed</c> en la consulta siguiente.</description></item>
/// <item><term>5</term><description><c>ChannelUnavailable</c> (no salió).</description></item>
/// <item><term>otro</term><description><c>Ok</c> y <c>Delivered</c>.</description></item>
/// </list>
/// Enviar dos veces el mismo <c>MensajePublicId</c> devuelve la misma referencia («ya existe» no es error). Con un retardo
/// (<c>ExternalChannels:Channels:SIMULADO:DelayMilliseconds</c>) mayor que el tiempo total, el envío queda <c>InProcess</c> —el proveedor no
/// contestó a tiempo, pero pudo haberlo recibido: queda recordado y la consulta lo resuelve— y la consulta, <c>ChannelUnavailable</c>.
/// (nuevo)
/// </summary>
public sealed class MensajeriaSimulada(IOptions<ExternalChannelsOptions> opciones, MemoriaDeMensajeriaSimulada memoria, TimeProvider? reloj = null)
    : ICanalDeMensajeria
{
    public const string Codigo = CodigosDeCanalExterno.Simulado;

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public string ChannelCode => Codigo;

    public CapacidadesDeMensajeria Capacidades { get; } =
        new(new HashSet<MessageChannel> { MessageChannel.Sms, MessageChannel.WhatsApp }, AdmiteAdjuntos: false, ExigePlantillaDelProveedor: false,
            LargoMaximo: null, ConsultaEntrega: true);

    /// <summary>La referencia simulada de un mensaje.</summary>
    public static string ReferenciaDe(Guid mensajePublicId) => $"SIM-{mensajePublicId:N}";

    public async Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(MensajeSaliente mensaje, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        var referencia = ReferenciaDe(mensaje.MensajePublicId);

        // «Ya existe» no es un error: lo que se aceptó antes se devuelve igual.
        if (memoria.Buscar(contexto.TenantPublicId, referencia) is { } previo)
            return Aceptado(referencia, previo.AceptadoEn, cronometro);

        var digito = UltimoDigito(mensaje.DocumentoDelTitular) ?? UltimoDigito(mensaje.Destino);
        switch (digito)
        {
            case '1':
                return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InvalidData, Codigo, true, "SIMULADO: el destino no es válido.",
                    codigoDelProveedor: "SIM-1", duracionMs: cronometro.ElapsedMilliseconds);
            case '5':
                return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true, "SIMULADO: el canal no está disponible.",
                    codigoDelProveedor: "SIM-5", duracionMs: cronometro.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(mensaje.Destino))
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InvalidData, Codigo, true, "SIMULADO: el mensaje no tiene destino.",
                duracionMs: cronometro.ElapsedMilliseconds);

        // El simulado «envía» antes de esperar: si el retardo pasa el tiempo total, pudo haber llegado.
        var recordado = memoria.Recordar(contexto.TenantPublicId, referencia, digito, _reloj.GetUtcNow());
        if (!await EsperarAsync(ct))
            return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.InProcess, Codigo, true,
                "SIMULADO: el canal no respondió a tiempo; el mensaje pudo haber llegado.", referencia, duracionMs: cronometro.ElapsedMilliseconds);

        return Aceptado(referencia, recordado.AceptadoEn, cronometro);
    }

    public async Task<ResultadoExterno<EstadoDeEntrega>> ConsultarEntregaAsync(string referenciaExterna, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        if (!await EsperarAsync(ct))
            return ResultadoExterno<EstadoDeEntrega>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true,
                "SIMULADO: el canal no respondió a tiempo.", referenciaExterna, duracionMs: cronometro.ElapsedMilliseconds);

        if (string.IsNullOrWhiteSpace(referenciaExterna) || memoria.Buscar(contexto.TenantPublicId, referenciaExterna) is not { } mensaje)
            return ResultadoExterno<EstadoDeEntrega>.Sin(ExternalOutcome.NotFound, Codigo, true, "SIMULADO: el mensaje no existe en este canal.",
                referenciaExterna, duracionMs: cronometro.ElapsedMilliseconds);

        var estado = mensaje.Digito == '3'
            ? new EstadoDeEntrega(DeliveryState.Failed, _reloj.GetUtcNow(), "SIMULADO: el operador no pudo entregarlo.")
            : new EstadoDeEntrega(DeliveryState.Delivered, _reloj.GetUtcNow(), null);
        return ResultadoExterno<EstadoDeEntrega>.Ok(estado, Codigo, true, referenciaExterna, cronometro.ElapsedMilliseconds);
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, Codigo, true));

    private ResultadoExterno<EnvioDeMensaje> Aceptado(string referencia, DateTimeOffset aceptadoEn, Stopwatch cronometro) =>
        ResultadoExterno<EnvioDeMensaje>.Ok(new EnvioDeMensaje(referencia, aceptadoEn), Codigo, true, referencia, cronometro.ElapsedMilliseconds);

    /// <summary>El retardo configurado, hasta el tiempo total: falso si el retardo lo pasa (el canal «no contestó»).</summary>
    private async Task<bool> EsperarAsync(CancellationToken ct)
    {
        var canal = opciones.Value.Canal(Codigo);
        if (canal.DelayMilliseconds <= 0) return true;
        var espera = Math.Min(canal.DelayMilliseconds, canal.TotalTimeoutMilliseconds);
        await Task.Delay(espera, ct);
        return canal.DelayMilliseconds <= canal.TotalTimeoutMilliseconds;
    }

    internal static char? UltimoDigito(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return null;
        for (var i = texto.Length - 1; i >= 0; i--)
            if (char.IsAsciiDigit(texto[i])) return texto[i];
        return null;
    }
}
