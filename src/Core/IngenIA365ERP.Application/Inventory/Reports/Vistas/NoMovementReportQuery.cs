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
/// La vista <c>no-movement</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T963; FR-086; contracts/api.md §27): lo que tiene
/// existencia a <c>asOf</c> (hoy por defecto) y no se movió en <c>days</c> días o más —por defecto <c>Informes.DiasSinMovimiento</c> vigente a
/// esa fecha—, por producto y bodega, con el último movimiento del kardex, los días transcurridos, la cantidad y el valor (sólo con
/// <c>Inventory.Costs.Read</c>; sin él, vacío y la nota lo dice). Las ocultas <c>_producto</c> y <c>_bodega</c> llevan al kardex. Sin bodegas
/// de tránsito. El cálculo es <see cref="AnaliticaDeInventario.SinMovimientoAsync"/>, el mismo que suma el motivo «sin movimiento» a
/// <c>impairment</c>. Alcance por bodega (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record NoMovementReportQuery(FiltrosDeInformeDeInventario Filtros, int? Days = null) : IRequest<Result<TablaExportable>>;

public sealed class NoMovementReportQueryHandler(
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<NoMovementReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T966).</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "no-movement", "Sin movimiento", "Productos con existencia que no se movieron en los últimos N días, con su último movimiento.",
        "sin-movimiento", ["asOf", "branch", "warehouse", "product", "category", "accountingGroup"], ["days"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Último movimiento", TipoDeColumna.Fecha),
        new("Días sin movimiento", TipoDeColumna.Entero),
        new("Cantidad", TipoDeColumna.Cantidad),
        new("Valor", TipoDeColumna.Moneda),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(NoMovementReportQuery request, CancellationToken ct)
    {
        if (request.Days is < 0) return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("days", request.Days.ToString(), ["0 o más días"]));
        var f = request.Filtros;
        var fecha = f.ALaFecha(reloj.HoyLocal);
        var dias = request.Days ?? await DiasAsync(parametros, ParametrosDeInventario.InformesDiasSinMovimiento, fecha, ct);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);
        var conCostos = await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct);

        var quietos = await analitica.SinMovimientoAsync(ambito.Value, fecha, dias, ct);
        var productos = await analitica.ProductosAsync(quietos.Select(q => q.ProductId), ct);
        var bodegas = await analitica.BodegasAsync(quietos.Select(q => q.WarehouseId), ct);
        var filas = quietos
            .OrderByDescending(q => q.Dias).ThenBy(q => productos[q.ProductId].Code, StringComparer.Ordinal).ThenBy(q => bodegas[q.WarehouseId].Code, StringComparer.Ordinal)
            .Select(q => new FilaExportable(
            [
                productos[q.ProductId].Texto, bodegas[q.WarehouseId].Code, q.UltimoMovimiento, q.Dias == int.MaxValue ? null : q.Dias, q.Cantidad,
                conCostos ? q.Valor : null, productos[q.ProductId].PublicId.ToString(), bodegas[q.WarehouseId].PublicId.ToString(),
            ]))
            .ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, conCostos ? quietos.Sum(q => q.Valor) : null, null, null]);
        var notas = new List<string>
        {
            $"Sin movimiento: el último movimiento del kardex (entrada o salida) es de hace {dias} días o más al {fecha:yyyy-MM-dd}"
            + (request.Days is null ? " (Informes.DiasSinMovimiento)." : "."),
        };
        if (!conCostos) notas.Add(AnaliticaDeInventario.NotaSinCostos);
        return Result.Success(new TablaExportable("Sin movimiento", $"Al {fecha:yyyy-MM-dd} · {dias} días o más · {ambito.Value.Descripcion}",
            Columnas, filas, totales, notas));
    }

    /// <summary>Los días de un parámetro entero de informes vigente a la fecha; si no se puede leer, cero.</summary>
    internal static async Task<int> DiasAsync(ILectorDeParametros parametros, string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Como<int>() : 0;
    }
}
