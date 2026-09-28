using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>impairment</c> de <c>/api/reports/inventory</c> (feature 012, I3, T624; FR-021; contracts/api.md §27;
/// decisiones-transversales §2.12): los <b>indicios de deterioro</b> a <c>asOf</c> (hoy por defecto), por producto y bodega. El costo es
/// el promedio del valorizado a la fecha (<see cref="ValorizadoALaFecha"/>, el mismo de <c>valuation</c>); el valor neto realizable
/// es el precio de la <b>lista general</b> vigente a esa fecha en la unidad base del producto (<see cref="ResolutorDeListaDePrecios"/>
/// sin cliente, segmento, canal ni sucursal) menos los gastos de venta estimados <c>Informes.DeterioroPorcentajeGastosVenta</c> (fracción,
/// por <see cref="ILectorDeParametros"/>). Una lista que incluye impuestos se lleva a precio sin IVA con las tarifas de IVA de venta
/// del producto vigentes a la fecha.
/// <list type="bullet">
/// <item>Sale una fila sólo si el costo unitario supera el valor neto realizable (motivo «Costo sobre valor neto realizable»), con el
/// indicio = (costo − VNR) × cantidad.</item>
/// <item>Un producto con existencia y sin precio en la lista general sale con el motivo que dice qué precio falta (producto y unidad):
/// sin él no se puede medir.</item>
/// </list>
/// En I6 le suman «vencido o próximo a vencer» (T933, US15) y «sin movimiento» (T963, US17). Exige <c>Inventory.Costs.Read</c> (sin él,
/// el 404 genérico) y respeta el alcance por bodega; las bodegas de tránsito no entran. (nuevo)
/// </summary>
public sealed record ImpairmentReportQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ImpairmentReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    ValorizadoALaFecha valorizado,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<ImpairmentReportQuery, Result<TablaExportable>>
{
    public const string MotivoCostoSobreVnr = "Costo sobre valor neto realizable";

    /// <summary>Lo que la vista declara al publicarse (T625): <c>asOf</c>, <c>warehouse</c> y <c>product</c>; exige <c>Inventory.Costs.Read</c>.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "impairment", "Indicios de deterioro",
        "Por producto y bodega a una fecha: costo promedio frente al precio de la lista general menos los gastos de venta; sólo lo que cuesta más de lo que se recupera.",
        "indicios-de-deterioro", ["asOf", "warehouse", "product"], [], RequiredPermission: ValuationReportQueryHandler.PermisoDeCostos);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Cantidad", TipoDeColumna.Cantidad),
        new("Costo unitario", TipoDeColumna.Costo),
        new("Precio de la lista general", TipoDeColumna.Moneda),
        new("Gastos de venta", TipoDeColumna.Moneda),
        new("Valor neto realizable", TipoDeColumna.Moneda),
        new("Indicio (valor)", TipoDeColumna.Moneda),
        new("Motivo", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto, "_producto"),
    ];

    public async Task<Result<TablaExportable>> Handle(ImpairmentReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(ValuationReportQueryHandler.PermisoDeCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);

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
        IReadOnlyCollection<int>? productosFiltro = null;
        if (f.Product is { } pp)
            productosFiltro = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == pp).Select(p => p.Id).ToListAsync(ct);

        var gastos = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesDeterioroPorcentajeGastosVenta, fecha, ct: ct);
        var porcentaje = gastos.IsSuccess ? gastos.Value.Como<decimal>() : 0m;

        var existencias = (await valorizado.CalcularAsync(fecha, productosFiltro, ct))
            .Where(x => x.Quantity > 0m && alcance.IncluyeBodega(x.WarehouseId) && (bodegaFiltro is null || x.WarehouseId == bodegaFiltro))
            .ToList();
        var bodegaIds = existencias.Select(x => x.WarehouseId).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => bodegaIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new { w.Code, w.Behavior }, ct);
        existencias = existencias.Where(x => bodegas[x.WarehouseId].Behavior != WarehouseBehavior.Transit).ToList();

        var productoIds = existencias.Select(x => x.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.Code, p.Name, p.BaseUnitId, Unidad = p.BaseUnit != null ? p.BaseUnit.Code : string.Empty })
            .ToDictionaryAsync(p => p.Id, ct);

        var contexto = new ContextoDePrecio(null, null, null, null, fecha);
        var (candidatas, _) = await ResolucionDePrecios.CandidatasAsync(db, contexto, productoIds, ct);
        var ivaDeVenta = await IvaDeVentaAsync(productoIds, fecha, ct);

        var filas = new List<(string Orden, FilaExportable Fila)>();
        foreach (var x in existencias)
        {
            var p = productos[x.ProductId];
            var texto = $"{p.Code} · {p.Name}";
            var bodega = bodegas[x.WarehouseId].Code;
            var precio = ResolutorDeListaDePrecios.Resolver(candidatas, contexto, p.Id, p.BaseUnitId);
            if (!precio.Found)
            {
                filas.Add(($"{texto}|{bodega}", new FilaExportable(
                [
                    texto, bodega, x.Quantity, x.AverageCost, null, null, null, null,
                    $"Falta el precio de {p.Code} en {p.Unidad} en la lista general: no se puede medir el valor neto realizable.",
                    p.PublicId.ToString(),
                ])));
                continue;
            }

            var bruto = precio.Price!.Value;
            var sinImpuestos = precio.IncludesTaxes
                ? Math.Round(bruto / (1m + ivaDeVenta.GetValueOrDefault(p.Id)), 2, MidpointRounding.AwayFromZero)
                : bruto;
            var gastosDeVenta = Math.Round(sinImpuestos * porcentaje, 2, MidpointRounding.AwayFromZero);
            var vnr = sinImpuestos - gastosDeVenta;
            if (x.AverageCost <= vnr) continue;
            var indicio = Math.Round((x.AverageCost - vnr) * x.Quantity, 2, MidpointRounding.AwayFromZero);
            filas.Add(($"{texto}|{bodega}", new FilaExportable(
                [texto, bodega, x.Quantity, x.AverageCost, sinImpuestos, gastosDeVenta, vnr, indicio, MotivoCostoSobreVnr, p.PublicId.ToString()],
                Resaltada: true)));
        }

        var ordenadas = filas.OrderBy(x => x.Orden, StringComparer.Ordinal).Select(x => x.Fila).ToList();
        var total = ordenadas.Sum(r => r.Valores[7] is decimal d ? d : 0m);
        var totales = new FilaExportable(["Total", null, null, null, null, null, null, total, null, null]);
        var subtitulo = $"Al {fecha:yyyy-MM-dd} · gastos de venta {porcentaje:P2}" + (bodegaFiltrada is null ? " · todas las bodegas del alcance" : $" · bodega {bodegaFiltrada}");
        return Result.Success(new TablaExportable("Indicios de deterioro", subtitulo, Columnas, ordenadas, totales,
        [
            "Valor neto realizable = precio de la lista general (sin IVA) − gastos de venta estimados (Informes.DeterioroPorcentajeGastosVenta).",
            "Es un indicio para el cálculo del deterioro (NIC 2 / sección 13): no registra ningún ajuste.",
        ]));
    }

    /// <summary>La suma de las tarifas porcentuales de IVA de venta vigentes a la fecha de cada producto (para quitarlas de una lista con impuestos).</summary>
    private async Task<Dictionary<int, decimal>> IvaDeVentaAsync(IReadOnlyCollection<int> productoIds, DateOnly fecha, CancellationToken ct)
    {
        if (productoIds.Count == 0) return [];
        var impuestos = await (from pt in db.ProductTaxes.AsNoTracking()
                               join d in db.TaxDefinitions.AsNoTracking() on pt.TaxDefinitionId equals d.Id
                               where productoIds.Contains(pt.ProductId) && d.Kind == TaxKind.Iva && pt.AppliesTo != TaxAppliesTo.Purchases && pt.TaxRateCode != null
                               select new { pt.ProductId, pt.TaxRateCode }).ToListAsync(ct);
        if (impuestos.Count == 0) return [];
        var codigos = impuestos.Select(i => i.TaxRateCode!).Distinct().ToList();
        var tarifas = (await db.TaxRates.AsNoTracking().Where(t => codigos.Contains(t.Code) && t.Rate != null).ToListAsync(ct))
            .Where(t => t.VigenteEn(fecha))
            .GroupBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.ValidFrom).First().Rate!.Value, StringComparer.OrdinalIgnoreCase);
        return impuestos.GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => tarifas.GetValueOrDefault(i.TaxRateCode!)));
    }
}
