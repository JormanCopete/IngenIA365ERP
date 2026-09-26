using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.Initialization;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// Corre las <see cref="ITareaProgramada"/> registradas en cada cooperativa (feature 012, T050;
/// decisiones-transversales T47). Registrado sólo en <c>Program.cs</c>, condicionado a
/// <c>Integration:ScheduledTasks:Enabled</c>; el DbMigrator nunca lo arranca.
///
/// <para>
/// Una pasada: espera a que <see cref="DatabaseReadiness"/> diga que la base está lista; recorre
/// <see cref="ITenantDirectory.ListActiveAsync"/>; en cada cooperativa toma el arrendamiento
/// <c>scheduled.tasks</c> (si lo tiene otra réplica, la salta); le pregunta a cada tarea si le toca con
/// <see cref="IDateTimeService.AhoraLocal"/> y su última corrida; corre las que sí, cada una en su propia
/// llamada a <see cref="IEjecutorEnCooperativa"/> —un ámbito nuevo— con el actor «Proceso de integración»
/// y origen <c>Tarea:{nombre}</c>, renovando el arrendamiento antes de cada una; y lo suelta al terminar.
/// </para>
///
/// <para>
/// Nada falla en silencio y nada detiene a los demás: una tarea que lanza se registra como error y sigue
/// la siguiente; una cooperativa que falla se registra y sigue la siguiente. La última corrida se recuerda
/// <b>por proceso</b> (sólo las que terminaron bien): tras un reinicio o si el arrendamiento pasa a otra
/// réplica, una tarea puede volver a correr, así que las tareas son idempotentes (<see cref="ITareaProgramada"/>).
/// </para>
/// </summary>
public sealed class ProgramadorDeTareas(
    IServiceScopeFactory ambitos,
    IEjecutorEnCooperativa ejecutor,
    IEnumerable<ITareaProgramada> tareas,
    DatabaseReadiness baseDeDatos,
    IDateTimeService reloj,
    IOptions<IntegrationOptions> opciones,
    ILogger<ProgramadorDeTareas> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);
    private static readonly string OrigenDelArrendamiento = Actor.OrigenDeTarea(NombresDeArrendamiento.TareasProgramadas);

    private readonly IReadOnlyList<ITareaProgramada> _tareas = [.. tareas];
    private readonly ConcurrentDictionary<(Guid Cooperativa, string Tarea), DateTimeOffset> _ultimaCorrida = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(opciones.Value.ScheduledTasks.IntervalSeconds);
        logger.LogInformation("ProgramadorDeTareas iniciado: {Tareas} tareas, una pasada cada {Segundos} s.",
            _tareas.Count, intervalo.TotalSeconds);

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
                logger.LogError(ex, "[Tareas.PasadaFallida] La pasada del programador falló; se reintenta en {Segundos} s.",
                    intervalo.TotalSeconds);
            }

            if (!await EsperarAsync(intervalo, stoppingToken)) return;
        }
    }

    /// <summary>Una pasada por todas las cooperativas activas. La usan el ciclo y las pruebas.</summary>
    public Task CorrerUnaPasadaAsync(CancellationToken ct) => CorrerAsync(null, ct);

    /// <summary>Una pasada sólo por la cooperativa indicada (la fixture de pruebas, que tiene el ciclo apagado).</summary>
    public Task CorrerUnaPasadaAsync(Guid tenantPublicId, CancellationToken ct) => CorrerAsync(tenantPublicId, ct);

    private async Task CorrerAsync(Guid? soloEsta, CancellationToken ct)
    {
        if (!baseDeDatos.IsReady || _tareas.Count == 0) return;

        IReadOnlyList<TenantDirectoryEntry> cooperativas;
        await using (var ambito = ambitos.CreateAsyncScope())
        {
            cooperativas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
        }

        foreach (var cooperativa in cooperativas.Where(c => soloEsta is null || c.PublicId == soloEsta))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await CorrerEnAsync(cooperativa, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Tareas.CooperativaFallida] Las tareas de {Cooperativa} ({TenantPublicId}) fallaron; siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        }
    }

    private async Task CorrerEnAsync(TenantDirectoryEntry cooperativa, CancellationToken ct)
    {
        var tomado = false;
        var resultado = await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
            async (servicios, c) => tomado = await servicios.GetRequiredService<IArrendamientos>()
                .ArrendarAsync(NombresDeArrendamiento.TareasProgramadas, null, c), ct);
        if (resultado == ResultadoEnCooperativa.Omitida || !tomado) return;

        try
        {
            foreach (var tarea in _tareas)
            {
                ct.ThrowIfCancellationRequested();
                var clave = (cooperativa.PublicId, tarea.Nombre);
                var ahora = reloj.AhoraLocal;
                if (!tarea.DebeCorrer(ahora, _ultimaCorrida.TryGetValue(clave, out var ultima) ? ultima : null)) continue;

                var origen = Actor.OrigenDeTarea(tarea.Nombre);
                try
                {
                    var sigue = true;
                    await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(origen), origen, async (servicios, c) =>
                    {
                        sigue = await servicios.GetRequiredService<IArrendamientos>()
                            .RenovarAsync(NombresDeArrendamiento.TareasProgramadas, null, c);
                        if (!sigue) return;
                        await tarea.EjecutarAsync(servicios, c);
                    }, ct);

                    if (!sigue) return; // otra réplica tomó el arrendamiento: ella sigue con lo que falte.
                    _ultimaCorrida[clave] = ahora;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Tareas.TareaFallida] {Origen} falló en {Cooperativa} ({TenantPublicId}); siguen las demás tareas.",
                        origen, cooperativa.Name, cooperativa.PublicId);
                }
            }
        }
        finally
        {
            await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
                (servicios, _) => servicios.GetRequiredService<IArrendamientos>()
                    .SoltarAsync(NombresDeArrendamiento.TareasProgramadas, CancellationToken.None), CancellationToken.None);
        }
    }

    /// <summary>Espera; falso si se pidió detener el servicio.</summary>
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
