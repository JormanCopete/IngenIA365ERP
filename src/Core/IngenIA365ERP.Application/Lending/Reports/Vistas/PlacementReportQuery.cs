using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>placement</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-133; contracts/api.md §17): la colocación del rango
/// —los desembolsos vivos (sin reversar) con fecha valor entre <c>from</c> y <c>to</c>— abierta por <c>groupBy=line|office|advisor|month</c>
/// (línea por defecto), con los créditos, el monto desembolsado y la tasa y el plazo promedio <b>ponderados por el monto</b> (la tasa efectiva
/// anual y el plazo en meses con que nació cada crédito, su primera versión de condiciones). El asesor es quien radicó la solicitud; los créditos
/// sin solicitud (migrados, de Comercial) van en «Sin asesor». Agregada: sin datos del deudor. El alcance va en la consulta de créditos
/// (<see cref="AmbitoDeInformeDeCartera"/>, <see cref="IAlcanceDeCartera"/>). (nuevo)
/// </summary>
public sealed record PlacementReportQuery(FiltrosDeInformeDeCartera Filtros, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class PlacementReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<PlacementReportQuery, Result<TablaExportable>>
{
    public const string PorLinea = "line";
    public const string PorOficina = "office";
    public const string PorAsesor = "advisor";
    public const string PorMes = "month";

    public static readonly IReadOnlyList<string> Dimensiones = [PorLinea, PorOficina, PorAsesor, PorMes];

    public const string SinAsesor = "Sin asesor";

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = new(
        "placement", "Colocación", "Créditos y monto desembolsado del rango por línea, oficina, asesor o mes, con la tasa y el plazo promedio ponderados.",
        "colocacion", ["from", "to", "branch", "line", "modality", "subsegment", "advisor"], ["groupBy"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión"),
        new("Créditos", TipoDeColumna.Entero),
        new("Monto desembolsado", TipoDeColumna.Moneda),
        new("Tasa promedio", TipoDeColumna.Porcentaje),
        new("Plazo promedio", TipoDeColumna.Decimal),
    ];

    public async Task<Result<TablaExportable>> Handle(PlacementReportQuery request, CancellationToken ct)
    {
        var dimension = FiltrosDeInformeDeCartera.Elegir("groupBy", request.GroupBy, Dimensiones, PorLinea);
        if (dimension.IsFailure) return Result.Failure<TablaExportable>(dimension.Error);

        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var (desde, hasta) = (f.Desde(hoy), f.Hasta(hoy));
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = ambito.Creditos(db);
        var desembolsos = await (from o in db.LoanOperations.AsNoTracking()
                                 join l in creditos on o.LoanId equals l.Id
                                 where o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null
                                       && o.ValueDate >= desde && o.ValueDate <= hasta
                                 select new { o.LoanId, o.Amount, o.ValueDate, l.BranchId, l.CreditLineId })
            .ToListAsync(ct);

        var ids = desembolsos.Select(d => d.LoanId).Distinct().ToList();
        var condiciones = await AmbitoDeInformeDeCartera.CondicionesInicialesAsync(db, ids, ct);
        desembolsos = desembolsos.Where(d => condiciones.TryGetValue(d.LoanId, out var c) && ambito.DeLaModalidad(c.ModalityCode, c.SubsegmentCode)).ToList();

        var nombre = await NombresAsync(dimension.Value, desembolsos.Select(d => (d.LoanId, d.BranchId, d.CreditLineId, d.ValueDate)).ToList(), ct);
        var filas = desembolsos
            .GroupBy(d => nombre((d.LoanId, d.BranchId, d.CreditLineId, d.ValueDate)))
            .Select(g => Fila(g.Key, g.Select(d => (d.LoanId, d.Amount)).ToList(), condiciones))
            .OrderBy(r => (string)r.Valores[0]!, StringComparer.CurrentCulture)
            .ToList();
        var totales = Fila("Total", desembolsos.Select(d => (d.LoanId, d.Amount)).ToList(), condiciones);
        return Result.Success(new TablaExportable("Colocación", $"Del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd} · por {Nombre(dimension.Value)} · {ambito.Descripcion()}",
            Columnas, filas, totales,
        [
            "Desembolsos sin reversar con fecha valor en el rango; un crédito con varios tramos cuenta una vez y suma todos sus tramos.",
            "Tasa promedio (efectiva anual pactada, %) y plazo promedio (meses) ponderados por el monto, con las condiciones con que nació cada crédito.",
        ]));
    }

    private static FilaExportable Fila(string dimension, IReadOnlyList<(int LoanId, decimal Monto)> tramos,
        IReadOnlyDictionary<int, Domain.Entities.Lending.Loans.LoanTermsVersion> condiciones)
    {
        var monto = tramos.Sum(t => t.Monto);
        decimal? tasa = null, plazo = null;
        if (monto != 0m)
        {
            tasa = Math.Round(tramos.Sum(t => t.Monto * (condiciones[t.LoanId].AnnualRate ?? condiciones[t.LoanId].ResultingEffectiveRate ?? 0m)) / monto * 100m, 4);
            plazo = Math.Round(tramos.Sum(t => t.Monto * condiciones[t.LoanId].TermMonths) / monto, 2);
        }
        return new FilaExportable([dimension, tramos.Select(t => t.LoanId).Distinct().Count(), monto, tasa, plazo]);
    }

    private static string Nombre(string dimension) => dimension switch
    {
        PorOficina => "oficina",
        PorAsesor => "asesor",
        PorMes => "mes",
        _ => "línea",
    };

    /// <summary>El nombre de la dimensión de cada desembolso.</summary>
    private async Task<Func<(int LoanId, int BranchId, int CreditLineId, DateOnly ValueDate), string>> NombresAsync(string dimension,
        IReadOnlyList<(int LoanId, int BranchId, int CreditLineId, DateOnly ValueDate)> desembolsos, CancellationToken ct)
    {
        switch (dimension)
        {
            case PorOficina:
            {
                var ids = desembolsos.Select(d => d.BranchId).Distinct().ToList();
                var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
                return d => oficinas.GetValueOrDefault(d.BranchId) ?? string.Empty;
            }
            case PorAsesor:
            {
                var asesores = await AmbitoDeInformeDeCartera.AsesoresAsync(db, desembolsos.Select(d => d.LoanId).Distinct().ToList(), ct);
                var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, asesores.Values, ct);
                return d => asesores.TryGetValue(d.LoanId, out var u) && usuarios.TryGetValue(u, out var nombre) ? nombre.Name : SinAsesor;
            }
            case PorMes:
                return d => d.ValueDate.ToString("yyyy-MM");
            default:
            {
                var ids = desembolsos.Select(d => d.CreditLineId).Distinct().ToList();
                var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id, l => $"{l.Code} · {l.Name}", ct);
                return d => lineas.GetValueOrDefault(d.CreditLineId) ?? string.Empty;
            }
        }
    }
}
