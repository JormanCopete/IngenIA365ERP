using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// Lo que comparten los dos comparativos con SOLIDO (feature 012, T314; US4-6; api.md §27): las cifras vivas de un par (fecha,
/// bodega) —las del <b>lote más reciente</b> de ese par: importar otra vez deja las anteriores de baja— y los nombres. (nuevo)
/// </summary>
internal static class CifrasDeSolido
{
    public const string SinProducto = "Sin producto en el catálogo";

    /// <summary>Las cifras vivas a <paramref name="fechas"/> de <paramref name="bodegas"/>, sólo las del lote más reciente de cada par.</summary>
    public static async Task<List<LegacyFigure>> VigentesAsync(IApplicationDbContext db, IReadOnlyCollection<DateOnly> fechas,
        IReadOnlyCollection<int> bodegas, CancellationToken ct)
    {
        var filas = await db.LegacyFigures.AsNoTracking()
            .Where(x => fechas.Contains(x.AsOfDate) && x.WarehouseId != null && bodegas.Contains(x.WarehouseId.Value))
            .ToListAsync(ct);
        var ultimo = filas.GroupBy(x => (x.AsOfDate, x.WarehouseId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().ImportBatchPublicId);
        return filas.Where(x => ultimo[(x.AsOfDate, x.WarehouseId)] == x.ImportBatchPublicId).ToList();
    }

    public static decimal? Resta(decimal? a, decimal? b) => a is null && b is null ? null : (a ?? 0m) - (b ?? 0m);
}

// ------------------------------------------------------------------------------------------------ valorizado --

/// <summary>
/// La vista <c>legacy-comparison-valuation</c> (feature 012, T314; US4-6; api.md §27): a <c>asOf</c> (hoy por defecto), por grupo
/// contable, bodega y producto, cantidad y valor de SOLIDO (lote más reciente del par fecha–bodega) contra el valorizado del
/// módulo a esa fecha (<see cref="ValorizadoALaFecha"/>) y sus diferencias. Las cifras sin producto resuelto suman a su grupo
/// en una fila «Sin producto en el catálogo» por grupo y bodega; no aparecen por producto. Filtros <c>warehouse</c> y
/// <c>accountingGroup</c>; alcance por bodega. Exige <c>Inventory.Costs.Read</c> (sin él, el 404 genérico). (nuevo)
/// </summary>
public sealed record LegacyComparisonValuationQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class LegacyComparisonValuationQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    ValorizadoALaFecha valorizado,
    IDateTimeService reloj)
    : IRequestHandler<LegacyComparisonValuationQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Grupo", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto),
        new("Cantidad SOLIDO", TipoDeColumna.Cantidad),
        new("Valor SOLIDO", TipoDeColumna.Moneda),
        new("Cantidad módulo", TipoDeColumna.Cantidad),
        new("Valor módulo", TipoDeColumna.Moneda),
        new("Diferencia (cantidad)", TipoDeColumna.Cantidad),
        new("Diferencia (valor)", TipoDeColumna.Moneda),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    private sealed record Linea(int? GrupoId, int BodegaId, int? ProductoId, decimal? CantidadSolido, decimal? ValorSolido,
        decimal? CantidadModulo, decimal? ValorModulo, int CodigosSinResolver = 0);

    public async Task<Result<TablaExportable>> Handle(LegacyComparisonValuationQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);

        var f = request.Filtros;
        var fecha = f.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters()
            .Select(w => new { w.Id, w.PublicId, w.Code, w.Behavior }).ToListAsync(ct);
        var visibles = bodegas.Where(w => alcance.IncluyeBodega(w.Id) && w.Behavior != Domain.Enums.Inventory.WarehouseBehavior.Transit).ToList();
        if (f.Warehouse is { } wp)
        {
            var bodega = visibles.FirstOrDefault(w => w.PublicId == wp);
            if (bodega is null) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            visibles = [bodega];
        }
        int? grupoFiltro = null;
        if (f.AccountingGroup is { } gp)
        {
            grupoFiltro = await db.AccountingGroups.AsNoTracking().Where(g => g.PublicId == gp).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);
            if (grupoFiltro is null) return Result.Failure<TablaExportable>(CatalogErrors.AccountingGroupNotFound());
        }
        var ids = visibles.Select(w => w.Id).ToList();

        var cifras = await CifrasDeSolido.VigentesAsync(db, [fecha], ids, ct);
        var modulo = (await valorizado.CalcularAsync(fecha, null, ct)).Where(x => ids.Contains(x.WarehouseId)).ToList();
        var productoIds = cifras.Select(c => c.ProductId).OfType<int>().Concat(modulo.Select(m => m.ProductId)).Distinct().ToList();
        var grupos = await GrupoContableALaFecha.DeAsync(db, productoIds, fecha, ct);

        var lineas = new List<Linea>();
        var porProducto = cifras.Where(c => c.ProductId is not null)
            .GroupBy(c => (Bodega: c.WarehouseId!.Value, Producto: c.ProductId!.Value))
            .ToDictionary(g => g.Key, g => (Cantidad: g.Sum(x => x.Quantity ?? 0m), Valor: g.Sum(x => x.Value ?? 0m)));
        var delModulo = modulo.GroupBy(m => (Bodega: m.WarehouseId, Producto: m.ProductId))
            .ToDictionary(g => g.Key, g => (Cantidad: g.Sum(x => x.Quantity), Valor: g.Sum(x => x.Value)));
        foreach (var llave in porProducto.Keys.Union(delModulo.Keys))
        {
            var s = porProducto.TryGetValue(llave, out var v1) ? v1 : ((decimal, decimal)?)null;
            var m = delModulo.TryGetValue(llave, out var v2) ? v2 : ((decimal, decimal)?)null;
            lineas.Add(new Linea(grupos.GetValueOrDefault(llave.Producto), llave.Bodega, llave.Producto,
                s?.Item1, s?.Item2, m?.Item1, m?.Item2));
        }
        // Sin producto resuelto: suman a su grupo, una fila por grupo y bodega.
        foreach (var g in cifras.Where(c => c.ProductId is null).GroupBy(c => (c.AccountingGroupId, Bodega: c.WarehouseId!.Value)))
            lineas.Add(new Linea(g.Key.AccountingGroupId, g.Key.Bodega, null, g.Sum(x => x.Quantity ?? 0m), g.Sum(x => x.Value ?? 0m), null, null,
                g.Select(x => x.ProductCodeRaw).Distinct().Count()));
        if (grupoFiltro is int gf) lineas = lineas.Where(l => l.GrupoId == gf).ToList();

        var grupoIds = lineas.Select(l => l.GrupoId).OfType<int>().Distinct().ToList();
        var nombresDeGrupo = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(x => grupoIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code + " · " + x.Name, ct);
        var enLineas = lineas.Select(l => l.ProductoId).OfType<int>().Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => enLineas.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.PublicId, Texto = p.Code + " · " + p.Name }, ct);
        var porId = bodegas.ToDictionary(w => w.Id);

        var filas = lineas
            .Select(l => new
            {
                L = l,
                Grupo = l.GrupoId is int gi ? nombresDeGrupo.GetValueOrDefault(gi) ?? string.Empty : string.Empty,
                Bodega = porId[l.BodegaId],
                Producto = l.ProductoId is int pi ? productos[pi].Texto : $"{CifrasDeSolido.SinProducto} ({l.CodigosSinResolver} código(s))",
            })
            .OrderBy(x => x.Grupo, StringComparer.Ordinal).ThenBy(x => x.Bodega.Code, StringComparer.Ordinal)
            .ThenBy(x => x.L.ProductoId is null ? 1 : 0).ThenBy(x => x.Producto, StringComparer.Ordinal)
            .Select(x => new FilaExportable(
            [
                x.Grupo, x.Bodega.Code, x.Producto,
                x.L.CantidadSolido, x.L.ValorSolido, x.L.CantidadModulo, x.L.ValorModulo,
                CifrasDeSolido.Resta(x.L.CantidadSolido, x.L.CantidadModulo), CifrasDeSolido.Resta(x.L.ValorSolido, x.L.ValorModulo),
                x.L.ProductoId is int pi ? productos[pi].PublicId.ToString() : null, x.Bodega.PublicId.ToString(),
            ]))
            .ToList();

        decimal Suma(Func<Linea, decimal?> campo) => lineas.Sum(l => campo(l) ?? 0m);
        var totales = new FilaExportable(
        [
            "Total", null, null, Suma(l => l.CantidadSolido), Suma(l => l.ValorSolido), Suma(l => l.CantidadModulo), Suma(l => l.ValorModulo),
            Suma(l => l.CantidadSolido) - Suma(l => l.CantidadModulo), Suma(l => l.ValorSolido) - Suma(l => l.ValorModulo), null, null,
        ]);

        return Result.Success(new TablaExportable("Comparativo de valorizado con SOLIDO", $"Al {fecha:yyyy-MM-dd}", Columnas, filas, totales,
        [
            "SOLIDO: las cifras importadas a esa fecha, del lote más reciente de cada bodega. Módulo: el valorizado a la misma fecha.",
            "Diferencia = SOLIDO − módulo. Las cifras cuyo producto no está en el catálogo nuevo suman a su grupo, sin detalle por producto.",
        ]));
    }
}

// ---------------------------------------------------------------------------------------------------- kardex --

/// <summary>
/// La vista <c>legacy-comparison-kardex</c> (feature 012, T314; US4-6; api.md §27): para una bodega (<c>warehouse</c>,
/// obligatoria), en cada fecha con cifras de SOLIDO dentro de <c>from</c>–<c>to</c>, por producto, la cantidad y el valor de
/// SOLIDO contra los del módulo a esa fecha en esa bodega. Filtro <c>product</c>. Sin <c>Inventory.Costs.Read</c> las columnas
/// de valor van vacías y la nota lo dice. Las cifras sin producto resuelto no entran (van en el comparativo de valorizado). (nuevo)
/// </summary>
public sealed record LegacyComparisonKardexQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class LegacyComparisonKardexQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    ValorizadoALaFecha valorizado,
    IDateTimeService reloj)
    : IRequestHandler<LegacyComparisonKardexQuery, Result<TablaExportable>>
{
    public const string BodegaObligatoria = "La vista legacy-comparison-kardex exige la bodega (warehouse).";

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Fecha", TipoDeColumna.Fecha),
        new("Cantidad SOLIDO", TipoDeColumna.Cantidad),
        new("Cantidad módulo", TipoDeColumna.Cantidad),
        new("Diferencia (cantidad)", TipoDeColumna.Cantidad),
        new("Valor SOLIDO", TipoDeColumna.Moneda),
        new("Valor módulo", TipoDeColumna.Moneda),
        new("Diferencia (valor)", TipoDeColumna.Moneda),
        new("Producto", TipoDeColumna.Texto, "_producto"),
    ];

    public async Task<Result<TablaExportable>> Handle(LegacyComparisonKardexQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        if (f.Warehouse is not { } wp) return Result.Failure<TablaExportable>(new Error(Error.Validation.Code, BodegaObligatoria));
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
        if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());

        int? productoFiltro = null;
        if (f.Product is { } pp)
        {
            productoFiltro = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (productoFiltro is null) return Result.Failure<TablaExportable>(CatalogErrors.ProductNotFound());
        }

        var conCostos = await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct);
        var desde = f.From ?? DateOnly.MinValue;
        var hasta = f.To ?? reloj.HoyLocal;
        var fechas = await db.LegacyFigures.AsNoTracking()
            .Where(x => x.WarehouseId == bodega.Id && x.AsOfDate >= desde && x.AsOfDate <= hasta)
            .Select(x => x.AsOfDate).Distinct().ToListAsync(ct);

        var cifras = (await CifrasDeSolido.VigentesAsync(db, fechas, [bodega.Id], ct))
            .Where(c => c.ProductId is not null && (productoFiltro is null || c.ProductId == productoFiltro))
            .ToList();

        var filas = new List<(DateOnly Fecha, int Producto, decimal? Cs, decimal? Cm, decimal? Vs, decimal? Vm)>();
        foreach (var fecha in fechas.Order())
        {
            var delDia = cifras.Where(c => c.AsOfDate == fecha)
                .GroupBy(c => c.ProductId!.Value)
                .ToDictionary(g => g.Key, g => (Cantidad: g.Sum(x => x.Quantity ?? 0m), Valor: g.Sum(x => x.Value ?? 0m)));
            var modulo = (await valorizado.CalcularAsync(fecha, productoFiltro is int p0 ? [p0] : null, ct))
                .Where(m => m.WarehouseId == bodega.Id)
                .GroupBy(m => m.ProductId)
                .ToDictionary(g => g.Key, g => (Cantidad: g.Sum(x => x.Quantity), Valor: g.Sum(x => x.Value)));
            foreach (var producto in delDia.Keys.Union(modulo.Keys))
            {
                var s = delDia.TryGetValue(producto, out var a) ? a : ((decimal, decimal)?)null;
                var m = modulo.TryGetValue(producto, out var b) ? b : ((decimal, decimal)?)null;
                filas.Add((fecha, producto, s?.Item1, m?.Item1, s?.Item2, m?.Item2));
            }
        }

        var ids = filas.Select(x => x.Producto).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.PublicId, Texto = p.Code + " · " + p.Name }, ct);

        var tabla = filas
            .OrderBy(x => productos[x.Producto].Texto, StringComparer.Ordinal).ThenBy(x => x.Fecha)
            .Select(x => new FilaExportable(
            [
                productos[x.Producto].Texto, bodega.Code, x.Fecha,
                x.Cs, x.Cm, CifrasDeSolido.Resta(x.Cs, x.Cm),
                conCostos ? x.Vs : null, conCostos ? x.Vm : null, conCostos ? CifrasDeSolido.Resta(x.Vs, x.Vm) : null,
                productos[x.Producto].PublicId.ToString(),
            ]))
            .ToList();

        var notas = new List<string>
        {
            "En cada fecha con cifras de SOLIDO de la bodega: SOLIDO (lote más reciente) contra el módulo a esa misma fecha. Diferencia = SOLIDO − módulo.",
        };
        if (!conCostos) notas.Add("Sin el permiso de ver costos (Inventory.Costs.Read) las columnas de valor van vacías.");
        return Result.Success(new TablaExportable("Comparativo de kardex con SOLIDO", $"Bodega {bodega.Code}", Columnas, tabla, null, notas));
    }
}
