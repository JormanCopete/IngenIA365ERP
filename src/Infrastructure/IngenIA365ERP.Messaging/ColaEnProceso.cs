using System.Threading.Channels;
using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// La cola sin broker (feature 023; research R5): un <see cref="Channel{T}"/> acotado por réplica. Sirve en desarrollo, en las pruebas y
/// como modo explícito cuando el broker no está desplegado. Un aviso perdido (réplica que se reinicia, cola llena) no pierde trabajo: la
/// orden está en la base y el barrido la retoma. Llena, <see cref="PublicarAsync"/> responde «no confirmado». (nuevo)
/// </summary>
public sealed class ColaEnProceso(IOptions<OpcionesDeMensajeria> opciones) : IColaDeTrabajos
{
    public TimeSpan RepublicarTras => TimeSpan.FromSeconds(opciones.Value.RepublicarTrasSegundos);

    public const int Capacidad = 4096;

    private readonly Channel<Aviso> _canal = Channel.CreateBounded<Aviso>(new BoundedChannelOptions(Capacidad)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false,
    });

    public ChannelReader<Aviso> Avisos => _canal.Reader;

    /// <summary>
    /// Sólo si esta réplica consume (<c>Mensajeria:Consumir</c>): un canal en proceso sin lector perdería el aviso y el barrido creería que el
    /// trabajo quedó en manos de la cola. Sin consumidor, el barrido hace el trabajo él mismo.
    /// </summary>
    public bool Disponible => opciones.Value.Consumir;

    public Task<bool> PublicarAsync(Aviso aviso, CancellationToken ct) => Task.FromResult(_canal.Writer.TryWrite(aviso));
}

/// <summary>
/// Atiende la <see cref="ColaEnProceso"/> con <c>Mensajeria:ConcurrenciaPorCola</c> lectores. Si un manejador lanza, se registra y el
/// aviso se suelta: la fila sigue en la base y el barrido la vuelve a avisar. (nuevo)
/// </summary>
public sealed class ConsumidorEnProceso(
    ColaEnProceso cola,
    ConsumoDeAvisos consumo,
    IMedidorDeTrabajos medidor,
    IOptions<OpcionesDeMensajeria> opciones,
    ILogger<ConsumidorEnProceso> logger,
    Func<bool>? baseLista = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.Consumir) return;
        while (baseLista is not null && !baseLista())
        {
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }

        var lectores = Enumerable.Range(0, Math.Max(1, (int)opciones.Value.ConcurrenciaPorCola))
            .Select(_ => LeerAsync(stoppingToken)).ToArray();
        await Task.WhenAll(lectores);
    }

    private async Task LeerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var aviso in cola.Avisos.ReadAllAsync(ct))
            {
                var nombreDeCola = TiposDeAviso.Todos.Contains(aviso.Tipo) ? TiposDeAviso.ColaDe(aviso.Tipo) : TiposDeAviso.ColaMuertos;
                try
                {
                    var r = await consumo.ConsumirAsync(aviso, ct);
                    medidor.Consumo(nombreDeCola, Resultado(r));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    medidor.Consumo(nombreDeCola, "fallido");
                    logger.LogError(ex, "[Mensajeria.ConsumoFallido] {Tipo} {TenantPublicId} {Entidad}: lo retoma el barrido.",
                        aviso.Tipo, aviso.Cooperativa, aviso.Entidad);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    internal static string Resultado(ResultadoDeConsumo r) => r switch
    {
        ResultadoDeConsumo.Atendido => "ok",
        ResultadoDeConsumo.CooperativaInactiva => "cooperativa-inactiva",
        ResultadoDeConsumo.Omitido => "omitido",
        _ => "ilegible",
    };
}
