using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>count-differences</c> de <c>/api/reports/inventory</c> (feature 012, US11, T400; FR-040, FR-041; contracts/api.md §27;
/// decisiones-transversales §2.12): por línea de cada conteo cerrado, el conteo (número y fecha de la foto), la bodega, la ubicación, el
/// producto, lo teórico (foto más lo movido después si el conteo lo admitía), lo contado, si pidió reconteo, la diferencia en cantidad y
/// —con <c>Inventory.Costs.Read</c>— el costo de la foto y la diferencia en valor, y el ajuste que la llevó a la existencia. Filtros
/// <c>from</c>/<c>to</c> (sobre la fecha de la foto), <c>warehouse</c> y el propio <c>count</c> (el PublicId de un conteo). Columnas
/// ocultas <c>_documento</c> (el conteo) y <c>_producto</c>. Sólo conteos de las bodegas del alcance (<see cref="IAlcanceDeInventario"/>).
/// (nuevo)
/// </summary>
public sealed record CountDifferencesReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Count = null, bool OnlyWithDifference = false)
    : IRequest<Result<TablaExportable>>;

public sealed class CountDifferencesReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, VistaDeDocumentos vista)
    : IRequestHandler<CountDifferencesReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Conteo", TipoDeColumna.Texto),
        new("Fecha de la foto", TipoDeColumna.Fecha),
        new("Bodega", TipoDeColumna.Texto),
        new("Ubicación", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Teórico", TipoDeColumna.Cantidad),
        new("Contado", TipoDeColumna.Cantidad),
        new("Reconteo", TipoDeColumna.Texto),
        new("Diferencia (cantidad)", TipoDeColumna.Cantidad),
        new("Costo unitario", TipoDeColumna.Costo),
        new("Diferencia (valor)", TipoDeColumna.Moneda),
        new("Ajuste", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto, "_documento"),
        new("Producto (id)", TipoDeColumna.Texto, "_producto"),
    ];

    public async Task<Result<TablaExportable>> Handle(CountDifferencesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);

        var conteos = db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.PhysicalCount && d.Status == DocumentStatus.Confirmed)
            .DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        if (request.Count is { } conteo) conteos = conteos.Where(d => d.PublicId == conteo);
        if (f.From is { } desde) conteos = conteos.Where(d => d.OperationDate >= desde);
        if (f.To is { } hasta) conteos = conteos.Where(d => d.OperationDate <= hasta);
        if (f.Warehouse is { } bodega) conteos = conteos.Where(d => db.Warehouses.Any(w => w.Id == d.WarehouseId && w.PublicId == bodega));

        var filas = await (
                from d in conteos
                join l in db.CountSnapshotLines.AsNoTracking() on d.Id equals l.DocumentId
                join p in db.Products.AsNoTracking().IgnoreQueryFilters() on l.ProductId equals p.Id
                join u in db.WarehouseLocations.AsNoTracking().IgnoreQueryFilters() on l.LocationId equals u.Id
                join w in db.Warehouses.AsNoTracking().IgnoreQueryFilters() on d.WarehouseId equals (int?)w.Id
                select new
                {
                    d.Id, d.PublicId, d.Prefix, d.Number, d.OperationDate,
                    Bodega = w.Code, Ubicacion = u.Code,
                    ProductoId = p.PublicId, p.Code, p.Name,
                    l.TheoreticalQuantity, l.MovementsAfterSnapshot, l.CountedQuantity, l.Difference, l.LastRound, l.SnapshotUnitCost,
                })
            .ToListAsync(ct);
        if (request.OnlyWithDifference) filas = filas.Where(x => (x.Difference ?? 0m) != 0m).ToList();

        var ids = filas.Select(x => x.Id).Distinct().ToList();
        var ajustes = (await db.DocumentLinks.AsNoTracking()
                .Where(l => ids.Contains(l.SourceDocumentId) && l.Kind == DocumentLinkKind.CountAdjustmentOf)
                .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => new { l.SourceDocumentId, d.Class, d.Prefix, d.Number, d.Status })
                .Where(x => x.Status != DocumentStatus.Discarded)
                .ToListAsync(ct))
            .ToLookup(x => x.SourceDocumentId);

        var tabla = filas.OrderBy(x => x.OperationDate).ThenBy(x => x.Number).ThenBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Ubicacion, StringComparer.Ordinal)
            .Select(x =>
            {
                var diferencia = x.Difference ?? 0m;
                var ajuste = diferencia == 0m
                    ? null
                    : ajustes[x.Id].FirstOrDefault(a => a.Class == (diferencia > 0m ? DocumentClass.PositiveAdjustment : DocumentClass.NegativeAdjustment));
                return new FilaExportable(
                [
                    VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), x.OperationDate, x.Bodega, x.Ubicacion, $"{x.Code} · {x.Name}",
                    x.TheoreticalQuantity + (x.MovementsAfterSnapshot ?? 0m), x.CountedQuantity ?? 0m, x.LastRound > 1 ? "Sí" : "No", diferencia,
                    costos ? x.SnapshotUnitCost : null,
                    costos ? Math.Round(diferencia * x.SnapshotUnitCost, 2, MidpointRounding.AwayFromZero) : null,
                    ajuste is null ? null : $"{VistaDeDocumentos.NumeroVisible(ajuste.Prefix, ajuste.Number) ?? "(en aprobación)"} · {Estado(ajuste.Status)}",
                    x.PublicId.ToString(), x.ProductoId.ToString(),
                ], Resaltada: diferencia != 0m);
            }).ToList();

        var notas = new List<string> { "El costo unitario es el promedio del ámbito al abrir el conteo; el ajuste se valora al promedio de su propia fecha (FR-041)." };
        if (!costos) notas.Add("Sin el permiso Inventory.Costs.Read los valores salen vacíos.");
        return Result.Success(new TablaExportable("Diferencias de conteo", "Teórico, contado y diferencia por línea de cada conteo cerrado", Columnas, tabla,
            null, notas));
    }

    private static string Estado(DocumentStatus estado) => estado switch
    {
        DocumentStatus.Draft => "Borrador",
        DocumentStatus.PendingApproval => "En aprobación",
        DocumentStatus.Confirmed => "Confirmado",
        DocumentStatus.Voided => "Anulado",
        _ => estado.ToString(),
    };
}
