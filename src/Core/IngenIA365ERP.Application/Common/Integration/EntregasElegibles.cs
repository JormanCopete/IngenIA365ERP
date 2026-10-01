using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El selector de elegibles (feature 012, T497; contracts/mensajes.md §9, §11; decisiones-transversales T9, T11, T12). Lo usa
/// el despachador para saber qué entregar en cada pasada; sólo lee. (nuevo)
///
/// <para>
/// <b>Elegible para un destino</b> = entrega <c>Pending</c> con <c>NextAttemptAt</c> vencido y <b>ninguna</b> dependencia con
/// entrega a <b>ese mismo destino</b> en <c>Pending</c>, <c>InBatch</c> o <c>Rejected</c>; <c>Processed</c>, <c>NotApplicable</c> y
/// <c>ValidationFailed</c> no bloquean, y una dependencia sin entrega a ese destino tampoco. En un lote vale lo mismo para
/// las <c>InBatch</c> de su <c>BatchId</c>, en pasadas sucesivas: cada pasada toma sólo lo que ya tiene sus dependencias
/// satisfechas. El orden es el <c>Id</c> ascendente; se devuelve agrupado en <b>unidades</b> (origen + <c>originEventKey</c> +
/// destino), completas aunque la tanda corte en medio de una. Un destino sin <see cref="IDestinoDeMensajes"/> registrado
/// (Cartera hasta IC) se salta: no devuelve nada.
/// </para>
/// </summary>
public sealed class EntregasElegibles(IApplicationDbContext db, IEnumerable<IDestinoDeMensajes> destinos)
{
    /// <summary>Los estados de una dependencia que bloquean a sus dependientes en el mismo destino.</summary>
    public static readonly IReadOnlyList<DeliveryStatus> Bloqueantes = [DeliveryStatus.Pending, DeliveryStatus.InBatch, DeliveryStatus.Rejected];

    private readonly HashSet<string> _conConsumidor = destinos.Select(d => d.Destino).ToHashSet(StringComparer.Ordinal);

    /// <summary>Los destinos con consumidor registrado: los únicos que el despachador recorre.</summary>
    public IReadOnlyCollection<string> DestinosConConsumidor => _conConsumidor;

    public bool TieneConsumidor(string destino) => _conConsumidor.Contains(destino);

    /// <summary>Las unidades en línea (y los informativos) elegibles del destino, hasta <paramref name="tanda"/> mensajes.</summary>
    public async Task<IReadOnlyList<UnidadDeConsumo>> EnLineaAsync(string destino, DateTime ahora, int tanda, CancellationToken ct)
    {
        if (!TieneConsumidor(destino)) return [];
        var candidatas = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == destino && d.Status == DeliveryStatus.Pending && (d.NextAttemptAt == null || d.NextAttemptAt <= ahora));
        return await UnidadesAsync(destino, NoBloqueadas(candidatas, destino), loteId: null, lotePublico: null, tanda, ct);
    }

    /// <summary>
    /// Una pasada de un lote: las <c>InBatch</c> de su <c>BatchId</c> que ya tienen las dependencias satisfechas (y cuya espera de
    /// reintento venció), hasta <paramref name="tanda"/> mensajes. Vacío cuando no queda nada que se pueda procesar ahora.
    /// </summary>
    public async Task<IReadOnlyList<UnidadDeConsumo>> DelLoteAsync(Guid batchPublicId, DateTime ahora, int tanda, CancellationToken ct)
    {
        var lote = await db.IntegrationBatches.AsNoTracking().Where(b => b.PublicId == batchPublicId)
            .Select(b => new { b.Id, b.Destination }).FirstOrDefaultAsync(ct);
        if (lote is null || !TieneConsumidor(lote.Destination)) return [];

        var candidatas = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.BatchId == lote.Id && d.Destination == lote.Destination && d.Status == DeliveryStatus.InBatch
                        && (d.NextAttemptAt == null || d.NextAttemptAt <= ahora));
        return await UnidadesAsync(lote.Destination, NoBloqueadas(candidatas, lote.Destination), lote.Id, batchPublicId, tanda, ct);
    }

    /// <summary>Las candidatas sin ninguna dependencia bloqueante en el mismo destino.</summary>
    private IQueryable<IntegrationMessageDelivery> NoBloqueadas(IQueryable<IntegrationMessageDelivery> candidatas, string destino)
    {
        var bloqueantes = Bloqueantes.ToArray();
        return candidatas.Where(d => !db.IntegrationMessageDependencies.Any(dep =>
            dep.MessageId == d.MessageId
            && db.IntegrationMessageDeliveries.Any(x => x.MessageId == dep.DependsOnMessageId && x.Destination == destino && bloqueantes.Contains(x.Status))));
    }

    private async Task<IReadOnlyList<UnidadDeConsumo>> UnidadesAsync(
        string destino, IQueryable<IntegrationMessageDelivery> elegibles, int? loteId, Guid? lotePublico, int tanda, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tanda, 1);

        var filas = await (
                from d in elegibles
                join m in db.IntegrationMessages.AsNoTracking() on d.MessageId equals m.Id
                orderby m.Id
                select new Fila(m.Id, m.PublicId, m.OriginPublicId, m.OriginEventKey, d.Attempts))
            .Take(tanda)
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        // Una unidad no se parte entre tandas: se completan las que la tanda cortó.
        var origenes = filas.Select(f => f.Origen).Distinct().ToList();
        var yaLeidos = filas.Select(f => f.Id).ToHashSet();
        var resto = await (
                from d in elegibles
                join m in db.IntegrationMessages.AsNoTracking() on d.MessageId equals m.Id
                where origenes.Contains(m.OriginPublicId)
                orderby m.Id
                select new Fila(m.Id, m.PublicId, m.OriginPublicId, m.OriginEventKey, d.Attempts))
            .ToListAsync(ct);
        var claves = filas.Select(f => (f.Origen, f.Clave)).ToHashSet();
        filas.AddRange(resto.Where(f => !yaLeidos.Contains(f.Id) && claves.Contains((f.Origen, f.Clave))));

        return filas
            .OrderBy(f => f.Id)
            .GroupBy(f => (f.Origen, f.Clave))
            .Select(g => new UnidadDeConsumo(destino, g.Key.Origen, g.Key.Clave, g.Select(f => f.PublicId).ToList(), lotePublico, g.Max(f => f.Intentos)))
            .ToList();
    }

    private sealed record Fila(long Id, Guid PublicId, Guid Origen, string Clave, int Intentos);
}
