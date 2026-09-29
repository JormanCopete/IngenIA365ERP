using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Inventory.Analytics;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>turnover</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T961; FR-086; contracts/api.md §27): por
/// <c>by=product|category|accountingGroup</c> (producto por defecto), el costo de venta del período, el inventario promedio, la rotación y
/// los días de inventario de <see cref="IndicadoresDeRotacion"/>. El costo de venta es el del kardex de ventas, remisiones y devoluciones por su
/// fecha de operación; el inventario promedio, el de los saldos del día anterior, de cada fin de mes dentro del período y del último día, cada
/// uno por <c>ValorizadoALaFecha</c>, que parte de los cierres del período (<c>INV_PeriodClosingBalances</c>) y suma el kardex posterior
/// (<see cref="AnaliticaDeInventario.InventarioPromedioPorProductoAsync"/>). Un grupo se mide con sus sumas, no con el promedio de las rotaciones.
/// Exige <c>Inventory.Costs.Read</c>; alcance por bodega (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record TurnoverReportQuery(FiltrosDeInformeDeInventario Filtros, string? By = null) : IRequest<Result<TablaExportable>>;

public sealed class TurnoverReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    IDateTimeService reloj)
    : IRequestHandler<TurnoverReportQuery, Result<TablaExportable>>
{
    public const string PorProducto = "product";
    public const string PorCategoria = "category";
    public const string PorGrupoContable = "accountingGroup";

    public static readonly IReadOnlyList<string> Dimensiones = [PorProducto, PorCategoria, PorGrupoContable];

    public const string SinDato = "Sin dato";

    /// <summary>Lo que la vista declara al publicarse (T966): exige costos.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "turnover", "Rotación", "Costo de venta del período sobre el inventario promedio, con los días de inventario, por producto, categoría o grupo contable.",
        "rotacion", ["from", "to", "branch", "warehouse", "product", "category", "accountingGroup"], ["by"],
        RequiredPermission: AnaliticaDeInventario.PermisoDeCostos);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión", TipoDeColumna.Texto),
        new("Costo de venta del período", TipoDeColumna.Moneda),
        new("Inventario promedio", TipoDeColumna.Moneda),
        new("Rotación", TipoDeColumna.Decimal),
        new("Días de inventario", TipoDeColumna.Decimal),
    ];

    public async Task<Result<TablaExportable>> Handle(TurnoverReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);
        var by = string.IsNullOrWhiteSpace(request.By) ? PorProducto : request.By.Trim();
        var dimension = Dimensiones.FirstOrDefault(d => string.Equals(d, by, StringComparison.OrdinalIgnoreCase));
        if (dimension is null) return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("by", request.By, Dimensiones));

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);

        var costos = await analitica.CostoDeVentaPorProductoAsync(ambito.Value, desde, hasta, ct);
        var promedios = await analitica.InventarioPromedioPorProductoAsync(ambito.Value, desde, hasta, ct);
        var productos = await analitica.ProductosAsync(costos.Keys.Concat(promedios.Keys), ct);
        var nombre = await NombresAsync(dimension, productos, ct);
        var dias = IndicadoresDeRotacion.DiasDelPeriodo(desde, hasta);

        var filas = productos.Keys
            .Where(p => costos.GetValueOrDefault(p) != 0m || promedios.GetValueOrDefault(p) != 0m)
            .GroupBy(nombre)
            .Select(g => Fila(g.Key, g.Sum(p => costos.GetValueOrDefault(p)), g.Sum(p => promedios.GetValueOrDefault(p)), dias))
            .OrderBy(x => (string)x.Valores[0]!, StringComparer.CurrentCulture)
            .ToList();
        var totales = Fila("Total", costos.Values.Sum(), promedios.Values.Sum(), dias);
        return Result.Success(new TablaExportable("Rotación", $"{DatosDeVentasYCaja.Rango(f, hoy)} ({dias} días) · {ambito.Value.Descripcion}",
            Columnas, filas, totales,
        [
            "Rotación = costo de venta del período ÷ inventario promedio; días de inventario = días del período ÷ rotación.",
            "Inventario promedio = promedio de los saldos al costo del día anterior al período, de cada fin de mes y del último día (desde los cierres y el kardex).",
            "Sin inventario promedio no hay rotación: la fila queda sin dato.",
        ]));
    }

    private static FilaExportable Fila(string dimension, decimal costo, decimal promedio, int dias)
    {
        var r = IndicadoresDeRotacion.Calcular(costo, promedio, dias);
        return new FilaExportable([dimension, costo, promedio, r.Rotacion, r.DiasDeInventario]);
    }

    private async Task<Func<int, string>> NombresAsync(string dimension, IReadOnlyDictionary<int, ProductoDeAnalitica> productos, CancellationToken ct)
    {
        if (dimension == PorCategoria)
        {
            var ids = productos.Values.Select(p => p.CategoryId).Distinct().ToList();
            var categorias = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => $"{c.Code} · {c.Name}", ct);
            return p => categorias.GetValueOrDefault(productos[p].CategoryId) ?? MarginReportQueryHandler.SinCategoria;
        }
        if (dimension == PorGrupoContable)
        {
            var ids = productos.Values.Select(p => p.AccountingGroupId).OfType<int>().Distinct().ToList();
            var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => ids.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => $"{g.Code} · {g.Name}", ct);
            return p => productos[p].AccountingGroupId is int g && grupos.TryGetValue(g, out var texto) ? texto : "Sin grupo contable";
        }
        return p => productos[p].Texto;
    }
}
