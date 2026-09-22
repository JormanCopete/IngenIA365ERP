using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;
using RuleBases = IngenIA365ERP.Domain.Payroll.Calculation.Rules.Bases;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Bases;

/// <summary>
/// Construye las bases del período a partir de las líneas de devengo ya calculadas
/// (data-model §3): salarial, de aportes (con el porcentaje del salario integral y el
/// tope en SMMLV, ambos parámetros), prestacional y la del auxilio de transporte. La
/// base de retención tiene su propio constructor porque se depura.
/// </summary>
public static class BaseBuilder
{
    public static IReadOnlyList<ExplanationStep> Build(RuleContext ctx)
    {
        var pasos = new List<ExplanationStep>();
        var devengos = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).ToList();

        decimal Suma(Func<Entities.Payroll.PayrollConceptDefinition, bool> criterio) =>
            devengos.Where(l => ctx.Concepts.Find(l.Code) is { } c && criterio(c)).Sum(l => l.Amount);

        var basico = devengos
            .Where(l => l.Code.Equals(WellKnownConceptCodes.BasicSalary, StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.Amount);
        ctx.Bases.BasicSalary = basico;
        pasos.Add(new ExplanationStep(RuleBases.Label(CalculationBase.BasicSalary), basico, null));

        var salarial = Suma(c => c.AffectsSalaryBase);
        ctx.Bases.SalaryEarnings = salarial;
        pasos.Add(new ExplanationStep(RuleBases.Label(CalculationBase.SalaryEarnings), salarial, null));

        // --- Base de aportes (IBC) ---
        var ibc = Suma(c => c.AffectsContributionBase);
        pasos.Add(new ExplanationStep("Devengos que forman el IBC", ibc, null));
        // Feature 010 (D-01, R6): los días de AUSENCIA_VACACIONES no son salario aquí —los pagó la
        // liquidación de vacaciones—, pero los aportes se causan completos sobre el salario vigente
        // al iniciar el descanso (Decreto 780/2016 art. 3.2.5.1): el IBC los recupera.
        ibc += VacationLeaveContribution(ctx, pasos);
        if (ctx.Employee.Class == EmployeeClass.IntegralSalary)
        {
            var pct = ctx.Parameters.Fraction(LegalParameterCodes.IntegralSalaryBasePct);
            var p = ctx.Parameters.Describe(LegalParameterCodes.IntegralSalaryBasePct);
            ibc *= pct;
            pasos.Add(new ExplanationStep($"Salario integral: IBC al {Fmt.Pct(pct)} ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})", ibc, null));
        }
        var smmlv = ctx.Parameters.Value(LegalParameterCodes.Smmlv);
        var topeSmmlv = ctx.Parameters.Value(LegalParameterCodes.ContributionBaseCapSmmlv);
        var tope = topeSmmlv * smmlv * ctx.Period.DaysInPeriod / CalendarConventions.DaysPerMonth;
        if (ibc > tope)
        {
            pasos.Add(new ExplanationStep($"Tope del IBC: {Fmt.Num(topeSmmlv)} SMMLV × {Fmt.Money(smmlv)} × {ctx.Period.DaysInPeriod}/{CalendarConventions.DaysPerMonth}", tope, null));
            ibc = tope;
        }
        ctx.Bases.ContributionBase = ibc;
        pasos.Add(new ExplanationStep(RuleBases.Label(CalculationBase.ContributionBase), ibc, null));

        var prestacional = Suma(c => c.AffectsBenefitsBase);
        ctx.Bases.BenefitsBase = prestacional;
        pasos.Add(new ExplanationStep(RuleBases.Label(CalculationBase.BenefitsBase), prestacional, null));

        var salarioCierre = SalaryTranches.SalaryAtEnd(ctx.Tranches);
        ctx.Bases.TransportAllowanceBase = salarioCierre;
        pasos.Add(new ExplanationStep(RuleBases.Label(CalculationBase.TransportAllowanceBase), salarioCierre, null));

        return pasos;
    }

    /// <summary>
    /// El IBC de los días de vacaciones pagados por la liquidación (novedad informativa
    /// <c>AUSENCIA_VACACIONES</c> con fechas): días comerciales dentro del período × salario
    /// mensual vigente al iniciar el descanso / 30. Queda en
    /// <see cref="Rules.Bases.VacationLeaveContribution"/> para que la ARL lo descuente si la
    /// cooperativa no cotiza riesgos en vacaciones. Cero si no hay novedad de ésas.
    /// </summary>
    public static decimal VacationLeaveContribution(RuleContext ctx, List<ExplanationStep> pasos)
    {
        var total = 0m;
        foreach (var n in ctx.Input.Novelties.Where(n =>
                     n.ConceptCode.Equals(WellKnownConceptCodes.VacationLeave, StringComparison.OrdinalIgnoreCase)
                     && n.StartDate is not null && n.EndDate is not null))
        {
            var desde = n.StartDate!.Value;
            var hasta = n.EndDate!.Value;
            var dias = SalaryTranches.DaysWithinPeriod(ctx.Period, desde, hasta);
            if (dias <= 0) continue;
            var salario = SalaryTranches.SalaryAt(ctx.Tranches, desde);
            var valor = salario / CalendarConventions.DaysPerMonth * dias;
            pasos.Add(new ExplanationStep(
                $"+ IBC de {dias} días de vacaciones ya pagados en la liquidación ({Fmt.Date(desde)} a {Fmt.Date(hasta)}): " +
                $"salario {Fmt.Money(salario)} / {CalendarConventions.DaysPerMonth} × {dias}", valor, null));
            total += valor;
        }
        ctx.Bases.VacationLeaveContribution = total;
        return total;
    }

    /// <summary>
    /// Elegibilidad del auxilio de transporte (FR-012): salario mensual hasta el tope en
    /// SMMLV, ambos parámetros. La clase de empleado la decide la definición.
    /// </summary>
    public static (bool Eligible, string Reason) TransportAllowanceEligibility(RuleContext ctx)
    {
        var salario = SalaryTranches.SalaryAtEnd(ctx.Tranches);
        var smmlv = ctx.Parameters.Value(LegalParameterCodes.Smmlv);
        var topeSmmlv = ctx.Parameters.Value(LegalParameterCodes.TransportAllowanceCapSmmlv);
        var tope = smmlv * topeSmmlv;
        return salario <= tope
            ? (true, $"Salario {Fmt.Money(salario)} ≤ {Fmt.Num(topeSmmlv)} SMMLV ({Fmt.Money(tope)}): tiene derecho.")
            : (false, $"Salario {Fmt.Money(salario)} > {Fmt.Num(topeSmmlv)} SMMLV ({Fmt.Money(tope)}): no tiene derecho.");
    }
}
