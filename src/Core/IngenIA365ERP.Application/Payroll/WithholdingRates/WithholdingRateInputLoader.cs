using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Withholding;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingRates;

/// <summary>Lo que el loader entrega además del insumo puro: el semestre que rige, el mes del cálculo y si el último mes de la ventana ya tiene nómina aprobada.</summary>
public sealed record WithholdingRateLoad(FixedRateInput Input, DateOnly ValidFrom, DateOnly ValidTo, DateTime CalculationMonth, bool LastMonthApproved, string? FirstApprovedMonth, int TableParameterId);

/// <summary>
/// Carga los doce meses anteriores al mes del cálculo (junio para el semestre que rige
/// julio–diciembre, diciembre para enero–junio del año siguiente; ET art. 386; feature 010,
/// US7; research R8): devengos con <c>AffectsWithholdingBase</c> de corridas aprobadas —ordinarias
/// por su mes de imputación, especiales por el mes del corte; la prima entra, las cesantías y
/// sus intereses no—, los aportes obligatorios reales, las deducciones declaradas vigentes al
/// mes del cálculo, los parámetros de ese mes y la tabla del plan si el plan trae tramos.
/// </summary>
public sealed class WithholdingRateInputLoader(IApplicationDbContext db, PayrollPolicyReader policies)
{
    public static (DateTime CalculationMonth, DateOnly ValidFrom, DateOnly ValidTo) Semestre(short year, byte semester) =>
        semester == 1
            ? (new DateTime(year, 6, 1), new DateOnly(year, 7, 1), new DateOnly(year, 12, 31))
            : (new DateTime(year, 12, 1), new DateOnly(year + 1, 1, 1), new DateOnly(year + 1, 6, 30));

    public async Task<Result<WithholdingRateLoad>> LoadAsync(Employee e, short year, byte semester, CancellationToken ct)
    {
        var (mesCalculo, desde, hasta) = Semestre(year, semester);
        var ventanaInicio = mesCalculo.AddMonths(-12);
        var ventanaFin = mesCalculo.AddDays(-1);
        var primerDiaVentana = DateOnly.FromDateTime(ventanaInicio);
        var ultimoDiaVentana = DateOnly.FromDateTime(ventanaFin);
        // Comparaciones por año y mes con los tipos de la tabla (short/byte): una expresión como año × 100 + mes se
        // traduciría como int2 y se desborda en PostgreSQL.
        short y0 = (short)ventanaInicio.Year, y1 = (short)ventanaFin.Year;
        byte m0 = (byte)ventanaInicio.Month, m1 = (byte)ventanaFin.Month;

        // --- corridas aprobadas del empleado en la ventana ---
        var ordinarias = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
            where re.EmployeeId == e.Id && r.Kind == PayrollRunKind.Ordinary && r.Status == PayrollRunStatus.Approved
                  && (p.ImputationYear > y0 || (p.ImputationYear == y0 && p.ImputationMonth >= m0))
                  && (p.ImputationYear < y1 || (p.ImputationYear == y1 && p.ImputationMonth <= m1))
            select new { RunEmployeeId = re.Id, r.PublicId, r.Version, r.Kind, Year = (int)p.ImputationYear, Month = (int)p.ImputationMonth }).ToListAsync(ct);
        var especiales = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            where re.EmployeeId == e.Id && r.Status == PayrollRunStatus.Approved && r.Kind != PayrollRunKind.Ordinary && r.Kind != PayrollRunKind.Severance
                  && r.CutoffDate != null && r.CutoffDate >= primerDiaVentana && r.CutoffDate <= ultimoDiaVentana
            select new { RunEmployeeId = re.Id, r.PublicId, r.Version, r.Kind, Year = r.CutoffDate!.Value.Year, Month = r.CutoffDate!.Value.Month }).ToListAsync(ct);
        var corridas = ordinarias.Concat(especiales).ToList();

        string? primerMesAprobado = null;
        if (corridas.Count == 0)
        {
            var primera = await (
                from re in db.PayrollRunEmployees.AsNoTracking()
                join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
                join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
                where re.EmployeeId == e.Id && r.Status == PayrollRunStatus.Approved
                orderby p.ImputationYear, p.ImputationMonth
                select new { p.ImputationYear, p.ImputationMonth }).FirstOrDefaultAsync(ct);
            primerMesAprobado = primera is null ? null : $"{primera.ImputationYear}-{primera.ImputationMonth:00}";
            return Result.Failure<WithholdingRateLoad>(WithholdingRateErrors.NoHistory(e.PublicId, primerMesAprobado));
        }

        var reIds = corridas.Select(c => c.RunEmployeeId).ToList();
        var lineas = await (
            from l in db.PayrollRunLines.AsNoTracking()
            join c in db.PayrollConceptDefinitions.AsNoTracking() on l.ConceptDefinitionId equals c.Id
            where reIds.Contains(l.PayrollRunEmployeeId)
            select new Linea(l.PayrollRunEmployeeId, l.ConceptCode, l.Nature, l.Amount, c.AffectsWithholdingBase)).ToListAsync(ct);
        var porCorrida = lineas.ToLookup(l => l.RunEmployeeId);
        var aportes = new HashSet<string>([WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.SolidarityFund], StringComparer.OrdinalIgnoreCase);
        var excluidos = new HashSet<string>([WellKnownConceptCodes.Severance, WellKnownConceptCodes.SeveranceInterest], StringComparer.OrdinalIgnoreCase);

        var meses = new List<FixedRateMonth>();
        foreach (var g in corridas.GroupBy(c => (c.Year, c.Month)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
        {
            decimal bruto = 0m, contribuciones = 0m;
            var fuentes = new List<FixedRateSourceRun>();
            var especial = false;
            foreach (var c in g)
            {
                var propias = porCorrida[c.RunEmployeeId].ToList();
                // Art. 386: las cesantías y sus intereses no entran a la sumatoria; la prima sí.
                var gravable = propias.Where(l => l.Nature == ConceptNature.Earning && l.AffectsWithholdingBase && !excluidos.Contains(l.ConceptCode)).Sum(l => l.Amount);
                var ap = propias.Where(l => aportes.Contains(l.ConceptCode)).Sum(l => l.Amount);
                bruto += gravable; contribuciones += ap;
                if (c.Kind != PayrollRunKind.Ordinary) especial = true;
                fuentes.Add(new FixedRateSourceRun(c.PublicId, c.Version, c.Kind.ToString(), gravable));
            }
            meses.Add(new FixedRateMonth((short)g.Key.Year, (byte)g.Key.Month, bruto, contribuciones, especial, fuentes));
        }

        // --- parámetros y tabla del mes del cálculo ---
        var parametros = await db.PayrollLegalParameters.AsNoTracking().Include(p => p.Ranges)
            .Where(p => p.ValidFrom <= mesCalculo && (p.ValidTo == null || p.ValidTo >= mesCalculo)).ToListAsync(ct);
        var plan = await db.PayrollPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == e.PayrollPlanId, ct);
        var tramosDelPlan = plan is null ? [] : await db.WithholdingParameters.AsNoTracking().Where(t => t.PayrollPlanId == plan.Id && !t.IsDeleted).ToListAsync(ct);
        var conTabla = plan is null ? parametros : TablaDeRetencionDelPlan.Aplicar(parametros, tramosDelPlan, plan, mesCalculo);
        var set = new ParameterSet(conTabla, mesCalculo);
        var faltantes = set.Missing(WithholdingRateParameterCodes.Required);
        if (faltantes.Count > 0) return Result.Failure<WithholdingRateLoad>(WithholdingRateErrors.ParametersMissing(faltantes));
        var tabla = set.Table(WithholdingRateParameterCodes.WithholdingTableUvt);
        var tablaDelPlan = tramosDelPlan.Count > 0;

        var deducciones = await db.EmployeeTaxDeductions.AsNoTracking()
            .Where(d => d.EmployeeId == e.Id && d.ValidFrom <= mesCalculo && (d.ValidTo == null || d.ValidTo >= mesCalculo))
            .Select(d => new TaxDeductionInput(d.Kind, d.MonthlyAmount, d.Percent)).ToListAsync(ct);

        var politicas = await policies.ReadAsync(DateOnly.FromDateTime(mesCalculo), ct);
        var secuencia = politicas.P2SecuenciaDepuracion == SecuenciaDepuracionP2.DividirLuegoDepurar ? DepurationSequence.DivideThenDepurate : DepurationSequence.DepurateThenDivide;

        var ultimoMes = (ventanaFin.Year, ventanaFin.Month);
        var ultimoAprobado = ordinarias.Any(c => c.Year == ultimoMes.Year && c.Month == ultimoMes.Month);

        var input = new FixedRateInput(e.PublicId, year, semester, meses, deducciones, set, tabla, tablaDelPlan, secuencia, politicas.RetefteTopesAnualesModo);
        return Result.Success(new WithholdingRateLoad(input, desde, hasta, mesCalculo, ultimoAprobado, primerMesAprobado, tabla.Id));
    }

    private sealed record Linea(int RunEmployeeId, string ConceptCode, ConceptNature Nature, decimal Amount, bool AffectsWithholdingBase);
}
