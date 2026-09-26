using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>stock</c> de <c>/api/reports/inventory</c> (feature 012, T260; contracts/api.md §27): existencia por bodega y
/// producto en unidad base —físico, reservado (0 hasta I6), disponible, en tránsito hacia la bodega— con mínimo, punto de
/// reorden y máximo de <c>INV_ReorderPolicies</c>. Filtros <c>warehouse</c>, <c>category</c> y el propio <c>onlyWithStock</c>. No
/// tiene columnas de costo (el valorizado es la vista <c>valuation</c>, US3). El alcance se aplica en la consulta
/// (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record StockReportQuery(FiltrosDeInformeDeInventario Filtros, bool OnlyWithStock = false) : IRequest<Result<TablaExportable>>;

public sealed class StockReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, PosicionDeReposicion posiciones)
    : IRequestHandler<StockReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Bodega", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Unidad", TipoDeColumna.Texto),
        new("Físico", TipoDeColumna.Cantidad),
        new("Reservado", TipoDeColumna.Cantidad),
        new("Disponible", TipoDeColumna.Cantidad),
        new("En tránsito hacia la bodega", TipoDeColumna.Cantidad),
        new("Mínimo", TipoDeColumna.Cantidad),
        new("Punto de reorden", TipoDeColumna.Cantidad),
        new("Máximo", TipoDeColumna.Cantidad),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(StockReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.StockBalances.AsNoTracking().PorBodega(alcance, s => s.WarehouseId);
        string? bodegaFiltrada = null;
        if (f.Warehouse is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            consulta = consulta.Where(s => s.WarehouseId == bodega.Id);
            bodegaFiltrada = bodega.Code;
        }
        if (f.Category is { } cp)
        {
            var categoria = await db.ProductCategories.AsNoTracking().Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => db.Products.Any(p => p.Id == s.ProductId && p.CategoryId == categoria));
        }
        if (f.Product is { } pp) consulta = consulta.Where(s => db.Products.Any(p => p.Id == s.ProductId && p.PublicId == pp));
        if (request.OnlyWithStock) consulta = consulta.Where(s => s.Physical != 0m || s.Reserved != 0m);

        var filas = await (
                from s in consulta
                join p in db.Products.AsNoTracking() on s.ProductId equals p.Id
                join w in db.Warehouses.AsNoTracking() on s.WarehouseId equals w.Id
                orderby w.Code, p.Code
                select new
                {
                    s.ProductId, s.WarehouseId, s.Physical, s.Reserved,
                    Bodega = w.Code, BodegaPublica = w.PublicId, Producto = p.Code + " · " + p.Name, ProductoPublico = p.PublicId,
                    Unidad = p.BaseUnit != null ? p.BaseUnit.Code : string.Empty,
                })
            .ToListAsync(ct);

        var parejas = filas.Select(x => (x.ProductId, x.WarehouseId)).ToList();
        var posicion = await posiciones.LeerAsync(parejas, ct);
        var productoIds = filas.Select(x => x.ProductId).Distinct().ToList();
        var politicas = await db.ReorderPolicies.AsNoTracking().Where(r => productoIds.Contains(r.ProductId)).ToListAsync(ct);

        var tabla = filas.Select(x =>
        {
            var politica = politicas.FirstOrDefault(r => r.ProductId == x.ProductId && r.WarehouseId == x.WarehouseId);
            return new FilaExportable(
            [
                x.Bodega, x.Producto, x.Unidad, x.Physical, x.Reserved, x.Physical - x.Reserved,
                (posicion.GetValueOrDefault((x.ProductId, x.WarehouseId)) ?? Posicion.Cero).EnTransito,
                politica?.MinimumQuantity, politica?.ReorderPoint, politica?.MaximumQuantity,
                x.ProductoPublico.ToString(), x.BodegaPublica.ToString(),
            ]);
        }).ToList();

        var subtitulo = bodegaFiltrada is null ? "Todas las bodegas del alcance" : $"Bodega {bodegaFiltrada}";
        return Result.Success(new TablaExportable("Existencias", subtitulo, Columnas, tabla, null,
            ["Cantidades en la unidad base del producto. Disponible = físico − reservado; reservado es 0 hasta la entrega de pedidos (I6)."]));
    }
}
