using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Lo pendiente de una línea de solicitud o de orden (feature 012, I5, T788). <see cref="Cantidad"/> es la de la línea en unidad base,
/// <see cref="Consumido"/> lo que ya suman sus vínculos <c>FromOrder</c> vigentes y <see cref="Pendiente"/> lo que queda: en una
/// solicitud es <c>pendingToOrder</c>, en una orden <c>pendingToReceive</c>. (nuevo)
/// </summary>
public sealed record PendienteDeLinea(int LineId, int DocumentId, int LineNumber, Guid LinePublicId, int ProductId, decimal Cantidad, decimal Consumido, decimal Pendiente);

/// <summary>
/// El cálculo de los pendientes de compras (feature 012, I5, T788; data-model §5.5; contracts/api.md §14.9). <b>No se guardan
/// contadores</b>: lo pendiente de una línea origen es su <c>QuantityBase</c> menos lo que suman los vínculos <c>FromOrder</c> cuyo
/// destino está <c>PendingApproval</c> o <c>Confirmed</c> —ni borrador, ni descartado, ni anulado—. Una línea de solicitud la consumen
/// las órdenes (<c>pendingToOrder</c>); una de orden, las recepciones (<c>pendingToReceive</c>). Una orden sin confirmar no tiene nada
/// pendiente de recibir más que lo pedido; una anulada, descartada o con el saldo cerrado (T792), nada. Lo recibido de más dentro de la
/// tolerancia deja el pendiente en cero, nunca negativo. Lo usan los detalles de §14.9, la recepción contra orden
/// (<see cref="Common.RecepcionContraOrden"/>) y «por recibir» de la posición de reposición (T793). (nuevo)
/// </summary>
public sealed class PendientesDeCompra(IApplicationDbContext db)
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    /// <summary>
    /// Lo que ya consumieron de cada línea origen los documentos vigentes que la toman por <c>FromOrder</c>, sin contar
    /// <paramref name="excluirDocumentoId"/> (el propio documento que se revisa).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, decimal>> ConsumidoAsync(IReadOnlyCollection<int> lineasOrigen, int excluirDocumentoId, CancellationToken ct)
    {
        if (lineasOrigen.Count == 0) return new Dictionary<int, decimal>();
        var filas = await (
                from x in db.DocumentLineLinks.AsNoTracking()
                join l in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals l.Id
                join d in db.InventoryDocuments.AsNoTracking() on l.TargetDocumentId equals d.Id
                where lineasOrigen.Contains(x.SourceLineId) && !x.IsDeleted && !l.IsDeleted && l.Kind == DocumentLinkKind.FromOrder
                      && d.Id != excluirDocumentoId && Vigentes.Contains(d.Status)
                select new { x.SourceLineId, x.QuantityBase })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.SourceLineId).ToDictionary(g => g.Key, g => g.Sum(f => f.QuantityBase));
    }

    /// <summary>Lo pendiente de cada línea viva del documento (solicitud u orden), según su estado.</summary>
    public async Task<IReadOnlyList<PendienteDeLinea>> DeDocumentoAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (documento.Class is not (DocumentClass.PurchaseRequest or DocumentClass.PurchaseOrder)) return [];
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var consumido = await ConsumidoAsync(vivas.Select(l => l.Id).ToList(), 0, ct);
        return vivas.Select(l =>
        {
            var hecho = consumido.GetValueOrDefault(l.Id);
            var pendiente = documento.Status switch
            {
                DocumentStatus.Draft or DocumentStatus.PendingApproval => l.QuantityBase,
                DocumentStatus.Confirmed when documento.BalanceClosedAt is null => Math.Max(0m, l.QuantityBase - hecho),
                _ => 0m,
            };
            return new PendienteDeLinea(l.Id, documento.Id, l.LineNumber, l.PublicId, l.ProductId, l.QuantityBase, hecho, pendiente);
        }).ToList();
    }

    /// <summary>
    /// «Por recibir» por (producto, bodega que recibe) (FR-035; T793): lo pendiente de las líneas de las órdenes <c>Confirmed</c> sin el
    /// saldo cerrado. Sin productos o sin bodegas, de todos o de todas.
    /// </summary>
    public async Task<IReadOnlyDictionary<(int ProductId, int WarehouseId), decimal>> PorRecibirAsync(
        IReadOnlyCollection<int>? productos, IReadOnlyCollection<int>? bodegas, CancellationToken ct)
    {
        var consulta =
            from l in db.InventoryDocumentLines.AsNoTracking()
            join d in db.InventoryDocuments.AsNoTracking() on l.DocumentId equals d.Id
            where d.Class == DocumentClass.PurchaseOrder && d.Status == DocumentStatus.Confirmed && d.BalanceClosedAt == null
                  && !l.IsDeleted && d.WarehouseId != null
            select new { l.Id, l.ProductId, Bodega = d.WarehouseId!.Value, l.QuantityBase };
        if (productos is not null) consulta = consulta.Where(x => productos.Contains(x.ProductId));
        if (bodegas is not null) consulta = consulta.Where(x => bodegas.Contains(x.Bodega));
        var lineas = await consulta.ToListAsync(ct);
        if (lineas.Count == 0) return new Dictionary<(int, int), decimal>();

        var consumido = await ConsumidoAsync(lineas.Select(l => l.Id).ToList(), 0, ct);
        return lineas
            .Select(l => new { Clave = (l.ProductId, l.Bodega), Pendiente = Math.Max(0m, l.QuantityBase - consumido.GetValueOrDefault(l.Id)) })
            .Where(x => x.Pendiente > 0m)
            .GroupBy(x => x.Clave)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Pendiente));
    }
}
