using System.Text.Json;
using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// Consume las tres colas de trabajo (feature 023; contracts/mensajeria.md §4). Por cola, un canal con <c>basic.qos</c> =
/// <c>Mensajeria:Prefetch</c> y <c>Mensajeria:ConcurrenciaPorCola</c> manejadores simultáneos. Cada aviso pasa por
/// <see cref="ConsumoDeAvisos"/> y se confirma <b>después</b> de que el comando guardó (el ack manual va al final): ilegible →
/// <c>reject</c> sin reencolar (lo aparta a <c>erp.muertos</c>); el manejador lanzó → <c>nack</c> con reencolar (el quorum cuenta las
/// entregas y a la quinta lo aparta); lo demás → <c>ack</c>. Se vuelve a suscribir cada vez que la conexión se recupera. (nuevo)
/// </summary>
public sealed class ConsumidorRabbitMq(
    ConexionRabbitMq conexion,
    ConsumoDeAvisos consumo,
    IMedidorDeTrabajos medidor,
    IOptions<OpcionesDeMensajeria> opciones,
    ILogger<ConsumidorRabbitMq> logger,
    Func<bool>? baseLista = null) : BackgroundService
{
    private readonly List<IChannel> _canales = [];
    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private CancellationToken _parada;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.Consumir) return;
        _parada = stoppingToken;
        while (baseLista is not null && !baseLista())
        {
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }

        conexion.Lista += SuscribirAsync;
        if (conexion.Actual is { } c) await SuscribirAsync(c, stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            conexion.Lista -= SuscribirAsync;
            await CerrarCanalesAsync();
        }
    }

    private async Task SuscribirAsync(IConnection c, CancellationToken ct)
    {
        await _cerrojo.WaitAsync(ct);
        try
        {
            await CerrarCanalesAsync();
            var o = opciones.Value;
            foreach (var cola in TiposDeAviso.Colas)
            {
                var canal = await c.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: o.ConcurrenciaPorCola), ct);
                await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: o.Prefetch, global: false, cancellationToken: ct);
                var consumidor = new AsyncEventingBasicConsumer(canal);
                var nombre = cola;
                consumidor.ReceivedAsync += (_, entrega) => AtenderAsync(canal, nombre, entrega);
                await canal.BasicConsumeAsync(cola, autoAck: false, consumer: consumidor, cancellationToken: ct);
                _canales.Add(canal);
            }
            logger.LogInformation("[Mensajeria.Suscrito] {Colas} prefetch {Prefetch}, {Concurrencia} por cola.",
                string.Join(", ", TiposDeAviso.Colas), o.Prefetch, o.ConcurrenciaPorCola);
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    private async Task AtenderAsync(IChannel canal, string cola, BasicDeliverEventArgs entrega)
    {
        Aviso? aviso = null;
        try
        {
            aviso = JsonSerializer.Deserialize<Aviso>(entrega.Body.Span, ColaRabbitMq.Json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[Mensajeria.AvisoIlegible] {Cola} {MessageId}", cola, entrega.BasicProperties.MessageId);
        }

        if (aviso is null)
        {
            medidor.Consumo(cola, "ilegible");
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, cancellationToken: CancellationToken.None);
            return;
        }

        try
        {
            var r = await consumo.ConsumirAsync(aviso, _parada);
            medidor.Consumo(cola, ConsumidorEnProceso.Resultado(r));
            if (r == ResultadoDeConsumo.Ilegible)
                await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, cancellationToken: CancellationToken.None);
            else
                await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            medidor.Consumo(cola, "reencolado");
            logger.LogError(ex, "[Mensajeria.ConsumoFallido] {Cola} {Tipo} {TenantPublicId} {Entidad} (entrega {Reentregado}): vuelve a la cola.",
                cola, aviso.Tipo, aviso.Cooperativa, aviso.Entidad, entrega.Redelivered);
            if (canal.IsOpen)
                await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true, cancellationToken: CancellationToken.None);
        }
    }

    private async Task CerrarCanalesAsync()
    {
        foreach (var canal in _canales)
        {
            try
            {
                await canal.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[Mensajeria.CanalCerradoConError]");
            }
        }
        _canales.Clear();
    }
}
