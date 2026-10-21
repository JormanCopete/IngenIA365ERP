using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Accounting.Integration;

/// <summary>
/// Contabilidad como destino de la bandeja, <b>enrutador de dos emisores</b> (feature 012, T514; feature 013, T0645, T15;
/// contracts/contabilidad.md §6 de la 013 y §3.1 y §5 de la 012). Sigue siendo el <b>único</b> <see cref="IDestinoDeMensajes"/> de
/// <c>Accounting</c> (<c>UnSoloConsumidorPorDestino</c>):
/// <list type="bullet">
///   <item><see cref="Acepta"/> busca el tipo en el catálogo agregado y responde con las versiones aceptadas de su módulo de origen;</item>
///   <item><see cref="ConsumirAsync"/> reparte por <see cref="UnidadDeConsumo.OriginModule"/>: <c>INV</c> a <see cref="PostInventoryMessagesCommand"/>
///         o <see cref="PostInventorySummaryGroupCommand"/>, sin cambios; <c>CAR</c> a <see cref="PostLendingFactsCommand"/> o
///         <see cref="PostLendingSummaryGroupCommand"/>. Todos por <see cref="ISender"/>, para que pasen por validación, auditoría y reintento
///         por concurrencia;</item>
///   <item><see cref="PlanearLote"/> separa por módulo antes de agrupar (<see cref="AgrupadorDeResumidos"/> para Inventario,
///         <see cref="AgrupadorDeResumidosDeCartera"/> para Cartera): un trabajo nunca mezcla módulos;</item>
///   <item><see cref="TotalesDelLoteAsync"/> suma los recibos de los dos (<c>ACC_InventoryPostings</c> y <c>ACC_LendingPostings</c>).</item>
/// </list>
/// Nunca lanza: una excepción (base caída, concurrencia que sobrevivió a los reintentos) es <c>Retry</c>, y un fallo de validación del comando,
/// <c>Rejected</c> con su código. El resultado lo registra el despachador en otro ámbito.
/// </summary>
public sealed class DestinoContabilidad(ISender sender, ILogger<DestinoContabilidad> logger) : IDestinoDeMensajes
{
    public string Destino => IntegrationDestinations.Accounting;

    public bool Acepta(string type, int version) => VersionesAceptadas.Acepta(type, version);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        try
        {
            var deCartera = trabajo.Unidades.Count > 0 && trabajo.Unidades.All(u => u.OriginModule == ModulosDeOrigen.Cartera);
            if (trabajo.ClaveDeGrupo is null)
            {
                var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
                foreach (var unidad in trabajo.Unidades)
                {
                    var r = unidad.OriginModule == ModulosDeOrigen.Cartera
                        ? await sender.Send(new PostLendingFactsCommand(unidad.MessagePublicIds, unidad.BatchPublicId), ct)
                        : await sender.Send(new PostInventoryMessagesCommand(unidad.MessagePublicIds, unidad.BatchPublicId), ct);
                    resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
                }
                return resultados;
            }

            var lote = trabajo.Unidades.Select(u => u.BatchPublicId).FirstOrDefault(b => b is not null) ?? Guid.Empty;
            var mensajes = trabajo.Unidades.SelectMany(u => u.MessagePublicIds).ToList();
            var grupo = deCartera
                ? await sender.Send(new PostLendingSummaryGroupCommand(lote, trabajo.ClaveDeGrupo, mensajes), ct)
                : await sender.Send(new PostInventorySummaryGroupCommand(lote, trabajo.ClaveDeGrupo, mensajes), ct);
            if (grupo.IsSuccess) return grupo.Value;
            return trabajo.Unidades.Select(u => new ResultadoDeUnidad(u, new ResultadoDeConsumo.Rejected(grupo.Error.Code, grupo.Error.Message))).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Contabilidad no pudo consumir el trabajo {Grupo} ({Unidades} unidades): se reintenta.",
                trabajo.ClaveDeGrupo ?? "por documento", trabajo.Unidades.Count);
            return trabajo.Unidades.Select(u => new ResultadoDeUnidad(u, new ResultadoDeConsumo.Retry(ex.Message))).ToList();
        }
    }

    public async Task<TotalesDeLoteEnDestino> TotalesDelLoteAsync(Guid batchPublicId, CancellationToken ct)
    {
        var inventario = await sender.Send(new TotalesDeLoteDeInventarioQuery(batchPublicId), ct);
        var cartera = await sender.Send(new GetLendingBatchTotalsQuery(batchPublicId), ct);
        var i = inventario is { IsSuccess: true } ? inventario.Value : TotalesDeLoteEnDestino.Cero;
        var c = cartera is { IsSuccess: true } ? cartera.Value : TotalesDeLoteEnDestino.Cero;
        return new TotalesDeLoteEnDestino(i.Debit + c.Debit, i.Credit + c.Credit);
    }

    /// <summary>
    /// Separa por módulo de origen antes de agrupar (feature 013, T0645, T15): lo de Inventario va a su agrupador (por documento o resumido),
    /// lo de Cartera al suyo (por hecho o resumido por día) y lo de los demás módulos, una unidad por trabajo. Una unidad nunca mezcla módulos.
    /// </summary>
    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        var deInventario = entregas.Where(m => m.OriginModule == ModulosDeOrigen.Inventario).ToList();
        var deCartera = entregas.Where(m => m.OriginModule == ModulosDeOrigen.Cartera).ToList();
        var otros = entregas.Where(m => m.OriginModule != ModulosDeOrigen.Inventario && m.OriginModule != ModulosDeOrigen.Cartera).ToList();
        var trabajos = AgrupadorDeResumidos.Planear(deInventario, Destino).ToList();
        trabajos.AddRange(AgrupadorDeResumidosDeCartera.Planear(deCartera, Destino));
        if (otros.Count > 0)
            trabajos.AddRange(AgrupadorDeResumidos.Unidades(otros, Destino).Select(u => TrabajoDeConsumo.DeUnaUnidad(u.Unidad)));
        return trabajos;
    }
}
