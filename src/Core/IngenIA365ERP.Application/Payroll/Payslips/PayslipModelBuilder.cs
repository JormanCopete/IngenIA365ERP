using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Payslips;

/// <summary>Un comprobante listo para pintar o enviar, con lo que hace falta para dirigirlo.</summary>
public sealed record PayslipBundle(int RunEmployeeId, Guid EmployeePublicId, string? Email, PayslipModel Model);

/// <summary>
/// Arma el comprobante de pago (FR-024) desde la corrida: líneas de devengo y deducción con
/// su resumen, tramos, totales, banco y cuenta del empleado (<c>PAY_Employees</c>), nombre y
/// documento de la persona (<c>COR_People</c>) y el estado de pago. Sólo corridas aprobadas
/// o reversadas: un borrador no es un comprobante.
/// </summary>
public sealed class PayslipModelBuilder(IApplicationDbContext db, ICurrentTenantService tenant, IDateTimeService clock)
{
    public async Task<Result<IReadOnlyList<PayslipBundle>>> BuildAsync(Guid runPublicId, IReadOnlyList<Guid>? employeePublicIds, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod).ThenInclude(p => p!.PayrollPlan)
            .FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<IReadOnlyList<PayslipBundle>>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));
        if (run.Status is not (PayrollRunStatus.Approved or PayrollRunStatus.Reversed))
            return Result.Failure<IReadOnlyList<PayslipBundle>>(new Error("Payroll.RunNotApproved", "El comprobante de pago se emite sobre una liquidación aprobada, no sobre un borrador."));

        // Feature 010 (R2, FR-011a): una liquidación especial no tiene período; el comprobante lleva la
        // etiqueta del tipo («Prima de servicios 2026-II», «Intereses a las cesantías 2026», «Vacaciones»,
        // «Liquidación definitiva») y, como fechas, el inicio del período liquidado y el corte.
        var period = run.PayPeriod;
        var empleados = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id && (employeePublicIds == null || employeePublicIds.Contains(e.PublicId))
            orderby p.LastName, p.FirstName
            select new { re, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId, p.Email, e.PayrollBankId, e.PayrollBankAccountType, e.PayrollBankAccountNumber, e.PositionId })
            .ToListAsync(ct);
        if (employeePublicIds is not null && empleados.Count != employeePublicIds.Distinct().Count())
            return Result.Failure<IReadOnlyList<PayslipBundle>>(new Error("Payroll.RunEmployeeNotFound", "Alguno de los empleados indicados no está en esta corrida."));

        var ids = empleados.Select(x => x.re.Id).ToList();
        var lineas = (await db.PayrollRunLines.AsNoTracking().Where(l => ids.Contains(l.PayrollRunEmployeeId)).OrderBy(l => l.Order).ToListAsync(ct))
            .ToLookup(l => l.PayrollRunEmployeeId);
        var pagos = await db.PayrollPayments.AsNoTracking().Where(p => ids.Contains(p.PayrollRunEmployeeId) && !p.IsReverted).ToDictionaryAsync(p => p.PayrollRunEmployeeId, ct);
        var codigosBanco = empleados.Select(x => x.PayrollBankId).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
        var bancos = codigosBanco.Count == 0 ? new Dictionary<string, string>()
            : await db.Banks.AsNoTracking().Where(b => b.LegacyCode != null && codigosBanco.Contains(b.LegacyCode)).ToDictionaryAsync(b => b.LegacyCode!, b => b.Name, ct);
        var posiciones = await db.Positions.AsNoTracking().Where(p => empleados.Select(x => x.PositionId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var cooperativa = tenant.TenantName ?? "Cooperativa";
        var etiqueta = run.EsEspecial
            ? Settlements.Common.SettlementLabels.EtiquetaDelComprobante(run)
            : string.IsNullOrWhiteSpace(period!.Description) ? $"{period.StartDate:dd/MM/yyyy} – {period.EndDate:dd/MM/yyyy}" : period.Description!;
        var (inicio, fin) = run.EsEspecial ? VentanaDe(run) : (period!.StartDate, period.EndDate);
        var plan = period?.PayrollPlan?.Name ?? (run.EsEspecial ? Settlements.Common.SettlementErrors.Nombre(run.Kind) : string.Empty);
        var ahora = clock.UtcNow;

        var bundles = new List<PayslipBundle>(empleados.Count);
        foreach (var x in empleados)
        {
            var propias = lineas[x.re.Id].ToList();
            var tramos = JsonSerializer.Deserialize<List<SalaryTrancheDto>>(x.re.SalaryTranchesJson, RunJson.Options) ?? [];
            pagos.TryGetValue(x.re.Id, out var pago);

            PayslipLineModel Linea(Domain.Entities.Payroll.Transactions.PayrollRunLine l) =>
                new(l.ConceptCode, l.ConceptName, l.Nature, l.Quantity, l.Amount, Resumen(l.ExplanationJson));

            var model = new PayslipModel(
                CooperativeName: cooperativa,
                CooperativeTaxId: null,
                EmployeeName: NombreDePersona.Completo(x.FirstName, x.OtherNames, x.LastName, x.SecondLastName),
                EmployeeDocument: x.TaxId,
                EmployeePosition: posiciones.GetValueOrDefault(x.PositionId),
                PlanName: plan,
                PeriodLabel: etiqueta,
                PeriodStart: inicio,
                PeriodEnd: fin,
                RunVersion: run.Version,
                ApprovedAt: run.ApprovedAt,
                DaysWorked: x.re.DaysWorked,
                MonthlySalary: tramos.Count > 0 ? tramos[^1].MonthlySalary : 0m,
                Earnings: propias.Where(l => l.Nature is ConceptNature.Earning or ConceptNature.Informative).Select(Linea).ToList(),
                Deductions: propias.Where(l => l.Nature == ConceptNature.Deduction).Select(Linea).ToList(),
                TotalEarnings: x.re.TotalEarnings,
                TotalDeductions: x.re.TotalDeductions,
                NetPay: x.re.NetPay,
                BankName: x.PayrollBankId is { } bk && bancos.TryGetValue(bk, out var nb) ? nb : x.PayrollBankId,
                BankAccount: x.PayrollBankAccountNumber,
                PaymentStatus: pago is null ? "Pendiente de pago" : $"Pagado el {pago.PaidAt:dd/MM/yyyy} ({pago.PaymentMethod}{(pago.Reference is null ? string.Empty : ", ref. " + pago.Reference)})",
                GeneratedAt: ahora);

            bundles.Add(new PayslipBundle(x.re.Id, x.PublicId, string.IsNullOrWhiteSpace(x.Email) ? null : x.Email.Trim(), model));
        }

        return Result.Success<IReadOnlyList<PayslipBundle>>(bundles);
    }

    /// <summary>El tramo que la liquidación cubre: el semestre, el año, o el corte mismo en vacaciones y definitiva.</summary>
    private static (DateTime Start, DateTime End) VentanaDe(Domain.Entities.Payroll.Transactions.PayrollRun run)
    {
        var corte = (run.CutoffDate ?? DateOnly.MinValue).ToDateTime(TimeOnly.MinValue);
        return run.Kind switch
        {
            PayrollRunKind.ServiceBonus => (new DateTime(corte.Year, run.Semester == 1 ? 1 : 7, 1), corte),
            PayrollRunKind.Severance => (new DateTime(corte.Year, 1, 1), corte),
            _ => (corte, corte),
        };
    }

    private static string Resumen(string explanationJson)
    {
        try { return JsonSerializer.Deserialize<Explanation>(explanationJson, RunJson.Options)?.Summary ?? string.Empty; }
        catch (JsonException) { return string.Empty; }
    }
}
