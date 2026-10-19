using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Reports;

/// <summary>
/// La vista <c>deposits-reconciliation</c> (feature 014, US8, T146; api.md §18.1; contabilidad.md §8.1 de la 014; FR-100): la conciliación contable de las
/// captaciones y los aportes al corte <c>asOf</c> (y la oficina, si viene), por conjunto de cuentas: las líneas, el concepto (rol y clase), las oficinas, el
/// saldo de la Cartera, el contable, la diferencia y lo que la explican los hechos que Contabilidad todavía no recibe. Es la misma conciliación de los
/// créditos (<see cref="ConciliacionContableDeCartera"/>) filtrada a los conjuntos de captaciones; sin datos de titulares; exige además
/// <c>Lending.Reconciliation.View</c>. (nuevo)
/// </summary>
public sealed record DepositsReconciliationReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class DepositsReconciliationReportQueryHandler(
    IApplicationDbContext db, ConciliacionContableDeCartera conciliacion, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DepositsReconciliationReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = new("deposits-reconciliation", "Conciliación de captaciones",
        "Los saldos de captaciones y aportes contra la contabilidad al corte, por línea, concepto y oficina, con lo que explica cada diferencia.",
        "conciliacion-de-captaciones", ["asOf", "branch"], AvailableFrom: EntregaDeCartera.K7, RequiredPermission: "Lending.Reconciliation.View");

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Línea"),
        new("Concepto"),
        new("Oficina"),
        new("Saldo Cartera", TipoDeColumna.Moneda),
        new("Saldo contable", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
        new("Explicada", TipoDeColumna.Moneda),
        new("Cuentas contables"),
    ];

    public async Task<Result<TablaExportable>> Handle(DepositsReconciliationReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (request.Filtros.Branch is { } sucursal)
        {
            var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == sucursal && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (oficina is not { } id || !alcance.IncluyeOficina(id)) return Result.Failure<TablaExportable>(Error.NotFound);
            alcance = alcance with { TodasLasOficinas = false, Oficinas = new HashSet<int> { id } };
        }
        var conciliada = await conciliacion.ConciliarAsync(corte, alcance, ct);
        if (conciliada.IsFailure) return Result.Failure<TablaExportable>(conciliada.Error);

        var roles = SaldosContablesDeCaptaciones.RolesDelLibro.ToHashSet(StringComparer.Ordinal);
        var conjuntos = conciliada.Value.Sets.Where(s => s.Combinations.Any(c => roles.Contains(c.Role))).ToList();
        var oficinas = conjuntos.SelectMany(s => s.Combinations.Select(c => c.Branch)).Distinct().ToList();
        var nombres = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Name, ct);

        var filas = conjuntos.Select(s => new FilaExportable(
        [
            Unir(s.Combinations.Select(c => c.CreditLine)),
            Unir(s.Combinations.Select(Concepto)),
            Unir(s.Combinations.Select(c => nombres.GetValueOrDefault(c.Branch))),
            s.LendingBalance, s.AccountingBalance, s.Difference,
            s.ExplainedBy.Pending + s.ExplainedBy.InBatch + s.ExplainedBy.Rejected + s.ExplainedBy.NotPosted,
            Unir(s.Accounts.Select(a => a.Code)),
        ])).ToList();
        var totales = new FilaExportable(["Total", null, null, conjuntos.Sum(s => s.LendingBalance), conjuntos.Sum(s => s.AccountingBalance),
            conjuntos.Sum(s => s.Difference), conjuntos.Sum(s => s.ExplainedBy.Pending + s.ExplainedBy.InBatch + s.ExplainedBy.Rejected + s.ExplainedBy.NotPosted),
            null]);
        var sinRegla = conciliada.Value.UnresolvedCombinations.Count(c => roles.Contains(c.Role));
        List<string> notas =
        [
            "El saldo de la Cartera es la suma del libro de cuentas por fecha contable; el contable, el de las cuentas que la matriz asigna a cada combinación. "
            + "Lo explicado son los hechos que Contabilidad todavía no recibe (pendientes, en lote, rechazados o que no pasan).",
            "La retención y el GMF por pagar no se concilian aquí: se concilian con la declaración (la cuenta contable baja al pagar a la DIAN).",
        ];
        if (sinRegla > 0) notas.Add($"{sinRegla} combinación(es) de captaciones no tienen regla en la matriz: complétela en Contabilidad › Cartera › Matriz.");
        return Result.Success(new TablaExportable("Conciliación de captaciones", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales, notas));
    }

    private static string Concepto(CombinacionConciliadaDto c) =>
        (c.Role switch
        {
            SaldosContablesDeCaptaciones.Capital => "Capital",
            SaldosContablesDeCaptaciones.CapitalInactiva => "Capital (inactivas)",
            SaldosContablesDeCaptaciones.InteresPorPagar => "Interés por pagar",
            SaldosContablesDeCaptaciones.Aportes => "Aportes",
            SaldosContablesDeCaptaciones.CargoPorCobrar => "Cargos por cobrar",
            _ => c.Role,
        }) + (c.ContributionClass ?? c.DepositClass) switch { { } clase => $" · {clase}", _ => string.Empty }
           + (c.AccountFlag is { } marca && marca != "Standard" ? $" · {marca}" : string.Empty);

    private static string Unir(IEnumerable<string?> valores) =>
        string.Join(", ", valores.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
