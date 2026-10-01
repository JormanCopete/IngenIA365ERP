using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>valuation</c> de <c>/api/reports/inventory</c> (feature 012, T291; FR-047, SC-017; contracts/api.md §27): el
/// valorizado a <c>asOf</c> (hoy por defecto) por grupo contable, bodega y producto. Sale de <see cref="ValorizadoALaFecha"/>
/// —último cierre vigente anterior más el kardex posterior—, así que a la fecha de un cierre coincide con lo fijado en él; el
/// grupo es el del producto <b>a esa fecha</b> (<see cref="GrupoContableALaFecha"/>) y el valor por bodega es cantidad × promedio
/// del ámbito con el residuo por <c>Redondeo.Residuo</c>. Filtros <c>warehouse</c>, <c>accountingGroup</c>, <c>category</c> y el
/// propio <c>includeTransit</c> (las bodegas de tránsito van en sus dos columnas). Exige <c>Inventory.Costs.Read</c> (sin él, el
/// 404 genérico) y respeta el alcance por bodega. (nuevo)
/// </summary>
public sealed record ValuationReportQuery(FiltrosDeInformeDeInventario Filtros, bool IncludeTransit = false) : IRequest<Result<TablaExportable>>;

public sealed class ValuationReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    ValorizadoALaFecha valorizado,
    IDateTimeService reloj)
    : IRequestHandler<ValuationReportQuery, Result<TablaExportable>>
{
    public const string PermisoDeCostos = "Inventory.Costs.Read";

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Grupo contable", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Unidad", TipoDeColumna.Texto),
        new("Cantidad", TipoDeColumna.Cantidad),
        new("Costo promedio", TipoDeColumna.Costo),
        new("Valor", TipoDeColumna.Moneda),
        new("En tránsito (cantidad)", TipoDeColumna.Cantidad),
        new("En tránsito (valor)", TipoDeColumna.Moneda),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(ValuationReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(PermisoDeCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);

        var f = request.Filtros;
        var fecha = f.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        int? bodegaFiltro = null;
        string? bodegaFiltrada = null;
        if (f.Warehouse is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            bodegaFiltro = bodega.Id;
            bodegaFiltrada = bodega.Code;
        }
        int? grupoFiltro = null;
        if (f.AccountingGroup is { } gp)
        {
            grupoFiltro = await db.AccountingGroups.AsNoTracking().Where(g => g.PublicId == gp).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);
            if (grupoFiltro is null) return Result.Failure<TablaExportable>(CatalogErrors.AccountingGroupNotFound());
        }
        IReadOnlyCollection<int>? productos = null;
        if (f.Category is { } cp)
        {
            var categoria = await db.ProductCategories.AsNoTracking().Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (categoria is null) return Result.Failure<TablaExportable>(CatalogErrors.CategoryNotFound());
            productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.CategoryId == categoria).Select(p => p.Id).ToListAsync(ct);
        }
        if (f.Product is { } pp)
        {
            var producto = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == pp).Select(p => p.Id).ToListAsync(ct);
            productos = productos is null ? producto : productos.Intersect(producto).ToList();
        }

        var filas = (await valorizado.CalcularAsync(fecha, productos, ct))
            .Where(x => alcance.IncluyeBodega(x.WarehouseId) && (bodegaFiltro is null || x.WarehouseId == bodegaFiltro))
            .ToList();
        var productoIds = filas.Select(x => x.ProductId).Distinct().ToList();
        var grupos = await GrupoContableALaFecha.DeAsync(db, productoIds, fecha, ct);
        if (grupoFiltro is int g0) filas = filas.Where(x => grupos.GetValueOrDefault(x.ProductId) == g0).ToList();

        var bodegaIds = filas.Select(x => x.WarehouseId).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new { w.PublicId, w.Code, w.Behavior }, ct);
        if (!request.IncludeTransit) filas = filas.Where(x => bodegas[x.WarehouseId].Behavior != WarehouseBehavior.Transit).ToList();

        var maestros = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, Texto = p.Code + " · " + p.Name, Unidad = p.BaseUnit != null ? p.BaseUnit.Code : string.Empty })
            .ToDictionaryAsync(p => p.Id, ct);
        var grupoIds = grupos.Values.OfType<int>().Distinct().ToList();
        var nombresDeGrupo = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(x => grupoIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, ct);

        var tabla = filas
            .Select(x => new
            {
                Fila = x,
                Grupo = grupos.GetValueOrDefault(x.ProductId) is int id ? nombresDeGrupo.GetValueOrDefault(id) ?? string.Empty : string.Empty,
                Bodega = bodegas[x.WarehouseId],
                Producto = maestros[x.ProductId],
            })
            .OrderBy(x => x.Grupo, StringComparer.Ordinal).ThenBy(x => x.Bodega.Code, StringComparer.Ordinal).ThenBy(x => x.Producto.Texto, StringComparer.Ordinal)
            .Select(x =>
            {
                var transito = x.Bodega.Behavior == WarehouseBehavior.Transit;
                return new FilaExportable(
                [
                    x.Grupo, x.Bodega.Code, x.Producto.Texto, x.Producto.Unidad,
                    transito ? null : x.Fila.Quantity, x.Fila.AverageCost, transito ? null : x.Fila.Value,
                    transito ? x.Fila.Quantity : null, transito ? x.Fila.Value : null,
                    x.Producto.PublicId.ToString(), x.Bodega.PublicId.ToString(),
                ]);
            })
            .ToList();

        var enBodegas = filas.Where(x => bodegas[x.WarehouseId].Behavior != WarehouseBehavior.Transit).ToList();
        var enTransito = filas.Where(x => bodegas[x.WarehouseId].Behavior == WarehouseBehavior.Transit).ToList();
        var totales = new FilaExportable(
        [
            "Total", null, null, null, enBodegas.Sum(x => x.Quantity), null, enBodegas.Sum(x => x.Value),
            request.IncludeTransit ? enTransito.Sum(x => x.Quantity) : null, request.IncludeTransit ? enTransito.Sum(x => x.Value) : null, null, null,
        ]);

        var subtitulo = $"Al {fecha:yyyy-MM-dd}" + (bodegaFiltrada is null ? " · todas las bodegas del alcance" : $" · bodega {bodegaFiltrada}");
        return Result.Success(new TablaExportable("Valorizado de inventario", subtitulo, Columnas, tabla, totales,
        [
            "Cantidades en la unidad base del producto. Valor = cantidad × costo promedio del ámbito de costo; el residuo de redondeo va según Redondeo.Residuo.",
            "El grupo contable es el del producto a la fecha del informe. A la fecha de un cierre, coincide con el valorizado fijado en él.",
        ]));
    }
}
