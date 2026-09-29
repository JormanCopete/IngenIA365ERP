using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.CicloComercial;

/// <summary>Un par (línea origen, línea destino) de un vínculo del ciclo, con la cantidad base del vínculo. (nuevo)</summary>
public sealed record ParDelCiclo(int SourceLineId, InventoryDocumentLine Destino, decimal QuantityBase, int SourceDocumentId);

/// <summary>
/// Los vínculos del ciclo comercial de I6 (feature 012, T877–T883; data-model §5.5, §14): pedido ← cotización (<c>FromOrder</c>), remisión
/// ← pedido (<c>DispatchOf</c>), factura ← pedido (<c>FromOrder</c>), factura ← remisiones (<c>FromShipment</c>) y nota débito ← factura
/// (<c>NoteOf</c>). <b>No se guardan contadores</b>: lo pendiente de una línea origen es su <c>QuantityBase</c> menos lo que suman los vínculos
/// cuyo destino está <c>PendingApproval</c> o <c>Confirmed</c> (una anulación deja el destino <c>Voided</c> y lo devuelve a lo pendiente). Lo
/// pendiente de facturar de una remisión suma, además, lo que acreditan las notas crédito confirmadas de sus facturas por su valor completo
/// (contracts/api.md §18.3; decisiones-transversales T53a). (nuevo)
/// </summary>
public sealed class VinculosDelCiclo(IApplicationDbContext db, IDateTimeService reloj)
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>
    /// Reemplaza los vínculos de <paramref name="kind"/> del <paramref name="destino"/>: los anteriores quedan de baja lógica y nace uno por
    /// documento origen (aunque no tenga pares de línea, como la nota débito) con los de sus líneas.
    /// </summary>
    public async Task ReemplazarAsync(InventoryDocument destino, DocumentLinkKind kind, IReadOnlyCollection<int> documentosOrigen,
        IReadOnlyList<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)> pares, CancellationToken ct)
    {
        if (destino.Id != 0)
        {
            var ahora = reloj.UtcNow;
            foreach (var vinculo in await db.DocumentLinks.Include(l => l.LineLinks).Where(l => l.TargetDocumentId == destino.Id && l.Kind == kind && !l.IsDeleted).ToListAsync(ct))
            {
                vinculo.IsDeleted = true;
                vinculo.DeletedAt = ahora;
                foreach (var deLinea in vinculo.LineLinks.Where(x => !x.IsDeleted))
                {
                    deLinea.IsDeleted = true;
                    deLinea.DeletedAt = ahora;
                }
            }
        }
        foreach (var origen in documentosOrigen.Distinct())
        {
            var vinculo = new DocumentLink { SourceDocumentId = origen, TargetDocument = destino, Kind = kind };
            foreach (var (o, d) in pares.Where(p => p.Origen.DocumentId == origen))
                vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = o.Id, TargetLine = d, QuantityBase = d.QuantityBase });
            db.DocumentLinks.Add(vinculo);
        }
    }

    /// <summary>Los pares de <paramref name="kind"/> del destino (en memoria primero: el borrador recién armado todavía no tiene Id).</summary>
    public async Task<IReadOnlyList<ParDelCiclo>> ParesAsync(InventoryDocument destino, DocumentLinkKind kind, CancellationToken ct)
    {
        var locales = db.DocumentLinks.Local
            .Where(l => !l.IsDeleted && l.Kind == kind && (ReferenceEquals(l.TargetDocument, destino) || (destino.Id != 0 && l.TargetDocumentId == destino.Id)))
            .SelectMany(l => l.LineLinks.Where(x => !x.IsDeleted && x.TargetLine is not null).Select(x => new ParDelCiclo(x.SourceLineId, x.TargetLine!, x.QuantityBase, l.SourceDocumentId)))
            .ToList();
        if (locales.Count > 0 || destino.Id == 0) return locales;

        var filas = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.DocumentLink!.IsDeleted && x.DocumentLink.TargetDocumentId == destino.Id && x.DocumentLink.Kind == kind)
            .Select(x => new { x.SourceLineId, x.TargetLineId, x.QuantityBase, x.DocumentLink!.SourceDocumentId })
            .ToListAsync(ct);
        var lineas = destino.Lines.ToDictionary(l => l.Id);
        return filas.Where(f => lineas.ContainsKey(f.TargetLineId))
            .Select(f => new ParDelCiclo(f.SourceLineId, lineas[f.TargetLineId], f.QuantityBase, f.SourceDocumentId)).ToList();
    }

    /// <summary>Los documentos origen de <paramref name="kind"/> del destino, con sus líneas.</summary>
    public async Task<IReadOnlyList<InventoryDocument>> OrigenesAsync(InventoryDocument destino, DocumentLinkKind kind, CancellationToken ct)
    {
        var locales = db.DocumentLinks.Local
            .Where(l => !l.IsDeleted && l.Kind == kind && (ReferenceEquals(l.TargetDocument, destino) || (destino.Id != 0 && l.TargetDocumentId == destino.Id)))
            .Select(l => l.SourceDocumentId).ToList();
        var ids = locales.Count > 0 || destino.Id == 0
            ? locales
            : await db.DocumentLinks.AsNoTracking().Where(l => !l.IsDeleted && l.TargetDocumentId == destino.Id && l.Kind == kind)
                .Select(l => l.SourceDocumentId).ToListAsync(ct);
        if (ids.Count == 0) return [];
        ids = ids.Distinct().ToList();
        return await db.InventoryDocuments.Include(d => d.Lines).Where(d => ids.Contains(d.Id)).OrderBy(d => d.Id).ToListAsync(ct);
    }

    /// <summary>
    /// Lo que queda por despachar o facturar de cada línea de pedido: su cantidad menos las remisiones (<c>DispatchOf</c>) y facturas
    /// (<c>FromOrder</c>) vigentes que la consumen, sin contar <paramref name="excluir"/>.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, decimal>> PendientePorDespacharAsync(IReadOnlyCollection<InventoryDocumentLine> lineasDelPedido, int excluir,
        CancellationToken ct)
    {
        var ids = lineasDelPedido.Select(l => l.Id).ToList();
        var consumido = await ConsumidoAsync(ids, [DocumentLinkKind.DispatchOf, DocumentLinkKind.FromOrder], excluir, ct);
        return lineasDelPedido.ToDictionary(l => l.Id, l => l.QuantityBase - consumido.GetValueOrDefault(l.Id));
    }

    /// <summary>
    /// Lo que queda por facturar de cada línea de remisión: su cantidad menos las facturas vigentes (<c>FromShipment</c>), sin contar
    /// <paramref name="excluir"/>, más lo que las notas crédito confirmadas de esas facturas acreditaron por su valor completo (T883, T53a).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, decimal>> PendientePorFacturarAsync(IReadOnlyCollection<InventoryDocumentLine> lineasDeRemision, int excluir,
        CancellationToken ct)
    {
        var ids = lineasDeRemision.Select(l => l.Id).ToList();
        if (ids.Count == 0) return new Dictionary<int, decimal>();
        var facturado = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && ids.Contains(x.SourceLineId) && !x.DocumentLink!.IsDeleted && x.DocumentLink.Kind == DocumentLinkKind.FromShipment
                && x.DocumentLink.TargetDocumentId != excluir && Vigentes.Contains(x.DocumentLink.TargetDocument!.Status))
            .Select(x => new { x.SourceLineId, x.TargetLineId, x.QuantityBase, Linea = new { x.TargetLine!.QuantityBase, x.TargetLine.NetAmount } })
            .ToListAsync(ct);

        // Lo que vuelve a quedar pendiente: la parte de cada línea de factura que una nota crédito confirmada acreditó por su valor completo.
        var lineasDeFactura = facturado.Select(f => f.TargetLineId).Distinct().ToList();
        var acreditado = lineasDeFactura.Count == 0
            ? []
            : await db.DocumentLineLinks.AsNoTracking()
                .Where(x => !x.IsDeleted && lineasDeFactura.Contains(x.SourceLineId) && !x.DocumentLink!.IsDeleted && x.DocumentLink.Kind == DocumentLinkKind.NoteOf
                    && x.DocumentLink.TargetDocument!.Class == DocumentClass.CreditNote && x.DocumentLink.TargetDocument.Status == DocumentStatus.Confirmed)
                .Select(x => new { x.SourceLineId, x.QuantityBase, x.TargetLine!.NetAmount })
                .ToListAsync(ct);

        var pendiente = lineasDeRemision.ToDictionary(l => l.Id, l => l.QuantityBase);
        foreach (var f in facturado)
        {
            pendiente[f.SourceLineId] -= f.QuantityBase;
            var neto = f.Linea.QuantityBase > 0m ? f.Linea.NetAmount / f.Linea.QuantityBase : 0m;
            foreach (var nota in acreditado.Where(a => a.SourceLineId == f.TargetLineId))
            {
                var vuelve = Math.Min(nota.QuantityBase, f.QuantityBase);
                if (vuelve > 0m && nota.NetAmount >= Math.Round(neto * vuelve, 2, MidpointRounding.ToZero)) pendiente[f.SourceLineId] += vuelve;
            }
        }
        return pendiente;
    }

    private async Task<Dictionary<int, decimal>> ConsumidoAsync(IReadOnlyCollection<int> lineas, DocumentLinkKind[] kinds, int excluir, CancellationToken ct)
    {
        if (lineas.Count == 0) return [];
        var filas = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => !x.IsDeleted && lineas.Contains(x.SourceLineId) && !x.DocumentLink!.IsDeleted && kinds.Contains(x.DocumentLink.Kind)
                && x.DocumentLink.TargetDocumentId != excluir && Vigentes.Contains(x.DocumentLink.TargetDocument!.Status))
            .Select(x => new { x.SourceLineId, x.QuantityBase })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.SourceLineId).ToDictionary(g => g.Key, g => g.Sum(f => f.QuantityBase));
    }
}
