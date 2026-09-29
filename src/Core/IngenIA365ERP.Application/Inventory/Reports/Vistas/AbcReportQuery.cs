using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Inventory.Analytics;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>abc</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T962; FR-086; contracts/api.md §27): el análisis ABC de los
/// productos del período por <c>basis=sales</c> (ventas netas, por defecto) o <c>basis=consumption</c> (salidas al costo, sin traslados ni
/// anulaciones), clasificado por <see cref="ClasificacionAbc"/> —el único motor del módulo, el mismo del conteo cíclico, sin copiar su
/// lógica— con los umbrales de <c>Informes.UmbralesAbc</c> vigentes en <c>to</c> (por <see cref="ILectorDeParametros"/>). Columnas Producto,
/// Valor, Participación (%), Acumulado (%) y Clase, y la oculta <c>_producto</c>. Sólo los productos con valor en el período. Por consumo el
/// valor es un costo: sin <c>Inventory.Costs.Read</c> sale vacío (la participación y la clase sí). Alcance por bodega y punto
/// (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record AbcReportQuery(FiltrosDeInformeDeInventario Filtros, string? Basis = null) : IRequest<Result<TablaExportable>>;

public sealed class AbcReportQueryHandler(
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<AbcReportQuery, Result<TablaExportable>>
{
    public const string PorVentas = "sales";
    public const string PorConsumo = "consumption";

    public static readonly IReadOnlyList<string> Bases = [PorVentas, PorConsumo];

    /// <summary>Lo que la vista declara al publicarse (T966).</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "abc", "Análisis ABC", "Los productos del período ordenados por ventas o por consumo, con su participación, el acumulado y su clase A, B o C.",
        "analisis-abc", ["from", "to", "branch", "warehouse", "pointOfSale", "category", "accountingGroup"], ["basis"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Valor", TipoDeColumna.Moneda),
        new("Participación (%)", TipoDeColumna.Porcentaje),
        new("Acumulado (%)", TipoDeColumna.Porcentaje),
        new("Clase", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto, "_producto"),
    ];

    public async Task<Result<TablaExportable>> Handle(AbcReportQuery request, CancellationToken ct)
    {
        var texto = string.IsNullOrWhiteSpace(request.Basis) ? PorVentas : request.Basis.Trim();
        var basis = Bases.FirstOrDefault(b => string.Equals(b, texto, StringComparison.OrdinalIgnoreCase));
        if (basis is null) return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("basis", request.Basis, Bases));

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesUmbralesAbc, hasta, ct: ct);
        if (leido.IsFailure) return Result.Failure<TablaExportable>(leido.Error);
        var umbrales = UmbralesAbc.Interpretar(leido.Value.Texto);
        if (!umbrales.Admitido) return Result.Failure<TablaExportable>(new Error(umbrales.Codigo!, umbrales.Mensaje!));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);

        Dictionary<int, decimal> valores;
        if (basis == PorVentas)
            valores = (await analitica.LineasDeVentaAsync(ambito.Value, desde, hasta, ct))
                .GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.VentaNeta));
        else
            valores = await analitica.ConsumoPorProductoAsync(ambito.Value, desde, hasta, ct);
        valores = valores.Where(v => v.Value != 0m).ToDictionary(v => v.Key, v => v.Value);

        var conCostos = basis == PorVentas || await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct);
        var productos = await analitica.ProductosAsync(valores.Keys, ct);
        var clasificados = ClasificacionAbc.Clasificar(valores.Select(v => new ValorParaAbc<string>(productos[v.Key].Code, v.Value)).ToList(), umbrales.Umbrales!);
        var porCodigo = productos.Values.ToDictionary(p => p.Code, StringComparer.Ordinal);

        var filas = clasificados.Select(c => new FilaExportable(
        [
            porCodigo[c.Clave].Texto, conCostos ? c.Valor : null, c.Participacion, c.Acumulado, c.Clase.ToString(), porCodigo[c.Clave].PublicId.ToString(),
        ])).ToList();
        var total = valores.Values.Sum();
        var totales = new FilaExportable(["Total", conCostos ? total : null, null, null, null, null]);
        var u = umbrales.Umbrales!;
        var notas = new List<string>
        {
            basis == PorVentas
                ? "Valor = ventas netas del período (ventas − notas, sin impuestos)."
                : "Valor = costo de las salidas del período (sin traslados, movimientos entre ubicaciones ni anulaciones).",
            $"Umbrales de Informes.UmbralesAbc vigentes al {hasta:yyyy-MM-dd}: A hasta {u.A} %, B hasta {u.A + u.B} % del acumulado; el que cruza un umbral pasa a la clase siguiente y el primero siempre es A.",
        };
        if (!conCostos) notas.Add(AnaliticaDeInventario.NotaSinCostos);
        return Result.Success(new TablaExportable("Análisis ABC", $"{DatosDeVentasYCaja.Rango(f, hoy)} · por {(basis == PorVentas ? "ventas" : "consumo")} · {ambito.Value.Descripcion}",
            Columnas, filas, totales, notas));
    }
}
