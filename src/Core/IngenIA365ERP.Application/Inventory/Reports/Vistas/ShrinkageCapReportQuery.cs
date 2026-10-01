using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>shrinkage-cap</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T965; FR-086; contracts/api.md §27), opcional, para
/// el régimen ordinario de renta: por <c>year</c> (el del día por defecto), el inventario inicial (el valorizado al 31 de diciembre anterior,
/// que parte del cierre de diciembre), las compras del año al costo (recepciones, facturas y notas del proveedor, documentos soporte y sus
/// notas, costos adicionales, menos devoluciones), la base (inicial + compras), el tope = base × <c>Informes.TopeFaltantesPorcentaje</c>
/// vigente al 31 de diciembre del año con su norma (<c>LegalSource</c>), los faltantes y mermas del año (el costo de los ajustes negativos y
/// las bajas, detallados por causa en las notas) y el exceso sobre el tope. <b>Con el parámetro en 0</b> —su defecto— la vista dice que el
/// tope no está parametrizado y no lo calcula. Ningún porcentaje está en el programa. Exige <c>Inventory.Costs.Read</c>; alcance por bodega
/// (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record ShrinkageCapReportQuery(FiltrosDeInformeDeInventario Filtros, int? Year = null) : IRequest<Result<TablaExportable>>;

public sealed class ShrinkageCapReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<ShrinkageCapReportQuery, Result<TablaExportable>>
{
    public const string TopeNoParametrizado = "El tope no está parametrizado";

    /// <summary>Las clases cuyo kardex es compra del año (las devoluciones restan con su signo).</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeCompra =
    [
        DocumentClass.PurchaseReceipt, DocumentClass.SupplierInvoice, DocumentClass.SupplierNote, DocumentClass.SupportDocument,
        DocumentClass.SupportDocumentAdjustmentNote, DocumentClass.LandedCost, DocumentClass.SupplierReturn,
    ];

    /// <summary>Las clases cuyas salidas son faltantes y mermas.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeFaltantes = [DocumentClass.NegativeAdjustment, DocumentClass.WriteOff];

    /// <summary>Lo que la vista declara al publicarse (T966): exige costos.</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "shrinkage-cap", "Tope de faltantes y mermas",
        "Faltantes y mermas del año frente al tope sobre inventario inicial más compras (opcional, régimen ordinario de renta).",
        "tope-de-faltantes", ["branch", "warehouse"], ["year"], RequiredPermission: AnaliticaDeInventario.PermisoDeCostos);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Inventario inicial", TipoDeColumna.Moneda),
        new("Compras del año", TipoDeColumna.Moneda),
        new("Base", TipoDeColumna.Moneda),
        new("Tope legal (%)", TipoDeColumna.Porcentaje),
        new("Tope", TipoDeColumna.Moneda),
        new("Faltantes y mermas", TipoDeColumna.Moneda),
        new("Exceso", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(ShrinkageCapReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);
        var anio = request.Year ?? reloj.HoyLocal.Year;
        if (anio < DateOnly.MinValue.Year + 1 || anio > DateOnly.MaxValue.Year)
            return Result.Failure<TablaExportable>(AnaliticaDeInventario.FiltroInvalido("year", request.Year?.ToString(CultureInfo.InvariantCulture), ["un año"]));
        var inicio = new DateOnly(anio, 1, 1);
        var fin = new DateOnly(anio, 12, 31);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(request.Filtros, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);
        var a = ambito.Value;

        var inicial = (await analitica.ExistenciasAsync(a, inicio.AddDays(-1), sinTransito: false, ct)).Sum(x => x.Value);
        var kardex = db.KardexEntries.AsNoTracking().PorBodega(alcance, k => k.WarehouseId).Where(k => k.OperationDate >= inicio && k.OperationDate <= fin);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            kardex = kardex.Where(k => bodegas.Contains(k.WarehouseId));
        }
        var compras = await (from k in kardex
                             join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                             where ClasesDeCompra.Contains(d.Class)
                             select k.TotalCost).ToListAsync(ct);
        var faltantes = await (from k in kardex
                               where k.Kind == KardexEntryKind.Exit
                               join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                               where ClasesDeFaltantes.Contains(d.Class)
                               join l in db.InventoryDocumentLines.AsNoTracking().IgnoreQueryFilters() on k.DocumentLineId equals l.Id
                               select new { l.AdjustmentCauseId, k.TotalCost }).ToListAsync(ct);
        var causaIds = faltantes.Select(x => x.AdjustmentCauseId).OfType<int>().Distinct().ToList();
        var causas = await db.AdjustmentCauses.AsNoTracking().IgnoreQueryFilters().Where(c => causaIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.Code} · {c.Name}", ct);

        var comprasDelAnio = compras.Sum();
        var baseDelTope = inicial + comprasDelAnio;
        var mermas = -faltantes.Sum(x => x.TotalCost);

        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.InformesTopeFaltantesPorcentaje, fin, ct: ct);
        var fraccion = leido.IsSuccess ? leido.Value.Como<decimal>() : 0m;
        decimal? porcentaje = null, tope = null, exceso = null;
        var notas = new List<string>();
        if (fraccion > 0m)
        {
            porcentaje = Math.Round(fraccion * 100m, 2, MidpointRounding.AwayFromZero);
            tope = Math.Round(baseDelTope * fraccion, 2, MidpointRounding.AwayFromZero);
            exceso = Math.Max(0m, mermas - tope.Value);
            var fuente = leido.Value.Vigencia?.LegalSource;
            notas.Add($"Tope: Informes.TopeFaltantesPorcentaje vigente al {fin:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(fuente) ? "." : $" ({fuente})."));
        }
        else
        {
            notas.Add($"{TopeNoParametrizado}: Informes.TopeFaltantesPorcentaje está en 0. Regístrelo con su norma en /inventario/parametros para calcular el tope y el exceso.");
        }
        notas.Add("Base = inventario inicial (valorizado al 31 de diciembre anterior) + compras del año al costo (menos devoluciones a proveedores).");
        foreach (var g in faltantes.GroupBy(x => x.AdjustmentCauseId is int id ? causas.GetValueOrDefault(id) ?? "Sin causa" : "Sin causa")
                     .OrderBy(g => g.Key, StringComparer.CurrentCulture))
            notas.Add($"Faltantes y mermas por causa — {g.Key}: {(-g.Sum(x => x.TotalCost)).ToString("N2", CultureInfo.GetCultureInfo("es-CO"))}.");

        var fila = new FilaExportable([inicial, comprasDelAnio, baseDelTope, porcentaje, tope, mermas, exceso], Resaltada: exceso > 0m);
        return Result.Success(new TablaExportable("Tope de faltantes y mermas", $"Año {anio} · {a.Descripcion}", Columnas, [fila], null, notas));
    }
}
