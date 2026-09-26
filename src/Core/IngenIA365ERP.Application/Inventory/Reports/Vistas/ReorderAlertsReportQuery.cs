using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Replenishment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>reorder-alerts</c> de <c>/api/reports/inventory</c> (feature 012, US17, T956; FR-035; contracts/api.md §27): por
/// bodega y producto con política de reorden, sólo las filas que <b>piden reorden</b> (posición igual o menor que el punto) o están
/// en <b>quiebre</b> (disponible bajo el mínimo), con disponible, en tránsito, por recibir (0 hasta I5), posición, mínimo, punto,
/// máximo, sugerido (máximo − posición) y quiebre, y las ocultas <c>_producto</c> y <c>_bodega</c> para llegar al kardex. La posición
/// es la de <see cref="PosicionDeReposicion"/> y el cálculo el de <c>CalculoDeReposicion</c>, por <see cref="EvaluacionDeReposicion"/>:
/// lo mismo que avisa la confirmación y levanta la revisión nocturna. Filtros <c>warehouse</c>, <c>category</c> y <c>product</c>;
/// alcance por bodega (<see cref="IAlcanceDeInventario"/>). No tiene columnas de costo. (nuevo)
/// </summary>
public sealed record ReorderAlertsReportQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ReorderAlertsReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, EvaluacionDeReposicion evaluacion)
    : IRequestHandler<ReorderAlertsReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T957): filtros <c>warehouse</c>, <c>category</c> y <c>product</c>, sin propios.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "reorder-alerts", "Reorden y quiebres", "Productos con la posición en o bajo su punto de reorden, o con el disponible bajo su mínimo, con el sugerido.",
        "reorden-y-quiebres", ["warehouse", "category", "product"], []);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Bodega", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Disponible", TipoDeColumna.Cantidad),
        new("En tránsito", TipoDeColumna.Cantidad),
        new("Por recibir", TipoDeColumna.Cantidad),
        new("Posición", TipoDeColumna.Cantidad),
        new("Mínimo", TipoDeColumna.Cantidad),
        new("Punto de reorden", TipoDeColumna.Cantidad),
        new("Máximo", TipoDeColumna.Cantidad),
        new("Sugerido", TipoDeColumna.Cantidad),
        new("Quiebre", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(ReorderAlertsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var politicas = db.ReorderPolicies.AsNoTracking().PorBodega(alcance, r => r.WarehouseId);
        string? bodegaFiltrada = null;
        if (f.Warehouse is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            politicas = politicas.Where(r => r.WarehouseId == bodega.Id);
            bodegaFiltrada = bodega.Code;
        }
        if (f.Category is { } cp)
        {
            var categoria = await db.ProductCategories.AsNoTracking().Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            politicas = politicas.Where(r => db.Products.Any(p => p.Id == r.ProductId && p.CategoryId == categoria));
        }
        if (f.Product is { } pp) politicas = politicas.Where(r => db.Products.Any(p => p.Id == r.ProductId && p.PublicId == pp));

        var filas = (await evaluacion.EvaluarAsync(politicas, ct))
            .Where(x => x.Resultado.Alerta)
            .Select(x => new FilaExportable(
            [
                x.WarehouseCode, $"{x.ProductCode} · {x.ProductName}",
                x.Posicion.Disponible, x.Posicion.EnTransito, x.Posicion.PorRecibir, x.Resultado.Posicion,
                x.Minimo, x.PuntoDeReorden, x.Maximo, x.Resultado.Sugerido, x.Resultado.Quiebre ? "Sí" : "No",
                x.ProductPublicId.ToString(), x.WarehousePublicId.ToString(),
            ], Resaltada: x.Resultado.Quiebre))
            .ToList();

        var subtitulo = bodegaFiltrada is null ? "Todas las bodegas del alcance" : $"Bodega {bodegaFiltrada}";
        return Result.Success(new TablaExportable("Reorden y quiebres", subtitulo, Columnas, filas, null,
        [
            "Posición = disponible + en tránsito hacia la bodega + por recibir (0 hasta la entrega de compras completas, I5).",
            "Sugerido = máximo − posición cuando la posición es igual o menor que el punto de reorden. Quiebre: disponible por debajo del mínimo.",
        ]));
    }
}
