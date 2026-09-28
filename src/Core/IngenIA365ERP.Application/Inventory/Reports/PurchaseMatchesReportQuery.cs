using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Consultas;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>purchase-matches</c> de <c>/api/reports/inventory</c> (feature 012, I5, T798; FR-050, US13; contracts/api.md §27): por
/// línea de factura del proveedor cruzada contra una orden, la orden, la recepción, la factura (número de la cooperativa · del
/// proveedor), el producto, lo pedido, lo recibido no facturado y lo facturado (unidad base), el precio pedido y el facturado, las dos
/// diferencias, si quedó dentro de la tolerancia y su estado. Filtros comunes <c>from</c>/<c>to</c> (sobre la fecha de la factura; sin
/// ellos, el mes en curso) y los propios <c>supplier</c> y <c>status</c> (<c>Held</c>, <c>Approved</c>, <c>Rejected</c>). Los precios y
/// la diferencia de precio sólo con <c>Inventory.Costs.Read</c>. La columna oculta <c>_documento</c> lleva el PublicId de la factura.
/// Sólo las filas vivas (las de un intento anterior quedaron de baja). El alcance por bodega se aplica por la factura
/// (<see cref="IAlcanceDeInventario"/>: la de sus recepciones). La registra en la API T809. (nuevo)
/// </summary>
public sealed record PurchaseMatchesReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Supplier = null, PurchaseMatchStatus? Status = null)
    : IRequest<Result<TablaExportable>>;

public sealed class PurchaseMatchesReportQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IPermissionChecker permisos, IDateTimeService reloj)
    : IRequestHandler<PurchaseMatchesReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Orden", TipoDeColumna.Texto),
        new("Recepción", TipoDeColumna.Texto),
        new("Factura", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Pedido", TipoDeColumna.Cantidad),
        new("Recibido", TipoDeColumna.Cantidad),
        new("Facturado", TipoDeColumna.Cantidad),
        new("Precio pedido", TipoDeColumna.Costo),
        new("Precio facturado", TipoDeColumna.Costo),
        new("Diferencia (cantidad)", TipoDeColumna.Cantidad),
        new("Diferencia (precio)", TipoDeColumna.Moneda),
        new("Dentro de tolerancia", TipoDeColumna.Texto),
        new("Estado", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(PurchaseMatchesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = ConsultasDelCruce.Visibles(db, alcance);
        if (request.Status is { } estado) consulta = consulta.Where(m => m.Status == estado);
        var facturas = db.InventoryDocuments.AsNoTracking().Where(d => d.OperationDate >= desde && d.OperationDate <= hasta);
        var proveedor = request.Supplier ?? f.Person;
        if (proveedor is { } p) facturas = facturas.Where(d => db.People.Any(x => x.Id == d.CounterpartyPersonId && x.PublicId == p));
        var ids = facturas.Select(d => d.Id);
        var filas = await consulta.Where(m => ids.Contains(m.InvoiceDocumentId)).ToListAsync(ct);
        var conPrecios = await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct);

        var deFactura = filas.Select(m => m.InvoiceDocumentId).Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => deFactura.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number, d.OperationDate }).ToDictionaryAsync(d => d.Id, ct);
        var delProveedor = await db.SupplierInvoiceDetails.AsNoTracking().Where(s => deFactura.Contains(s.DocumentId))
            .Select(s => new { s.DocumentId, Numero = s.SupplierPrefix + s.SupplierNumber }).ToDictionaryAsync(s => s.DocumentId, s => s.Numero, ct);

        var lineasDeFactura = filas.Select(m => m.InvoiceLineId).Distinct().ToList();
        var productos = await (from l in db.InventoryDocumentLines.AsNoTracking()
                               join pr in db.Products.AsNoTracking() on l.ProductId equals pr.Id
                               where lineasDeFactura.Contains(l.Id)
                               select new { l.Id, l.LineNumber, Producto = pr.Code + " · " + pr.Name })
            .ToDictionaryAsync(x => x.Id, ct);
        var recepcionDe = (await (from x in db.DocumentLineLinks.AsNoTracking()
                                  join v in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals v.Id
                                  join d in db.InventoryDocuments.AsNoTracking() on v.SourceDocumentId equals d.Id
                                  where lineasDeFactura.Contains(x.TargetLineId) && !x.IsDeleted && !v.IsDeleted && v.Kind == DocumentLinkKind.InvoiceOfReceipt
                                  select new { x.TargetLineId, d.Prefix, d.Number })
                .ToListAsync(ct))
            .GroupBy(x => x.TargetLineId).ToDictionary(g => g.Key, g => VistaDeDocumentos.NumeroVisible(g.First().Prefix, g.First().Number));
        var lineasDeOrden = filas.Select(m => m.OrderLineId).OfType<int>().Distinct().ToList();
        var ordenDe = await (from l in db.InventoryDocumentLines.AsNoTracking()
                             join d in db.InventoryDocuments.AsNoTracking() on l.DocumentId equals d.Id
                             where lineasDeOrden.Contains(l.Id)
                             select new { l.Id, d.Prefix, d.Number })
            .ToDictionaryAsync(x => x.Id, x => VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), ct);

        var tabla = filas
            .Where(m => documentos.ContainsKey(m.InvoiceDocumentId) && productos.ContainsKey(m.InvoiceLineId))
            .OrderBy(m => documentos[m.InvoiceDocumentId].OperationDate).ThenBy(m => m.InvoiceDocumentId).ThenBy(m => productos[m.InvoiceLineId].LineNumber)
            .Select(m =>
            {
                var d = documentos[m.InvoiceDocumentId];
                var numero = VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number);
                var factura = delProveedor.TryGetValue(d.Id, out var suyo) ? $"{numero ?? "(en aprobación)"} · {suyo}" : numero;
                return new FilaExportable(
                [
                    m.OrderLineId is int o ? ordenDe.GetValueOrDefault(o) : null,
                    recepcionDe.GetValueOrDefault(m.InvoiceLineId),
                    factura,
                    productos[m.InvoiceLineId].Producto,
                    m.OrderedQuantity,
                    m.ReceivedNotInvoicedQuantity,
                    m.InvoicedQuantity,
                    conPrecios ? m.OrderedUnitPrice : null,
                    conPrecios ? m.InvoicedUnitPrice : null,
                    m.QuantityDifference,
                    conPrecios ? m.PriceDifferenceAmount : null,
                    m.ExceedsTolerance ? "No" : "Sí",
                    Estado(m.Status),
                    d.PublicId.ToString(),
                ], Resaltada: m.Status == PurchaseMatchStatus.Held);
            })
            .ToList();

        return Result.Success(new TablaExportable("Cruce a tres vías de compras", $"Facturas del proveedor del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}",
            Columnas, tabla, null,
            conPrecios ? [] : ["Los precios no se muestran: requieren el permiso para ver costos."]));
    }

    private static string Estado(PurchaseMatchStatus? estado) => estado switch
    {
        PurchaseMatchStatus.Held => "Retenida",
        PurchaseMatchStatus.Approved => "Aprobada",
        PurchaseMatchStatus.Rejected => "Rechazada",
        _ => "Dentro de la tolerancia",
    };
}
