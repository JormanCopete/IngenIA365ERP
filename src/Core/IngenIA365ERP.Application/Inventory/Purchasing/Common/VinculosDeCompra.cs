using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>Lo facturado y lo devuelto de una línea de recepción (por los vínculos vigentes). (nuevo)</summary>
public sealed record ConsumoDeRecepcion(decimal Facturado, decimal Devuelto);

/// <summary>
/// Los vínculos de compras (feature 012, US9; data-model §5.5): factura ← recepción (<c>InvoiceOfReceipt</c>), nota ← factura
/// (<c>NoteOf</c>) y devolución ← recepción (<c>ReturnOf</c>), por documento y por línea. <b>No se guardan contadores</b>: lo
/// pendiente de una línea origen es su <c>QuantityBase</c> menos lo que suman los vínculos cuyo destino está
/// <c>PendingApproval</c> o <c>Confirmed</c> (no anulado, no borrador, no descartado). El borrador los escribe al guardar
/// (así el alcance de una factura sin bodega se calcula por sus orígenes, <c>FiltroDeAlcance</c>) y descartarlo los suelta.
/// (nuevo)
/// </summary>
public sealed class VinculosDeCompra(IApplicationDbContext db, IDateTimeService reloj)
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>
    /// Reemplaza los vínculos de <paramref name="kind"/> donde <paramref name="destino"/> es el destino: los anteriores quedan de
    /// baja lógica (Principio VII) y nacen uno por documento origen con los de sus líneas.
    /// </summary>
    public async Task ReemplazarAsync(InventoryDocument destino, DocumentLinkKind kind,
        IReadOnlyList<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)> pares, CancellationToken ct)
    {
        if (destino.Id != 0)
        {
            var ahora = reloj.UtcNow;
            var anteriores = await db.DocumentLinks.Include(l => l.LineLinks)
                .Where(l => l.TargetDocumentId == destino.Id && l.Kind == kind).ToListAsync(ct);
            foreach (var vinculo in anteriores)
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

        foreach (var porDocumento in pares.GroupBy(p => p.Origen.DocumentId))
        {
            var vinculo = new DocumentLink { SourceDocumentId = porDocumento.Key, TargetDocument = destino, Kind = kind };
            foreach (var (origen, linea) in porDocumento)
            {
                vinculo.LineLinks.Add(new DocumentLineLink
                {
                    DocumentLink = vinculo,
                    SourceLineId = origen.Id,
                    TargetLine = linea,
                    QuantityBase = linea.QuantityBase,
                });
            }
            db.DocumentLinks.Add(vinculo);
        }
    }

    /// <summary>Los pares (línea origen, línea destino) de <paramref name="kind"/> que tiene hoy el documento.</summary>
    public async Task<IReadOnlyList<(int SourceLineId, int TargetLineId, decimal QuantityBase, int SourceDocumentId)>> DeAsync(
        InventoryDocument destino, DocumentLinkKind kind, CancellationToken ct)
    {
        // En memoria primero: el borrador recién armado todavía no tiene Id.
        var locales = db.DocumentLinks.Local.Where(l => !l.IsDeleted && l.Kind == kind && (l.TargetDocument == destino || (destino.Id != 0 && l.TargetDocumentId == destino.Id)))
            .SelectMany(l => l.LineLinks.Where(x => !x.IsDeleted).Select(x => (x.SourceLineId, TargetLineId: x.TargetLine?.Id ?? x.TargetLineId, x.QuantityBase, l.SourceDocumentId)))
            .ToList();
        if (locales.Count > 0 || destino.Id == 0) return locales;

        return (await db.DocumentLineLinks.AsNoTracking()
                .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x, l })
                .Where(y => y.l.TargetDocumentId == destino.Id && y.l.Kind == kind)
                .Select(y => new { y.x.SourceLineId, y.x.TargetLineId, y.x.QuantityBase, y.l.SourceDocumentId })
                .ToListAsync(ct))
            .Select(y => (y.SourceLineId, y.TargetLineId, y.QuantityBase, y.SourceDocumentId))
            .ToList();
    }

    /// <summary>Los documentos origen de <paramref name="kind"/> del documento (recepciones de una factura, factura de una nota).</summary>
    public async Task<IReadOnlyList<InventoryDocument>> OrigenesAsync(InventoryDocument destino, DocumentLinkKind kind, CancellationToken ct)
    {
        var ids = (await DeAsync(destino, kind, ct)).Select(p => p.SourceDocumentId).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await db.InventoryDocuments.Include(d => d.Lines).Where(d => ids.Contains(d.Id)).OrderBy(d => d.Id).ToListAsync(ct);
    }

    /// <summary>Lo facturado y lo devuelto de cada línea de recepción, sin contar <paramref name="excluir"/>.</summary>
    public async Task<IReadOnlyDictionary<int, ConsumoDeRecepcion>> ConsumoAsync(IReadOnlyCollection<int> lineasDeRecepcion, int excluir, CancellationToken ct)
    {
        if (lineasDeRecepcion.Count == 0) return new Dictionary<int, ConsumoDeRecepcion>();
        var filas = await db.DocumentLineLinks.AsNoTracking()
            .Where(x => lineasDeRecepcion.Contains(x.SourceLineId))
            .Join(db.DocumentLinks.AsNoTracking(), x => x.DocumentLinkId, l => l.Id, (x, l) => new { x.SourceLineId, x.QuantityBase, l.Kind, l.TargetDocumentId })
            .Where(y => (y.Kind == DocumentLinkKind.InvoiceOfReceipt || y.Kind == DocumentLinkKind.ReturnOf) && y.TargetDocumentId != excluir)
            .Join(db.InventoryDocuments.AsNoTracking(), y => y.TargetDocumentId, d => d.Id, (y, d) => new { y.SourceLineId, y.QuantityBase, y.Kind, d.Status })
            .Where(y => Vigentes.Contains(y.Status))
            .ToListAsync(ct);
        return filas.GroupBy(f => f.SourceLineId).ToDictionary(g => g.Key, g => new ConsumoDeRecepcion(
            g.Where(f => f.Kind == DocumentLinkKind.InvoiceOfReceipt).Sum(f => f.QuantityBase),
            g.Where(f => f.Kind == DocumentLinkKind.ReturnOf).Sum(f => f.QuantityBase)));
    }

    /// <summary>
    /// Lo que queda de una factura tras sus notas vigentes (sin contar <paramref name="excluir"/>): el total de la factura
    /// menos las notas crédito y más las notas débito.
    /// </summary>
    public async Task<decimal> RestanteDeFacturaAsync(InventoryDocument factura, int excluir, CancellationToken ct)
    {
        var notas = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == factura.Id && l.Kind == DocumentLinkKind.NoteOf && l.TargetDocumentId != excluir)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => Vigentes.Contains(d.Status))
            .Join(db.SupplierInvoiceDetails.AsNoTracking(), d => d.Id, s => s.DocumentId, (d, s) => new { d.Total, s.IsDebitNote })
            .ToListAsync(ct);
        return factura.Total - notas.Where(n => !n.IsDebitNote).Sum(n => n.Total) + notas.Where(n => n.IsDebitNote).Sum(n => n.Total);
    }

    /// <summary>El detalle del proveedor del documento (seguido), en memoria o en la base.</summary>
    public async Task<SupplierInvoiceDetail?> DetalleAsync(InventoryDocument documento, CancellationToken ct) =>
        db.SupplierInvoiceDetails.Local.FirstOrDefault(d => d.Document == documento || (documento.Id != 0 && d.DocumentId == documento.Id))
        ?? (documento.Id == 0 ? null : await db.SupplierInvoiceDetails.FirstOrDefaultAsync(d => d.DocumentId == documento.Id, ct));
}
