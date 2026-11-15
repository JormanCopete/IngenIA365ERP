using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Mensajeria;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// Lleva a la cola los guardados con mensajes de integración (feature 023, US7; contracts/mensajeria.md §3): lee las cooperativas que anota
/// <see cref="SenalDeMensajes"/>, espera <c>Mensajeria:AvisoIntegracionMinimoMs</c> (250 ms) para juntar las de un mismo pico y darle tiempo al
/// commit, y publica un aviso <c>integracion.entregar</c> por cooperativa. Lo atiende <see cref="ManejadorDeAvisoDeIntegracion"/> en cualquier
/// réplica. Un aviso perdido no pierde nada: el despachador de cada réplica sigue sondeando cada pocos segundos. Sólo corre con RabbitMQ: con la
/// cola en proceso, el despachador local ya despierta con la señal. (nuevo)
/// </summary>
public sealed class AvisadorDeIntegracion(
    SenalDeMensajes senal,
    IColaDeTrabajos cola,
    IDateTimeService reloj,
    IConfiguration configuracion,
    ILogger<AvisadorDeIntegracion> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var junta = TimeSpan.FromMilliseconds(Math.Max(0, configuracion.GetValue("Mensajeria:AvisoIntegracionMinimoMs", 250)));
        try
        {
            while (await senal.Cooperativas.WaitToReadAsync(stoppingToken))
            {
                if (junta > TimeSpan.Zero) await Task.Delay(junta, stoppingToken);
                var cooperativas = new HashSet<Guid>();
                while (senal.Cooperativas.TryRead(out var c)) cooperativas.Add(c);
                foreach (var cooperativa in cooperativas)
                {
                    if (!cola.Disponible) break;
                    var ok = await cola.PublicarAsync(
                        Aviso.De(TiposDeAviso.IntegracionEntregar, cooperativa, "*", reloj.UtcNow, "confirmacion"), stoppingToken);
                    if (!ok)
                        logger.LogDebug("[Mensajeria.AvisoDeIntegracionSinConfirmar] {TenantPublicId}: lo toma el sondeo del despachador.", cooperativa);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

/// <summary>
/// <c>integracion.entregar</c> (feature 023, US7): una pasada del despachador sólo por esa cooperativa, bajo su arrendamiento
/// <c>integration.dispatch</c> —si lo tiene otra réplica, ésa ya está entregando y no se hace nada—. No corre dentro del ejecutor por cooperativa
/// (<see cref="IManejadorDeAviso.PorCooperativa"/> falso): la pasada abre ella misma un ámbito por unidad, como siempre. (nuevo)
/// </summary>
public sealed class ManejadorDeAvisoDeIntegracion(DespachadorDeMensajes despachador) : IManejadorDeAviso
{
    public string Tipo => TiposDeAviso.IntegracionEntregar;

    public bool PorCooperativa => false;

    public Task ManejarAsync(Aviso aviso, IServiceProvider servicios, CancellationToken ct) =>
        despachador.CorrerUnaPasadaAsync(aviso.Cooperativa, ct);
}
