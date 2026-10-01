using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>method-change-valuation</c> de <c>/api/reports/inventory</c> (feature 012, I5, US16, T844; FR-043; US16-4; contracts/api.md
/// §27): por grupo contable, el valorizado por promedio ponderado y por PEPS <b>al inicio</b> de la fecha del cambio (<c>asOf</c>, por
/// defecto hoy) y del período comparativo más antiguo que la cooperativa presente (<c>comparativeFrom</c>, opcional), la diferencia (PEPS −
/// promedio) y la nota de por qué algún producto no pudo calcularse. Lo calcula <see cref="ValorizacionPorDosMetodos"/> desde el kardex
/// de cada producto y ámbito: el método con que se registró sale del libro y el otro se reconstruye. El grupo es el del producto a cada
/// fecha (<see cref="GrupoContableALaFecha"/>); el corte del sistema anterior de un producto es la fecha de su primer saldo inicial. Es
/// lo que se entrega a Contabilidad para la NIC 8 o la Sección 10. Exige <c>Inventory.Costs.Read</c> (sin él, el 404 del informe). (nuevo)
/// </summary>
public sealed record MethodChangeValuationReportQuery(FiltrosDeInformeDeInventario Filtros, DateOnly? ComparativeFrom = null)
    : IRequest<Result<TablaExportable>>;

public sealed class MethodChangeValuationReportQueryHandler(
    IApplicationDbContext db, IPermissionChecker permisos, ILectorDeParametros parametros, IDateTimeService reloj)
    : IRequestHandler<MethodChangeValuationReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Grupo contable", TipoDeColumna.Texto),
        new("Fecha", TipoDeColumna.Fecha),
        new("Valor promedio ponderado", TipoDeColumna.Moneda),
        new("Valor PEPS", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
        new("Nota", TipoDeColumna.Texto),
    ];

    public async Task<Result<TablaExportable>> Handle(MethodChangeValuationReportQuery request, CancellationToken ct)
    {
        if (!await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct)) return Result.Failure<TablaExportable>(Error.NotFound);

        var hoy = reloj.HoyLocal;
        var fecha = request.Filtros.ALaFecha(hoy);
        if (request.ComparativeFrom is { } comparativo && comparativo > fecha)
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeInventario.RangoInvalido);
        var fechas = request.ComparativeFrom is { } desde ? new[] { desde, fecha } : [fecha];

        var montos = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, fecha, ct: ct);
        if (montos.IsFailure) return Result.Failure<TablaExportable>(montos.Error);

        // Todo lo anterior a la fecha más reciente: el valorizado es al inicio de cada fecha.
        var kardex = await db.KardexEntries.AsNoTracking().Where(k => k.OperationDate < fecha)
            .Select(k => new
            {
                k.Id, k.ProductId, k.CostScopeWarehouseId, k.DocumentId, k.OperationDate, k.Kind, k.Reason, k.QuantityBase, k.UnitCost,
                k.TotalCost, k.CostMethod, k.AffectsEntryId,
            })
            .OrderBy(k => k.OperationDate).ThenBy(k => k.Id)
            .ToListAsync(ct);
        var documentos = kardex.Select(k => k.DocumentId).Distinct().ToList();
        var saldosIniciales = (await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters()
                .Where(d => documentos.Contains(d.Id) && d.Class == DocumentClass.OpeningBalance).Select(d => d.Id).ToListAsync(ct))
            .ToHashSet();
        var productos = kardex.Select(k => k.ProductId).Distinct().ToList();

        var filas = new List<(string Grupo, ValorizacionDeGrupo Valor)>();
        foreach (var dia in fechas.Distinct().OrderBy(f => f))
        {
            var grupos = await GrupoContableALaFecha.DeAsync(db, productos, dia, ct);
            var historias = kardex.GroupBy(k => (k.ProductId, k.CostScopeWarehouseId)).Select(g =>
            {
                var ajustesSobreEntrada = new HashSet<(int, long)>();
                var movimientos = g.Select(k =>
                {
                    var clase = k.Kind switch
                    {
                        KardexEntryKind.Entry => ClaseAValorizar.Entrada,
                        KardexEntryKind.Exit => ClaseAValorizar.Salida,
                        _ when k.Reason is KardexReason.PriceDifference or KardexReason.LandedCost && k.AffectsEntryId is long entrada
                               && ajustesSobreEntrada.Add((k.DocumentId, entrada)) => ClaseAValorizar.AjusteSobreEntrada,
                        _ => ClaseAValorizar.OtroAjuste,
                    };
                    return new MovimientoAValorizar(k.Id, k.OperationDate, clase, k.QuantityBase, k.UnitCost, k.TotalCost, k.CostMethod, k.AffectsEntryId);
                }).ToList();
                var corte = g.Where(k => saldosIniciales.Contains(k.DocumentId)).Select(k => (DateOnly?)k.OperationDate).Min();
                return new HistoriaParaValorizar(g.Key.ProductId, g.Key.CostScopeWarehouseId, grupos.GetValueOrDefault(g.Key.ProductId) ?? 0, corte, movimientos);
            }).ToList();

            var resultado = ValorizacionPorDosMetodos.Calcular(historias, [dia], Redondeo.MontosDesde(montos.Value.Texto));
            var ids = resultado.Grupos.Select(r => r.AccountingGroupId).Distinct().ToList();
            var nombres = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => $"{x.Code} · {x.Name}", ct);
            filas.AddRange(resultado.Grupos.Select(r => (nombres.GetValueOrDefault(r.AccountingGroupId) ?? "Sin grupo contable", r)));
        }

        var tabla = filas
            .OrderBy(f => f.Grupo, StringComparer.Ordinal).ThenBy(f => f.Valor.Fecha)
            .Select(f => new FilaExportable(
            [
                f.Grupo,
                f.Valor.Fecha,
                f.Valor.PromedioPonderado,
                f.Valor.Peps,
                f.Valor.Diferencia,
                f.Valor.Nota,
            ], Resaltada: f.Valor.SinCalcular.Count > 0))
            .ToList();

        var subtitulo = request.ComparativeFrom is { } c
            ? $"Al inicio del {fecha:dd/MM/yyyy} y del {c:dd/MM/yyyy} (período comparativo)"
            : $"Al inicio del {fecha:dd/MM/yyyy}";
        return Result.Success(new TablaExportable("Valorizado por los dos métodos de costeo", subtitulo, Columnas, tabla, null,
            ["PEPS − promedio ponderado. El método con que se registró sale del kardex; el otro se reconstruye (NIC 8, Sección 10)."]));
    }
}
