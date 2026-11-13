using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// La conexión de la réplica con RabbitMQ (feature 023; research R5). Una sola por réplica, con recuperación automática de la conexión y
/// de la topología. La abre <see cref="ConectorRabbitMq"/> en segundo plano y la reintenta mientras el broker no esté: <b>la API arranca y
/// vende sin broker</b> (FR-012); mientras tanto <see cref="Abierta"/> es falso y el barrido hace el trabajo. Singleton. (nuevo)
/// </summary>
public sealed class ConexionRabbitMq(IOptions<OpcionesDeMensajeria> opciones, ILogger<ConexionRabbitMq> logger) : IAsyncDisposable
{
    public const string Intercambio = "erp.trabajos";
    public const string IntercambioDeMuertos = "erp.muertos.x";

    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private IConnection? _conexion;

    /// <summary>¿Hay conexión abierta ahora?</summary>
    public bool Abierta => _conexion is { IsOpen: true };

    /// <summary>La conexión, si está abierta.</summary>
    public IConnection? Actual => Abierta ? _conexion : null;

    /// <summary>Se dispara cada vez que la conexión queda lista (la primera y tras reconectar), para que los consumidores se suscriban.</summary>
    public event Func<IConnection, CancellationToken, Task>? Lista;

    /// <summary>Intenta conectar y declarar la topología; verdadero si quedó abierta.</summary>
    public async Task<bool> ConectarAsync(CancellationToken ct)
    {
        if (Abierta) return true;
        await _cerrojo.WaitAsync(ct);
        try
        {
            if (Abierta) return true;
            var o = opciones.Value;
            var fabrica = new ConnectionFactory
            {
                HostName = o.Host,
                Port = o.Puerto,
                VirtualHost = o.VirtualHost,
                UserName = o.Usuario,
                Password = o.Clave,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
                ClientProvidedName = $"erp-api:{Environment.MachineName}",
            };
            _conexion = await fabrica.CreateConnectionAsync(ct);
            _conexion.RecoverySucceededAsync += async (_, _) =>
            {
                logger.LogInformation("[Mensajeria.Reconectada] {Host}", o.Host);
                await AvisarListaAsync(CancellationToken.None);
            };
            _conexion.ConnectionShutdownAsync += (_, e) =>
            {
                logger.LogWarning("[Mensajeria.ConexionCaida] {Host}: {Motivo}", o.Host, e.ReplyText);
                return Task.CompletedTask;
            };
            await DeclararTopologiaAsync(_conexion, o, ct);
            logger.LogInformation("[Mensajeria.Conectada] {Host}:{Puerto}{VirtualHost}", o.Host, o.Puerto, o.VirtualHost);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("[Mensajeria.SinBroker] {Host}: {Motivo}. Sigue el barrido; se reintenta.", opciones.Value.Host, ex.Message);
            await CerrarAsync();
            return false;
        }
        finally
        {
            _cerrojo.Release();
        }

        await AvisarListaAsync(ct);
        return true;
    }

    /// <summary>
    /// Declara intercambios y colas (idempotente; contracts/mensajeria.md §1): colas quorum con límite de entregas y su cola de muertos.
    /// </summary>
    public static async Task DeclararTopologiaAsync(IConnection conexion, OpcionesDeMensajeria o, CancellationToken ct)
    {
        await using var canal = await conexion.CreateChannelAsync(cancellationToken: ct);
        await canal.ExchangeDeclareAsync(Intercambio, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await canal.ExchangeDeclareAsync(IntercambioDeMuertos, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);

        await canal.QueueDeclareAsync(TiposDeAviso.ColaMuertos, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await canal.QueueBindAsync(TiposDeAviso.ColaMuertos, IntercambioDeMuertos, routingKey: string.Empty, cancellationToken: ct);

        foreach (var cola in TiposDeAviso.Colas)
        {
            await canal.QueueDeclareAsync(cola, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "quorum",
                    ["x-delivery-limit"] = o.LimiteDeEntregas,
                    ["x-dead-letter-exchange"] = IntercambioDeMuertos,
                }, cancellationToken: ct);
            await canal.QueueBindAsync(cola, Intercambio, routingKey: cola, cancellationToken: ct);
        }
    }

    private async Task AvisarListaAsync(CancellationToken ct)
    {
        if (Lista is null || _conexion is not { IsOpen: true } c) return;
        foreach (var suscriptor in Lista.GetInvocationList().Cast<Func<IConnection, CancellationToken, Task>>())
        {
            try
            {
                await suscriptor(c, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogError(ex, "[Mensajeria.SuscripcionFallida] Un consumidor no pudo suscribirse; se reintenta al reconectar.");
            }
        }
    }

    private async Task CerrarAsync()
    {
        if (_conexion is null) return;
        try
        {
            await _conexion.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Mensajeria.CierreConError]");
        }
        _conexion = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CerrarAsync();
        _cerrojo.Dispose();
    }
}

/// <summary>Abre la conexión al arrancar y la reintenta cada 10 s mientras el broker no responda. (nuevo)</summary>
public sealed class ConectorRabbitMq(ConexionRabbitMq conexion, ILogger<ConectorRabbitMq> logger) : BackgroundService
{
    private static readonly TimeSpan Reintento = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ConectorRabbitMq iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            // Tras la primera conexión, la recuperación automática del cliente se encarga; sólo se vuelve a intentar si se cerró del todo.
            if (!conexion.Abierta) await conexion.ConectarAsync(stoppingToken);
            try
            {
                await Task.Delay(Reintento, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
