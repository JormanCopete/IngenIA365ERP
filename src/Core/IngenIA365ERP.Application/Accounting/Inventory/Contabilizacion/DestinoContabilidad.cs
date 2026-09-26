using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// Contabilidad como destino de los mensajes de Inventario (feature 012, T514; contracts/contabilidad.md §3.1 y §5;
/// contracts/mensajes.md §13). Acepta lo que diga <see cref="VersionesAceptadas"/>; consume una unidad por
/// <see cref="PostInventoryMessagesCommand"/> y un grupo resumido por <see cref="PostInventorySummaryGroupCommand"/>, los dos por
/// <see cref="ISender"/> para que pasen por validación, auditoría y reintento por concurrencia; y planea las pasadas de un lote
/// con <see cref="AgrupadorDeResumidos"/>: por documento, o resumido si la <c>ScheduleKey</c> de la entrega lo sella.
///
/// <para>
/// Nunca lanza: una excepción (base caída, concurrencia que sobrevivió a los reintentos) es <c>Retry</c>, y un fallo de
/// validación del comando, <c>Rejected</c> con su código. El resultado lo registra el despachador en otro ámbito. (nuevo)
/// </para>
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
            if (trabajo.ClaveDeGrupo is null)
            {
                var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
                foreach (var unidad in trabajo.Unidades)
                {
                    var r = await sender.Send(new PostInventoryMessagesCommand(unidad.MessagePublicIds, unidad.BatchPublicId), ct);
                    resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
                }
                return resultados;
            }

            var lote = trabajo.Unidades.Select(u => u.BatchPublicId).FirstOrDefault(b => b is not null) ?? Guid.Empty;
            var grupo = await sender.Send(new PostInventorySummaryGroupCommand(lote, trabajo.ClaveDeGrupo,
                trabajo.Unidades.SelectMany(u => u.MessagePublicIds).ToList()), ct);
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
        var r = await sender.Send(new TotalesDeLoteDeInventarioQuery(batchPublicId), ct);
        return r.IsSuccess ? r.Value : TotalesDeLoteEnDestino.Cero;
    }

    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas) =>
        AgrupadorDeResumidos.Planear(entregas, Destino);
}
