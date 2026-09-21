using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// La provisión acumulada por empleado y concepto de provisión (research «Contabilización de
/// las liquidaciones», FR-004). El libro no la tiene por empleado —las provisiones se
/// contabilizan sin tercero—, así que se deriva de las corridas: lo que la nómina ordinaria
/// provisionó (<c>PROV_*</c> en corridas aprobadas no reversadas, por año/mes de imputación
/// hasta el corte), más lo que las liquidaciones aprobadas ajustaron (<c>*_AJUSTE_PROV</c>, con
/// signo), menos lo que esas liquidaciones consumieron (el rubro pagado: <c>PRIMA</c>,
/// <c>CESANTIAS</c>, <c>INT_CESANTIAS</c>, <c>VACACIONES_LIQ</c>/<c>_COMP</c>), más el saldo
/// inicial digitado (R3: la fila vigente, nunca la suma de apertura y ajustes). Tras cada
/// liquidación aprobada el saldo de su rubro vuelve a cero, que es lo que el ajuste garantiza.
/// Nada se guarda: es una suma (Principio XI).
/// </summary>
public sealed class ProvisionBalanceReader(IApplicationDbContext db)
{
    public sealed record Saldo(string ProvisionCode, decimal Accrued, decimal Adjusted, decimal Consumed, decimal OpeningBalance)
    {
        /// <summary>Lo que la próxima liquidación cancela.</summary>
        public decimal Balance => Accrued + Adjusted + OpeningBalance - Consumed;
    }

    /// <summary>Saldos por concepto de provisión para varios empleados, con corte (imputación ≤ mes del corte; liquidaciones con corte ≤ corte).</summary>
    /// <summary>Las provisiones que alguna liquidación cancela (<see cref="WellKnownConceptCodes.ProvisionPairFor"/>); se informan siempre, aunque valgan cero.</summary>
    private static readonly string[] ProvisionesConRubroPar =
    [
        WellKnownConceptCodes.ServiceBonusProvision,
        WellKnownConceptCodes.SeveranceProvision,
        WellKnownConceptCodes.SeveranceInterestProvision,
        WellKnownConceptCodes.VacationProvision,
    ];

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<Saldo>>> LeerAsync(IReadOnlyCollection<int> employeeIds, DateOnly cutoff, int? excludeRunId, CancellationToken ct)
    {
        if (employeeIds.Count == 0) return new Dictionary<int, IReadOnlyList<Saldo>>();

        // --- provisiones de la nómina ordinaria, por imputación ---
        var provisiones = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
            where employeeIds.Contains(re.EmployeeId)
                  && r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved
                  && l.Nature == ConceptNature.Provision
                  && (p.ImputationYear < cutoff.Year || (p.ImputationYear == cutoff.Year && p.ImputationMonth <= cutoff.Month))
            group l by new { re.EmployeeId, l.ConceptCode } into g
            select new { g.Key.EmployeeId, g.Key.ConceptCode, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        // --- liquidaciones especiales aprobadas: lo consumido y lo ajustado ---
        var especiales = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            where employeeIds.Contains(re.EmployeeId)
                  && r.Kind != PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved
                  && r.CutoffDate <= cutoff
                  && (excludeRunId == null || r.Id != excludeRunId)
                  && (l.Nature == ConceptNature.Provision || l.Nature == ConceptNature.Earning)
            group l by new { re.EmployeeId, l.ConceptCode, l.Nature } into g
            select new { g.Key.EmployeeId, g.Key.ConceptCode, g.Key.Nature, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        // --- saldo inicial (R3): la fila vigente con corte ≤ cutoff (la apertura, o el ajuste que la reemplaza) ---
        var saldos = await db.EmployeeBenefitOpeningBalances.AsNoTracking()
            .Where(b => employeeIds.Contains(b.EmployeeId) && b.AsOfDate <= cutoff)
            .ToListAsync(ct);
        var salarios = await db.SalaryChanges.AsNoTracking()
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .OrderBy(s => s.EffectiveDate)
            .ToListAsync(ct);
        var fichas = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).Select(e => new { e.Id, e.Salary }).ToDictionaryAsync(e => e.Id, e => e.Salary, ct);

        var resultado = new Dictionary<int, IReadOnlyList<Saldo>>();
        foreach (var empleado in employeeIds.Distinct())
        {
            var acumulado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var ajustado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var consumido = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var inicial = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in provisiones.Where(x => x.EmployeeId == empleado))
                acumulado[p.ConceptCode] = acumulado.GetValueOrDefault(p.ConceptCode) + p.Total;

            foreach (var e in especiales.Where(x => x.EmployeeId == empleado))
            {
                if (e.Nature == ConceptNature.Earning)
                {
                    if (WellKnownConceptCodes.ProvisionPairFor(e.ConceptCode) is { } par)
                        consumido[par.Provision] = consumido.GetValueOrDefault(par.Provision) + e.Total;
                }
                else if (ProvisionDelAjuste(e.ConceptCode) is { } provision)
                {
                    ajustado[provision] = ajustado.GetValueOrDefault(provision) + e.Total;
                }
            }

            // Una sola fila: la vigente (BenefitBalanceRules.Vigente). El ajuste es el saldo completo
            // corregido y reemplaza a la apertura; sumarlos doblaba la provisión inicial (2026-09-21).
            if (BenefitBalanceRules.Vigente(saldos.Where(b => b.EmployeeId == empleado)) is { } b)
            {
                inicial[WellKnownConceptCodes.ServiceBonusProvision] = b.AccruedServiceBonus;
                inicial[WellKnownConceptCodes.SeveranceProvision] = b.AccruedSeverance;
                inicial[WellKnownConceptCodes.SeveranceInterestProvision] = b.AccruedSeveranceInterest;
                // El saldo de vacaciones viene en días hábiles; en pesos vale el salario vigente a la
                // fecha del saldo por día comercial. Es el mismo valor con que la apertura contable pudo
                // provisionarlas; la diferencia, si la hay, la absorbe el ajuste al liquidar.
                var salario = salarios.Where(s => s.EmployeeId == empleado && s.EffectiveDate.Date <= b.AsOfDate.ToDateTime(TimeOnly.MinValue))
                    .Select(s => (decimal?)s.NewSalary).LastOrDefault() ?? fichas.GetValueOrDefault(empleado);
                inicial[WellKnownConceptCodes.VacationProvision] = b.PendingVacationDays * salario / CalendarConventions.DaysPerMonth;
            }

            // Las cuatro provisiones con rubro par van SIEMPRE, con cero si no hay historia: el
            // saldo se consultó y es cero de verdad. Si faltara el registro, el motor omitiría el
            // ajuste («sin provisión informada») y el comprobante debitaría la provisión por todo lo
            // liquidado: un empleado sin provisión acumulada dejaría la cuenta en negativo. Lo
            // atrapó la e2e de la prima el 2026-09-21 con empleados recién creados por otra prueba.
            var codigos = ProvisionesConRubroPar
                .Concat(acumulado.Keys).Concat(ajustado.Keys).Concat(consumido.Keys).Concat(inicial.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.Ordinal).ToList();
            resultado[empleado] = codigos
                .Select(c => new Saldo(c, acumulado.GetValueOrDefault(c), ajustado.GetValueOrDefault(c), consumido.GetValueOrDefault(c), inicial.GetValueOrDefault(c)))
                .Where(s => ProvisionesConRubroPar.Contains(s.ProvisionCode, StringComparer.OrdinalIgnoreCase)
                            || s.Accrued != 0m || s.Adjusted != 0m || s.Consumed != 0m || s.OpeningBalance != 0m)
                .ToList();
        }
        return resultado;
    }

    /// <summary>El concepto de provisión que corrige un concepto de ajuste (<c>PRIMA_AJUSTE_PROV</c> → <c>PROV_PRIMA</c>); nulo si el código no es un ajuste.</summary>
    public static string? ProvisionDelAjuste(string adjustmentCode)
    {
        foreach (var rubro in new[] { WellKnownConceptCodes.ServiceBonus, WellKnownConceptCodes.Severance, WellKnownConceptCodes.SeveranceInterest, WellKnownConceptCodes.VacationPayout })
        {
            var par = WellKnownConceptCodes.ProvisionPairFor(rubro)!.Value;
            if (par.Adjustment.Equals(adjustmentCode, StringComparison.OrdinalIgnoreCase)) return par.Provision;
        }
        return null;
    }
}
