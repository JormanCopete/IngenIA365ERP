using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>Los números técnicos de una pasada (sección <c>Lending:Process</c>; data-model parte 3 §12.1, «Técnicos»). (nuevo)</summary>
/// <param name="MaxParallelCooperatives">Cooperativas que se recorren a la vez.</param>
/// <param name="Presupuesto">Lo que dura el trabajo en una cooperativa; menor que el arrendamiento.</param>
/// <param name="DuracionDelArrendamiento">TTL de <c>lending.process</c>.</param>
public sealed record ParametrosDeLaPasada(int MaxParallelCooperatives, TimeSpan Presupuesto, TimeSpan DuracionDelArrendamiento)
{
    /// <summary>Lo que está mal; vacío si sirve. El presupuesto tiene que caber en el arrendamiento (T5).</summary>
    public IReadOnlyList<string> Problemas()
    {
        var problemas = new List<string>();
        if (MaxParallelCooperatives <= 0) problemas.Add("MaxParallelCooperatives tiene que ser mayor que cero.");
        if (Presupuesto <= TimeSpan.Zero) problemas.Add("BudgetSeconds tiene que ser mayor que cero.");
        if (DuracionDelArrendamiento <= Presupuesto)
            problemas.Add("LeaseTtlSeconds tiene que ser mayor que BudgetSeconds: el arrendamiento vencería a mitad del presupuesto.");
        return problemas;
    }
}

/// <summary>
/// Una pasada del procesador de Cartera por las cooperativas activas (feature 013, T5, PM-13, T0226; data-model §22.5). La llama
/// <c>ProcesadorDeCartera</c> (API, <c>BackgroundService</c>) en cada intervalo y la llaman las pruebas a mano. Por cada cooperativa
/// —a lo sumo <see cref="ParametrosDeLaPasada.MaxParallelCooperatives"/> a la vez—, y siempre por <see cref="IEjecutorEnCooperativa"/>
/// con el actor «Proceso de integración»:
/// <list type="number">
/// <item>toma el arrendamiento <c>lending.process</c> de su base (si lo tiene otra réplica, la salta);</item>
/// <item>conduce sus corridas con <see cref="ConductorDeCorridasDeCartera"/> dentro del presupuesto (que salta las cooperativas sin
/// Cartera activada);</item>
/// <item>suelta el arrendamiento.</item>
/// </list>
/// Una cooperativa que falla se registra y la pasada sigue con las demás. (nuevo)
/// </summary>
public sealed class PasadaDeCartera(
    IServiceScopeFactory ambitos,
    IEjecutorEnCooperativa ejecutor,
    ILogger<PasadaDeCartera> logger)
{
    /// <summary>El origen con que firma el proceso: <c>Tarea:lending.process</c>.</summary>
    public static readonly string Origen = Actor.OrigenDeTarea(NombresDeArrendamiento.ProcesoDeCartera);

    /// <summary>Por todas las cooperativas activas, o sólo la indicada (la fixture de pruebas).</summary>
    public async Task<IReadOnlyDictionary<Guid, ResultadoDeConduccion?>> CorrerAsync(ParametrosDeLaPasada parametros, Guid? soloEsta, CancellationToken ct)
    {
        IReadOnlyList<TenantDirectoryEntry> cooperativas;
        await using (var ambito = ambitos.CreateAsyncScope())
        {
            cooperativas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
        }

        var resultados = new System.Collections.Concurrent.ConcurrentDictionary<Guid, ResultadoDeConduccion?>();
        var opciones = new ParallelOptions { MaxDegreeOfParallelism = parametros.MaxParallelCooperatives, CancellationToken = ct };
        await Parallel.ForEachAsync(cooperativas.Where(c => soloEsta is null || c.PublicId == soloEsta), opciones, async (cooperativa, c) =>
        {
            try
            {
                resultados[cooperativa.PublicId] = await CorrerEnAsync(cooperativa, parametros, c);
            }
            catch (OperationCanceledException) when (c.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                resultados[cooperativa.PublicId] = null;
                logger.LogError(ex, "[Cartera.CooperativaFallida] El proceso de Cartera falló en {Cooperativa} ({TenantPublicId}); siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        });
        return resultados;
    }

    /// <summary>Una cooperativa: arrendamiento, conducción y liberación. Nulo si no se tomó el arrendamiento o la base está pendiente.</summary>
    private async Task<ResultadoDeConduccion?> CorrerEnAsync(TenantDirectoryEntry cooperativa, ParametrosDeLaPasada parametros, CancellationToken ct)
    {
        var actor = Actor.ProcesoDeIntegracion(Origen);
        var tomado = false;
        var ejecutada = await ejecutor.EjecutarAsync(cooperativa, actor, Origen,
            async (servicios, c) => tomado = await servicios.GetRequiredService<IArrendamientos>()
                .ArrendarAsync(NombresDeArrendamiento.ProcesoDeCartera, parametros.DuracionDelArrendamiento, c), ct);
        if (ejecutada == ResultadoEnCooperativa.Omitida || !tomado) return null;

        ResultadoDeConduccion? resultado = null;
        try
        {
            await ejecutor.EjecutarAsync(cooperativa, actor, Origen, async (servicios, c) =>
                resultado = await servicios.GetRequiredService<ConductorDeCorridasDeCartera>().ConducirAsync(parametros.Presupuesto, c), ct);
        }
        finally
        {
            await ejecutor.EjecutarAsync(cooperativa, actor, Origen, (servicios, _) => servicios.GetRequiredService<IArrendamientos>()
                .SoltarAsync(NombresDeArrendamiento.ProcesoDeCartera, CancellationToken.None), CancellationToken.None);
        }
        return resultado;
    }
}
