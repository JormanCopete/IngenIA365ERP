using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using IngenIA365ERP.Domain.Payroll.Settlements.Rules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Vacations;

/// <summary>El saldo de vacaciones de un empleado a una fecha, con cada pieza y su explicación (contracts/api.md §5).</summary>
public sealed record VacationBalanceResult(
    Employee Employee,
    DateOnly AsOf,
    decimal AccruedDays,
    decimal OpeningDays,
    decimal EnjoyedDays,
    decimal CompensatedDays,
    decimal AdjustedDays,
    decimal SettlementPaidDays,
    decimal PendingDays,
    int WorkedDays,
    int SuspensionDays,
    DateOnly? LastEnjoymentTo,
    IReadOnlyList<ExplanationStep> Explanation,
    IReadOnlyList<VacationMovement> Movements)
{
    /// <summary>Lo causado más el saldo inicial: la base del máximo compensable (CST art. 189).</summary>
    public decimal TotalAccrued => AccruedDays + OpeningDays;
}

/// <summary>
/// El saldo <b>derivado</b> de vacaciones (feature 010, R6, FR-014): causado + saldo inicial −
/// disfrutado − compensado − pagado al retiro ± ajustes. Nunca se guarda. La aritmética es la
/// misma del motor de liquidaciones (<see cref="VacationRule.Balance"/>): días trabajados por
/// el calendario comercial desde el ingreso —o desde el saldo inicial— hasta la fecha, menos las
/// suspensiones del contrato (CST art. 53), por el parámetro <c>VACACIONES_DIAS_ANIO</c> vigente a
/// la fecha, sobre 360. Este servicio sólo carga los datos y arma la entrada; si el parámetro no
/// tiene vigencia responde <c>Payroll.Settlement.ParametersMissing</c>, nunca un número inventado.
///
/// <para>
/// Cuenta <b>todos</b> los movimientos vivos del empleado, también los registrados para fechas
/// posteriores: un disfrute programado ya compromete el saldo. El cargador de las liquidaciones
/// (<see cref="SettlementInputLoader"/>) trae los mismos movimientos y las suspensiones desde el
/// ingreso, así que la pantalla y lo que pagan la corrida de vacaciones y la definitiva coinciden.
/// </para>
/// </summary>
public sealed class VacationBalanceCalculator(IApplicationDbContext db)
{
    public async Task<Result<VacationBalanceResult>> CalcularAsync(Employee employee, DateOnly asOf, CancellationToken ct)
    {
        var varios = await CalcularVariosAsync([employee], asOf, ct);
        return varios.IsFailure ? Result.Failure<VacationBalanceResult>(varios.Error) : Result.Success(varios.Value[employee.Id]);
    }

    /// <summary>El saldo de varios empleados a la misma fecha en pocas consultas; la clave es <c>Employee.Id</c>.</summary>
    public async Task<Result<IReadOnlyDictionary<int, VacationBalanceResult>>> CalcularVariosAsync(
        IReadOnlyList<Employee> employees, DateOnly asOf, CancellationToken ct)
    {
        var asOfDt = asOf.ToDateTime(TimeOnly.MinValue);
        var ids = employees.Select(e => e.Id).ToList();

        var parametros = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => p.ValidFrom <= asOfDt && (p.ValidTo == null || p.ValidTo >= asOfDt))
            .ToListAsync(ct);
        var set = new ParameterSet(parametros, asOfDt);
        if (!set.Has(SettlementParameterCodes.VacationDaysPerYear))
            return Result.Failure<IReadOnlyDictionary<int, VacationBalanceResult>>(
                SettlementErrors.ParametersMissing([SettlementParameterCodes.VacationDaysPerYear], asOfDt));

        var saldos = await db.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Where(b => ids.Contains(b.EmployeeId) && b.AsOfDate <= asOf)
            .OrderBy(b => b.AsOfDate).ThenBy(b => b.Kind).ToListAsync(ct);
        var saldosPorEmpleado = saldos.ToLookup(b => b.EmployeeId);

        var movimientos = await db.VacationMovements.AsNoTracking()
            .Where(m => ids.Contains(m.EmployeeId) && m.Status != VacationMovementStatus.Cancelled)
            .OrderBy(m => m.StartDate).ThenBy(m => m.Id).ToListAsync(ct);
        var movimientosPorEmpleado = movimientos.ToLookup(m => m.EmployeeId);

        var suspensiones = await SuspensionesAsync(ids, asOfDt, ct);
        var suspensionesPorEmpleado = suspensiones.ToLookup(s => s.EmployeeId);

        var resultado = new Dictionary<int, VacationBalanceResult>(employees.Count);
        foreach (var e in employees)
        {
            var propios = movimientosPorEmpleado[e.Id].ToList();
            var input = new SettlementInput
            {
                Kind = SettlementKind.Vacation,
                CutoffDate = asOfDt,
                Employee = new SettlementEmployeeInput
                {
                    PublicId = e.PublicId,
                    DisplayName = e.Person is null ? string.Empty : $"{e.Person.FirstName} {e.Person.LastName}".Trim(),
                    Class = e.EmployeeClass,
                    JoinDate = e.JoinDate.Date,
                    TerminationDate = e.Status < 0 && e.TerminationDate < DateTime.MaxValue.Date ? e.TerminationDate.Date : null,
                    SalaryHistory = [new SalaryChangeInput(e.JoinDate.Date, e.Salary)],
                    ApprenticeStage = e.ApprenticeStage,
                },
                Absences = suspensionesPorEmpleado[e.Id].Select(s => new AbsenceInput(s.From, s.To, IsSuspension: true, s.Code)).ToList(),
                OpeningBalance = SettlementInputLoader.SaldoInicial(saldosPorEmpleado[e.Id].ToList()),
                VacationMovements = propios.Select(Movimiento).ToList(),
                Parameters = parametros,
                Concepts = [],
            };
            var ctx = new SettlementContext { Input = input, Parameters = set, Concepts = new ConceptSet([], asOfDt) };
            var exp = new Explanation { Form = "Saldo de vacaciones" };
            var balance = VacationRule.Balance(ctx, exp);

            var fin = ctx.EffectiveEnd;
            var inicio = input.OpeningBalance is { } ob && ob.AsOfDate.Date >= ctx.EmploymentStart && ob.AsOfDate.Date <= fin
                ? ob.AsOfDate.Date.AddDays(1)
                : ctx.EmploymentStart;
            var vinculados = fin >= inicio ? CalendarConventions.Days(inicio, fin) : 0;
            var suspension = fin >= inicio ? ctx.SuspensionDays(inicio, fin) : 0;

            decimal Suma(VacationMovementKind kind) => propios.Where(m => m.Kind == kind).Sum(m => m.BusinessDays);
            resultado[e.Id] = new VacationBalanceResult(
                e, asOf,
                AccruedDays: balance.Accrued,
                OpeningDays: balance.OpeningBalanceDays,
                EnjoyedDays: Suma(VacationMovementKind.Enjoyment),
                CompensatedDays: Suma(VacationMovementKind.Compensation),
                AdjustedDays: Suma(VacationMovementKind.Adjustment),
                SettlementPaidDays: Suma(VacationMovementKind.SettlementPayout),
                PendingDays: balance.Pending,
                WorkedDays: Math.Max(0, vinculados - suspension),
                SuspensionDays: suspension,
                LastEnjoymentTo: propios.Where(m => m.Kind == VacationMovementKind.Enjoyment).Select(m => m.EndDate).Max(),
                Explanation: exp.Steps.ToList(),
                Movements: propios);
        }

        return Result.Success<IReadOnlyDictionary<int, VacationBalanceResult>>(resultado);
    }

    /// <summary>El máximo compensable en dinero: el porcentaje del parámetro sobre lo causado más el saldo inicial (CST art. 189 num. 1).</summary>
    public async Task<Result<(decimal MaxDays, decimal Fraction)>> MaximoCompensableAsync(VacationBalanceResult balance, CancellationToken ct)
    {
        var asOfDt = balance.AsOf.ToDateTime(TimeOnly.MinValue);
        var parametros = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => p.Code == SettlementParameterCodes.VacationCompensablePct && p.ValidFrom <= asOfDt && (p.ValidTo == null || p.ValidTo >= asOfDt))
            .ToListAsync(ct);
        var set = new ParameterSet(parametros, asOfDt);
        if (!set.Has(SettlementParameterCodes.VacationCompensablePct))
            return Result.Failure<(decimal, decimal)>(SettlementErrors.ParametersMissing([SettlementParameterCodes.VacationCompensablePct], asOfDt));
        var fraccion = set.Fraction(SettlementParameterCodes.VacationCompensablePct);
        return Result.Success((balance.TotalAccrued * fraccion, fraccion));
    }

    private sealed record Suspension(int EmployeeId, DateTime From, DateTime To, string Code);

    /// <summary>
    /// Las suspensiones del contrato con fechas (novedades activas informativas que reducen días,
    /// salvo la ausencia por vacaciones ya pagadas) desde el ingreso hasta la fecha: son las que
    /// descuentan vacaciones (CST art. 53). Las incapacidades y licencias remuneradas no.
    /// </summary>
    private async Task<List<Suspension>> SuspensionesAsync(List<int> ids, DateTime asOf, CancellationToken ct)
    {
        var conceptos = await db.PayrollConceptDefinitions.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.ReducesWorkedDays && c.Nature == ConceptNature.Informative && c.Code != WellKnownConceptCodes.VacationLeave)
            .Select(c => new { c.Id, c.Code }).ToListAsync(ct);
        if (conceptos.Count == 0) return [];
        var idsConcepto = conceptos.Select(c => c.Id).ToList();
        var codigoPorId = conceptos.ToDictionary(c => c.Id, c => c.Code);
        var novedades = await db.PayrollNovelties.AsNoTracking()
            .Where(n => ids.Contains(n.EmployeeId) && n.Status == NoveltyStatus.Active
                        && idsConcepto.Contains(n.ConceptDefinitionId) && n.StartDate != null && n.EndDate != null && n.StartDate <= asOf)
            .Select(n => new { n.EmployeeId, n.StartDate, n.EndDate, n.ConceptDefinitionId })
            .ToListAsync(ct);
        return novedades.Select(n => new Suspension(n.EmployeeId, n.StartDate!.Value.Date, n.EndDate!.Value.Date, codigoPorId[n.ConceptDefinitionId])).ToList();
    }

    private static VacationMovementInput Movimiento(VacationMovement m) => new()
    {
        PublicId = m.PublicId,
        Kind = m.Kind,
        StartDate = m.StartDate.ToDateTime(TimeOnly.MinValue),
        EndDate = m.EndDate?.ToDateTime(TimeOnly.MinValue),
        BusinessDays = m.BusinessDays,
        CalendarDays = m.CalendarDays,
        IsCancelled = m.Status == VacationMovementStatus.Cancelled,
        Description = m.Notes,
    };
}
