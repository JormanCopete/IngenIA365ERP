using System.Text.Json;
using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// Publica avisos en RabbitMQ con confirmación del broker (feature 023; contracts/mensajeria.md §3). Un canal de publicación por réplica
/// con <c>publisherConfirmationsEnabled</c> y seguimiento: <c>BasicPublishAsync</c> termina cuando el broker confirma y lanza ante un nack,
/// un return (<c>mandatory</c>) o una conexión caída. Cualquiera de esos casos, o la confirmación que no llega en
/// <c>Mensajeria:ConfirmacionSegundos</c>, responde «no confirmado»: la orden sigue en la base y el barrido la retoma. Singleton. (nuevo)
/// </summary>
public sealed class ColaRabbitMq(ConexionRabbitMq conexion, IOptions<OpcionesDeMensajeria> opciones, ILogger<ColaRabbitMq> logger)
    : IColaDeTrabajos, IAsyncDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private IChannel? _canal;

    public bool Disponible => conexion.Abierta;

    public TimeSpan RepublicarTras => TimeSpan.FromSeconds(opciones.Value.RepublicarTrasSegundos);

    public async Task<bool> PublicarAsync(Aviso aviso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        if (conexion.Actual is not { } c) return false;

        var cola = TiposDeAviso.ColaDe(aviso.Tipo);
        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(aviso, Json);
        var propiedades = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = aviso.IdDeMensaje,
            Type = aviso.Tipo,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.SpecifyKind(aviso.EmitidoEn, DateTimeKind.Utc)).ToUnixTimeSeconds()),
        };

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.ConfirmacionSegundos)));
        try
        {
            await _cerrojo.WaitAsync(limite.Token);
            try
            {
                var canal = await CanalAsync(c, limite.Token);
                await canal.BasicPublishAsync(ConexionRabbitMq.Intercambio, cola, mandatory: true, basicProperties: propiedades, body: cuerpo,
                    cancellationToken: limite.Token);
            }
            finally
            {
                _cerrojo.Release();
            }
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("[Mensajeria.SinConfirmacion] {Tipo} {TenantPublicId} {Entidad}: el broker no confirmó a tiempo.",
                aviso.Tipo, aviso.Cooperativa, aviso.Entidad);
            await DescartarCanalAsync();
            return false;
        }
        catch (PublishException ex)
        {
            logger.LogWarning("[Mensajeria.Rechazada] {Tipo} {TenantPublicId} {Entidad}: {Return}", aviso.Tipo, aviso.Cooperativa, aviso.Entidad,
                ex.IsReturn ? "sin cola que lo reciba" : "nack del broker");
            return false;
        }
        catch (Exception ex) when (ex is AlreadyClosedException or OperationInterruptedException or BrokerUnreachableException
                                       or System.IO.IOException)
        {
            logger.LogWarning("[Mensajeria.PublicacionFallida] {Tipo} {TenantPublicId} {Entidad}: {Motivo}",
                aviso.Tipo, aviso.Cooperativa, aviso.Entidad, ex.Message);
            await DescartarCanalAsync();
            return false;
        }
    }

    private async Task<IChannel> CanalAsync(IConnection c, CancellationToken ct)
    {
        if (_canal is { IsOpen: true } abierto) return abierto;
        if (_canal is not null) await DescartarCanalAsync();
        _canal = await c.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        return _canal;
    }

    private async Task DescartarCanalAsync()
    {
        var canal = _canal;
        _canal = null;
        if (canal is null) return;
        try
        {
            await canal.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Mensajeria.CanalDescartadoConError]");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DescartarCanalAsync();
        _cerrojo.Dispose();
    }
}
