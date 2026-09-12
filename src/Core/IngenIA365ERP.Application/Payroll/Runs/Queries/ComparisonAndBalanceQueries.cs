using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.Queries;

// ---------------------------------------------------------------- comparativo --

public sealed record ComparisonRowDto(
    Guid EmployeePublicId,
    string EmployeeName,
    decimal? PreviousNet,
    decimal CurrentNet,
    decimal? VariationPercent,
    bool OverThreshold,
    bool NewEmployee,
    bool LeftEmployee);

public sealed record ComparisonDto(
    Guid RunPublicId,
    Guid? PreviousRunPublicId,
    Guid? PreviousPeriodPublicId,
    string? PreviousPeriodLabel,
    decimal ThresholdPercent,
    IReadOnlyList<ComparisonRowDto> Rows);

/// <summary>FR-018/US4: neto por empleado contra la última corrida aprobada del período anterior del mismo plan; variación y umbral de la cooperativa.</summary>
public sealed record GetRunComparisonQuery(Guid RunPublicId) : IRequest<Result<ComparisonDto>>;

public sealed class GetRunComparisonQueryValidator : AbstractValidator<GetRunComparisonQuery>
{
    public GetRunComparisonQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetRunComparisonQueryHandler(IApplicationDbContext db, PayrollPolicyReader policies)
    : IRequestHandler<GetRunComparisonQuery, Result<ComparisonDto>>
{
    public async Task<Result<ComparisonDto>> Handle(GetRunComparisonQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<ComparisonDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));
        var period = run.PayPeriod!;

        var anterior = await (
            from p in db.PayPeriods.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on p.Id equals r.PayPeriodId
            where p.PayrollPlanId == period.PayrollPlanId && p.EndDate < period.StartDate && r.Status == PayrollRunStatus.Approved
            orderby p.EndDate descending, r.Version descending
            select new { Period = p, Run = r }).FirstOrDefaultAsync(ct);

        var actuales = await Netos(run.Id, ct);
        var previos = anterior is null ? new Dictionary<int, (Guid, string, decimal)>() : await Netos(anterior.Run.Id, ct);

        var umbral = (await policies.ReadAsync(ct)).VariationThresholdPercent;
        var filas = new List<ComparisonRowDto>();
        foreach (var (empId, (publicId, nombre, neto)) in actuales)
        {
            if (previos.TryGetValue(empId, out var prev))
            {
                decimal? variacion = prev.Item3 == 0m ? null : Math.Round((neto - prev.Item3) / Math.Abs(prev.Item3) * 100m, 2);
                filas.Add(new ComparisonRowDto(publicId, nombre, prev.Item3, neto, variacion, variacion is { } v && Math.Abs(v) > umbral, false, false));
            }
            else
            {
                filas.Add(new ComparisonRowDto(publicId, nombre, null, neto, null, anterior is not null, anterior is not null, false));
            }
        }
        foreach (var (empId, (publicId, nombre, neto)) in previos.Where(p => !actuales.ContainsKey(p.Key)))
            filas.Add(new ComparisonRowDto(publicId, nombre, neto, 0m, null, true, false, true));

        return Result.Success(new ComparisonDto(run.PublicId, anterior?.Run.PublicId, anterior?.Period.PublicId,
            anterior is null ? null : $"{anterior.Period.StartDate:dd/MM/yyyy} – {anterior.Period.EndDate:dd/MM/yyyy}",
            umbral, filas.OrderBy(f => f.EmployeeName, StringComparer.CurrentCultureIgnoreCase).ToList()));
    }

    private async Task<Dictionary<int, (Guid, string, decimal)>> Netos(int runId, CancellationToken ct) =>
        await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == runId
            select new { re.EmployeeId, e.PublicId, Nombre = p.FirstName + " " + p.LastName, re.NetPay })
        .ToDictionaryAsync(x => x.EmployeeId, x => (x.PublicId, x.Nombre.Trim(), x.NetPay), ct);
}

// --------------------------------------------------------------------- cuadre --

public sealed record BalanceCheckDto(
    bool EarningsMinusDeductionsEqualsNet,
    bool EmployerAndProvisionsOutsideNet,
    bool? AccountingDocumentBalanced,
    bool LinesMatchEmployeeTotals,
    IReadOnlyList<string> Details);

/// <summary>US4: las verificaciones de cuadre de una corrida, con el detalle de cada una.</summary>
public sealed record GetRunBalanceCheckQuery(Guid RunPublicId) : IRequest<Result<BalanceCheckDto>>;

public sealed class GetRunBalanceCheckQueryValidator : AbstractValidator<GetRunBalanceCheckQuery>
{
    public GetRunBalanceCheckQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetRunBalanceCheckQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRunBalanceCheckQuery, Result<BalanceCheckDto>>
{
    public async Task<Result<BalanceCheckDto>> Handle(GetRunBalanceCheckQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<BalanceCheckDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var empleados = await db.PayrollRunEmployees.AsNoTracking().Where(e => e.PayrollRunId == run.Id).ToListAsync(ct);
        var ids = empleados.Select(e => e.Id).ToList();
        var porNaturaleza = await db.PayrollRunLines.AsNoTracking()
            .Where(l => ids.Contains(l.PayrollRunEmployeeId))
            .GroupBy(l => l.Nature)
            .Select(g => new { Nature = g.Key, Total = g.Sum(l => l.Amount) })
            .ToListAsync(ct);
        decimal Total(ConceptNature n) => porNaturaleza.FirstOrDefault(x => x.Nature == n)?.Total ?? 0m;

        var detalles = new List<string>();

        var devengos = Total(ConceptNature.Earning);
        var deducciones = Total(ConceptNature.Deduction);
        var netoLineas = devengos - deducciones;
        var cuadraNeto = netoLineas == run.TotalNet && empleados.Sum(e => e.NetPay) == run.TotalNet;
        detalles.Add($"Devengos {devengos:N0} − deducciones {deducciones:N0} = {netoLineas:N0}; neto de la corrida {run.TotalNet:N0}; suma de netos por empleado {empleados.Sum(e => e.NetPay):N0}.");

        var aportes = Total(ConceptNature.EmployerContribution);
        var provisiones = Total(ConceptNature.Provision);
        var fueraDelNeto = aportes == run.TotalEmployerContributions && provisiones == run.TotalProvisions
                           && run.TotalNet == run.TotalEarnings - run.TotalDeductions;
        detalles.Add($"Aportes del empleador {aportes:N0} y provisiones {provisiones:N0} no entran al neto.");

        var totalesPorEmpleado = empleados.All(e => e.NetPay == e.TotalEarnings - e.TotalDeductions);
        detalles.Add(totalesPorEmpleado ? "Cada empleado: devengado − deducido = neto." : "Hay empleados cuyo neto no coincide con devengado − deducido.");

        bool? documentoCuadrado = null;
        if (run.AccountingDocumentId is { } docId)
        {
            var doc = await db.AccountingDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId, ct);
            if (doc is not null)
            {
                // El asiento va por concepto y en valor absoluto (una línea negativa —el ajuste por
                // redondeo, un descuento en reverso— gira las cuentas, no resta del débito). Por eso
                // se compara contra la suma de |total por concepto|, no contra la suma con signo:
                // con horas a 220/mes el ajuste por redondeo aparece casi siempre y con signo.
                var porConcepto = await db.PayrollRunLines.AsNoTracking()
                    .Where(l => ids.Contains(l.PayrollRunEmployeeId) && l.AffectsAccounting)
                    .GroupBy(l => l.ConceptCode)
                    .Select(g => g.Sum(l => l.Amount))
                    .ToListAsync(ct);
                var contable = porConcepto.Sum(Math.Abs);
                documentoCuadrado = doc.TotalDebit == doc.TotalCredit && doc.TotalDebit == contable;
                detalles.Add($"Comprobante {doc.VoucherTypeCode}-{doc.DocumentNumber}: débitos {doc.TotalDebit:N0}, créditos {doc.TotalCredit:N0}; líneas con asiento {contable:N0}.");
            }
        }
        else
        {
            detalles.Add("Sin comprobante contable todavía: se genera al aprobar.");
        }

        return Result.Success(new BalanceCheckDto(cuadraNeto, fueraDelNeto, documentoCuadrado, totalesPorEmpleado, detalles));
    }
}
