using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Lo común de los comandos que crean un lote (feature 012, T499–T502; T12): el número de
/// <c>COR_IntegrationBatchCounters</c>, el solicitante y el grafo de dependencias para el arrastre y la clausura. No guarda:
/// cada comando guarda en su transacción. (nuevo)
/// </summary>
public static class LotesDeIntegracion
{
    /// <summary>
    /// Toma el número del lote que se crea. El contador es fila única con <c>RowVersion</c>: si dos órdenes toman número a
    /// la vez, una pierde y la operación se repite.
    /// </summary>
    public static async Task<long> TomarNumeroAsync(IApplicationDbContext db, CancellationToken ct)
    {
        // Primero el que ya está en la unidad de trabajo (dos lotes en el mismo guardado comparten contador).
        var contador = db.IntegrationBatchCounters.Local.OrderBy(c => c.Id).FirstOrDefault()
                       ?? await db.IntegrationBatchCounters.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (contador is null)
        {
            contador = new IntegrationBatchCounter();
            db.IntegrationBatchCounters.Add(contador);
        }

        return contador.TomarSiguiente();
    }

    /// <summary>Un lote nuevo, <c>Requested</c>, con quien lo pide: la persona (con su IP y motivo) o el proceso.</summary>
    public static IntegrationBatch Nuevo(long numero, string destino, BatchTrigger disparador, Actor solicitante, string? motivo, DateTime ahora) => new()
    {
        Number = numero,
        Destination = destino,
        Trigger = disparador,
        RequestedByKind = solicitante.Kind,
        RequestedByUserId = solicitante.UserId,
        RequestedByCentralUserId = solicitante.CentralUserId,
        RequestedByName = solicitante.Name,
        RequestedByEmail = solicitante.Email,
        RequestedByIp = solicitante.Ip,
        Reason = string.IsNullOrWhiteSpace(motivo) ? solicitante.Reason : motivo.Trim(),
        RequestedAt = ahora,
    };

    /// <summary>
    /// Pasa las entregas al lote: <c>InBatch</c> con su <c>BatchId</c> y sin espera pendiente. El lote tiene que estar guardado
    /// (necesita su <c>Id</c>).
    /// </summary>
    public static void Asignar(IntegrationBatch lote, IEnumerable<IntegrationMessageDelivery> entregas)
    {
        if (lote.Id == 0) throw new InvalidOperationException("El lote se guarda antes de asignarle entregas.");
        foreach (var entrega in entregas)
        {
            entrega.Status = DeliveryStatus.InBatch;
            entrega.BatchId = lote.Id;
            entrega.NextAttemptAt = null;
        }
    }

    /// <summary>
    /// Los mensajes que dependen, directa o transitivamente, de <paramref name="semillas"/> y cumplen
    /// <paramref name="incluir"/> (sólo se sigue la cadena a través de los que entran). No incluye las semillas.
    /// </summary>
    public static async Task<HashSet<long>> DependientesAsync(
        IApplicationDbContext db, IEnumerable<long> semillas, Func<IReadOnlyCollection<long>, Task<HashSet<long>>> incluir, CancellationToken ct)
    {
        var vistos = semillas.ToHashSet();
        var agregados = new HashSet<long>();
        var frontera = vistos.ToList();
        while (frontera.Count > 0)
        {
            var hijos = await db.IntegrationMessageDependencies.AsNoTracking()
                .Where(d => frontera.Contains(d.DependsOnMessageId))
                .Select(d => d.MessageId).Distinct().ToListAsync(ct);
            var nuevos = hijos.Where(vistos.Add).ToList();
            var entran = nuevos.Count == 0 ? new HashSet<long>() : await incluir(nuevos);
            agregados.UnionWith(entran);
            frontera = entran.ToList();
        }

        return agregados;
    }

    /// <summary>
    /// La clausura de relacionados de <paramref name="semillas"/>: los mensajes unidos por aristas de dependencia en cualquier
    /// dirección (anulaciones, notas, ajustes y derivados, y los originales de éstos), de cualquier fecha, que cumplen
    /// <paramref name="incluir"/>. Incluye las semillas.
    /// </summary>
    public static async Task<HashSet<long>> ClausuraAsync(
        IApplicationDbContext db, IEnumerable<long> semillas, Func<IReadOnlyCollection<long>, Task<HashSet<long>>> incluir, CancellationToken ct)
    {
        var clausura = semillas.ToHashSet();
        var descartados = new HashSet<long>();
        var frontera = clausura.ToList();
        while (frontera.Count > 0)
        {
            var vecinos = await db.IntegrationMessageDependencies.AsNoTracking()
                .Where(d => frontera.Contains(d.MessageId) || frontera.Contains(d.DependsOnMessageId))
                .Select(d => new { d.MessageId, d.DependsOnMessageId })
                .ToListAsync(ct);
            var candidatos = vecinos.SelectMany(v => new[] { v.MessageId, v.DependsOnMessageId })
                .Where(id => !clausura.Contains(id) && !descartados.Contains(id))
                .Distinct().ToList();
            if (candidatos.Count == 0) break;

            var entran = await incluir(candidatos);
            descartados.UnionWith(candidatos.Where(c => !entran.Contains(c)));
            clausura.UnionWith(entran);
            frontera = entran.ToList();
        }

        return clausura;
    }
}

/// <summary>
/// Lo que responden las órdenes de lote (api.md §25.2 y §26.4): el lote creado y cuánto tomó. <see cref="Dragged"/> sólo en el
/// reproceso; <see cref="Documents"/>, documentos de origen distintos. (nuevo)
/// </summary>
public sealed record LoteOrdenadoDto(
    Guid BatchPublicId,
    long Number,
    BatchTrigger Trigger,
    BatchStatus Status,
    int Messages,
    int Documents,
    int Dragged);
