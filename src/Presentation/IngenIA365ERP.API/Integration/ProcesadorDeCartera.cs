using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Persistence.Initialization;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// El procesador de Cartera (feature 013, T5, PM-13, C59, T0226; data-model §22): el <c>BackgroundService</c> que, cada
/// <c>Lending:Process:IntervalSeconds</c>, corre una <see cref="PasadaDeCartera"/> por las cooperativas activas. Registrado sólo en
/// <c>Program.cs</c> y condicionado a <c>Lending:Process:Enabled</c>; el DbMigrator nunca lo arranca. Todo su trabajo pasa por
/// <see cref="IEjecutorEnCooperativa"/> (la pasada), con el arrendamiento <c>lending.process</c> de cada base, a lo sumo
/// <c>MaxParallelCooperatives</c> a la vez y con un presupuesto menor que el arrendamiento; las corridas de una cooperativa sin
/// Cartera activada no se tocan. Los ejecutores de las cuatro clases de corrida llegan con US4 y US9: hasta entonces sus corridas
/// quedan <c>Requested</c> con advertencia. (nuevo)
///
/// <para>US4 (bloque 8B, T0501): el ciclo diario y la corrección por valor nacional ya tienen ejecutor
/// (<c>EjecutorDelCicloDiario</c>, <c>EjecutorDeLaCorreccionDeValorNacional</c>). La pasada lleva de <c>Lending:Process</c> también
/// <c>StartTimeLocal</c> (el ciclo de hoy no nace antes) y <c>BatchSize</c> (créditos por tanda); el conductor elige la corrida en el
/// orden de data-model §22.1, crea el <c>DailyCycle</c> siguiente sólo con el anterior <c>Completed</c> o <c>CompletedWithErrors</c>,
/// conduce las tandas desde <c>CursorLoanId</c> —la tanda que falla al guardar se reintenta crédito por crédito— y renueva
/// <c>lending.process</c> entre tandas; la corrida cierra con <c>TotalsJson</c> y <c>ResultSha256</c> como evento resumen.</para>
/// </summary>
public sealed class ProcesadorDeCartera(
    PasadaDeCartera pasada,
    IEjecutorEnCooperativa ejecutor,
    DatabaseReadiness baseDeDatos,
    IOptions<OpcionesDelProcesoDeCartera> opciones,
    ILogger<ProcesadorDeCartera> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);

    /// <summary>El ejecutor que usa la pasada (lo declara para la regla de los trabajos de fondo: todo pasa por él).</summary>
    public IEjecutorEnCooperativa Ejecutor { get; } = ejecutor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = opciones.Value;
        if (!config.Enabled) return;
        var intervalo = TimeSpan.FromSeconds(config.IntervalSeconds);
        logger.LogInformation("ProcesadorDeCartera iniciado: una pasada cada {Segundos} s, {Paralelas} cooperativas a la vez.",
            intervalo.TotalSeconds, config.MaxParallelCooperatives);

        while (!baseDeDatos.IsReady)
        {
            if (!await EsperarAsync(EsperaDeLaBase, stoppingToken)) return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CorrerUnaPasadaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Cartera.PasadaFallida] La pasada del procesador de Cartera falló; se reintenta en {Segundos} s.",
                    intervalo.TotalSeconds);
            }

            if (!await EsperarAsync(intervalo, stoppingToken)) return;
        }
    }

    /// <summary>Una pasada por todas las cooperativas activas. La usan el ciclo y las pruebas.</summary>
    public Task CorrerUnaPasadaAsync(CancellationToken ct) =>
        baseDeDatos.IsReady ? pasada.CorrerAsync(opciones.Value.Pasada(), null, ct) : Task.CompletedTask;

    /// <summary>Una pasada sólo por la cooperativa indicada (la fixture de pruebas, que tiene el ciclo apagado).</summary>
    public Task CorrerUnaPasadaAsync(Guid tenantPublicId, CancellationToken ct) =>
        baseDeDatos.IsReady ? pasada.CorrerAsync(opciones.Value.Pasada(), tenantPublicId, ct) : Task.CompletedTask;

    private static async Task<bool> EsperarAsync(TimeSpan cuanto, CancellationToken ct)
    {
        try
        {
            await Task.Delay(cuanto, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
