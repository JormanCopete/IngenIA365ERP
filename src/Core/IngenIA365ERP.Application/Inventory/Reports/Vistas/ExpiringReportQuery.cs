using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>expiring</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T963; FR-086; contracts/api.md §27): los lotes con
/// existencia que vencen entre <c>asOf</c> (hoy por defecto) y <c>days</c> días después —por defecto <c>Informes.DiasProximoAVencer</c> vigente a
/// esa fecha—, por lote y bodega, desde la existencia por lote (<c>INV_StockDetails</c>), con los días que faltan, la cantidad y el valor al
/// costo promedio de la bodega (sólo con <c>Inventory.Costs.Read</c>). Los vencidos no son «por vencer»: salen en <c>impairment</c>. Ocultas
/// <c>_producto</c> y <c>_bodega</c> (PublicId) y <c>_lote</c> (el código del lote, que es lo que filtra el kardex con <c>?lot=</c>). Es el mismo cálculo de la ficha <c>expiringSoon</c> del tablero
/// (<see cref="AnaliticaDeInventario.LotesPorVencerAsync"/>). Alcance por bodega (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record ExpiringReportQuery(FiltrosDeInformeDeInventario Filtros, int? Days = null) : IRequest<Result<TablaExportable>>;

public sealed class ExpiringReportQueryHandler(
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<ExpiringReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T966).</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "expiring", "Próximos a vencer", "Lotes con existencia que vencen en los próximos N días, con la cantidad y el valor por bodega.",
        "proximos-a-vencer", ["asOf", "branch", "warehouse", "product", "category", "accountingGroup"], ["days"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Lote", TipoDeColumna.Texto),
        new("Vence", TipoDeColumna.Fecha),
        new("Días", TipoDeColumna.Entero),
        new("Bodega", TipoDeColumna.Texto),
        new("Cantidad", TipoDeColumna.Cantidad),
        new("Valor", TipoDeColumna.Moneda),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Lote", TipoDeColumna.Texto, "_lote"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(ExpiringReportQuery request, CancellationToken ct)
    {
        if (request.Days is < 0) return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("days", request.Days.ToString(), ["0 o más días"]));
        var f = request.Filtros;
        var fecha = f.ALaFecha(reloj.HoyLocal);
        var dias = request.Days ?? await NoMovementReportQueryHandler.DiasAsync(parametros, ParametrosDeInventario.InformesDiasProximoAVencer, fecha, ct);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);
        var conCostos = await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct);

        var lotes = await analitica.LotesPorVencerAsync(ambito.Value, fecha, dias, ct);
        var costos = conCostos && lotes.Count > 0 ? await analitica.CostosPromedioAsync(ambito.Value, fecha, ct) : [];
        var productos = await analitica.ProductosAsync(lotes.Select(l => l.ProductId), ct);
        var bodegas = await analitica.BodegasAsync(lotes.Select(l => l.WarehouseId), ct);
        decimal? Valor(LotePorVencer l) => conCostos
            ? Math.Round(l.Cantidad * costos.GetValueOrDefault((l.ProductId, l.WarehouseId)), 2, MidpointRounding.AwayFromZero)
            : null;

        var filas = lotes.Select(l => new FilaExportable(
        [
            productos[l.ProductId].Texto, l.LotCode, l.Vence, l.Vence.DayNumber - fecha.DayNumber, bodegas[l.WarehouseId].Code, l.Cantidad, Valor(l),
            productos[l.ProductId].PublicId.ToString(), l.LotCode, bodegas[l.WarehouseId].PublicId.ToString(),
        ])).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, null, conCostos ? lotes.Sum(l => Valor(l)) : null, null, null, null]);
        var notas = new List<string>
        {
            $"Lotes que vencen del {fecha:yyyy-MM-dd} al {fecha.AddDays(dias):yyyy-MM-dd}" + (request.Days is null ? " (Informes.DiasProximoAVencer)." : ".")
            + " Los vencidos salen en los indicios de deterioro.",
            "Valor = cantidad del lote × costo promedio del producto en la bodega a la fecha.",
        };
        if (!conCostos) notas.Add(AnaliticaDeInventario.NotaSinCostos);
        return Result.Success(new TablaExportable("Próximos a vencer", $"Al {fecha:yyyy-MM-dd} · {dias} días · {ambito.Value.Descripcion}",
            Columnas, filas, totales, notas));
    }
}
