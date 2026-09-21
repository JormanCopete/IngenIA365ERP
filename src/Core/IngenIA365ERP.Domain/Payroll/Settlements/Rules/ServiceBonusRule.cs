using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Prima de servicios (CST art. 306; Ley 1788/2016; FR-008, FR-009): por cada tramo de
/// salario del semestre, (salario + auxilio si lo devengó + promedio de variables) / 30 ×
/// días de prima por año × días del tramo / 360; los tramos se suman, lo que equivale al
/// promedio ponderado del semestre. Los días de suspensión no se descuentan (el art. 53
/// sólo autoriza descontarlos de vacaciones, cesantías y jubilación). El saldo inicial
/// entra como tramo propio y la prima ya pagada en una definitiva del semestre se resta.
/// «Días de prima por año» es el parámetro <c>PRIMA_DIAS_ANIO</c>: aquí no hay 15 ni 30.
/// </summary>
public static class ServiceBonusRule
{
    /// <summary>Devuelve el código de exclusión si no hay línea; nulo si la produjo (o la omitió por un motivo menor ya anotado).</summary>
    public static string? Evaluate(SettlementContext ctx, DateTime semesterStart, DateTime semesterEnd)
    {
        var code = WellKnownConceptCodes.ServiceBonus;
        if (ctx.BenefitsExclusion() is { } exclusion)
        {
            ctx.Skip(code, exclusion, SettlementContext.ExclusionText(exclusion));
            return exclusion;
        }

        var def = ctx.Concept(code);
        if (def is null) return null;

        var fin = ctx.EffectiveEnd < semesterEnd ? ctx.EffectiveEnd : semesterEnd.Date;
        var inicio = ctx.EmploymentStart > semesterStart.Date ? ctx.EmploymentStart : semesterStart.Date;

        // FR-009: la definitiva ya pagó la prima proporcional de este semestre. D-30, el otro sentido: la
        // corrida semestral aprobada antes de registrar el retiro ya pagó la prima completa del semestre.
        var pagadas = ctx.Input.ServiceBonusPaidInSettlements
            .Where(p => p.PaidThrough.Date >= semesterStart.Date && p.PaidThrough.Date <= semesterEnd.Date)
            .ToList();
        if (pagadas.Count > 0 && ctx.Employee.TerminationDate is { } retiro && retiro.Date <= semesterEnd.Date)
        {
            var semestral = pagadas.FirstOrDefault(p => p.PaidBy == SettlementKind.ServiceBonus);
            var motivo = semestral is null ? SettlementReasonCodes.YaPagadaEnDefinitiva : SettlementReasonCodes.YaPagadaEnCorridaSemestral;
            var texto = SettlementContext.ExclusionText(motivo);
            if (semestral is not null)
                texto += $" Corrida {semestral.RunPublicId}: {Fmt.Money(semestral.Amount)} por {semestral.Days} días hasta el {Fmt.Date(semestral.PaidThrough)}.";
            ctx.Skip(code, motivo, texto);
            return motivo;
        }

        var saldo = OpeningBalanceStep.Apply(ctx, inicio, fin, s => s.AccruedServiceBonus, s => s.ServiceBonusDaysAccrued, "prima");
        inicio = saldo.WindowStart;

        var tramos = ctx.Tranches(inicio, fin);
        if (tramos.Count == 0 && !saldo.Applies)
        {
            ctx.Skip(code, SettlementReasonCodes.SinDiasEnElSemestre, SettlementContext.ExclusionText(SettlementReasonCodes.SinDiasEnElSemestre));
            return SettlementReasonCodes.SinDiasEnElSemestre;
        }

        var diasPorAnio = ctx.Parameters.Value(SettlementParameterCodes.ServiceBonusDaysPerYear);
        var pDias = ctx.Parameters.Describe(SettlementParameterCodes.ServiceBonusDaysPerYear);
        var variable = ctx.AverageVariable(semesterStart, semesterEnd, vacationBase: false);

        var exp = new Explanation
        {
            Form = "Prima de servicios",
            Parameter = pDias,
        };
        exp.Note("Semestre", $"{Fmt.Date(semesterStart)} a {Fmt.Date(semesterEnd)}; días de prima por año: {Fmt.Num(diasPorAnio)} ({pDias.Code}, vigente desde {Fmt.Date(pDias.ValidFrom)}).");
        if (saldo.Applies)
        {
            exp.Note("Saldo inicial", saldo.Text!);
            exp.Step("Prima del saldo inicial", saldo.Amount);
        }
        if (variable != 0m) exp.Step("Promedio mensual de devengos variables prestacionales del semestre", variable);

        var total = saldo.Amount;
        var dias = saldo.Days;
        var diasTramos = 0;
        var basePonderada = 0m;
        foreach (var t in tramos)
        {
            var aux = ctx.TransportAllowanceFor(t.MonthlySalary);
            var baseMensual = t.MonthlySalary + aux + variable;
            var valor = baseMensual / CalendarConventions.DaysPerMonth * diasPorAnio * t.Days / 360m;
            exp.Step($"Tramo {Fmt.Date(t.From)} a {Fmt.Date(t.To)}: {t.Days} días × (salario {Fmt.Money(t.MonthlySalary)} + {ctx.TransportAllowanceText(t.MonthlySalary)}" +
                     (variable != 0m ? $" + variables {Fmt.Money(variable)}" : string.Empty) +
                     $") / {CalendarConventions.DaysPerMonth} × {Fmt.Num(diasPorAnio)} / 360", valor);
            total += valor;
            dias += t.Days;
            diasTramos += t.Days;
            basePonderada += baseMensual * t.Days;
        }
        if (diasTramos > 0) basePonderada /= diasTramos;
        exp.Step("Días del semestre que causan prima", dias);

        foreach (var p in pagadas)
        {
            exp.Step($"− Prima ya pagada en la liquidación definitiva {p.RunPublicId} hasta el {Fmt.Date(p.PaidThrough)} ({p.Days} días)", p.Amount);
            total -= p.Amount;
        }

        exp.Base = new ExplanationBase("Base prestacional promedio del semestre", basePonderada);
        exp.Step("Prima de servicios", total);
        exp.Summary = saldo.Applies
            ? $"Saldo inicial {Fmt.Money(saldo.Amount)} ({saldo.Days} días) + {diasTramos} días × {Fmt.Money(basePonderada)} × {Fmt.Num(diasPorAnio)}/360 = {Fmt.Money(total)}"
            : $"{dias} días × {Fmt.Money(basePonderada)} × {Fmt.Num(diasPorAnio)}/360 = {Fmt.Money(total)}";
        ctx.Add(LineFactory.Create(def, total, exp, quantity: dias, baseAmount: basePonderada, parameterCode: pDias.Code));
        return null;
    }

    /// <summary>Inicio y fin del semestre calendario al que pertenece una fecha.</summary>
    public static (DateTime Start, DateTime End) SemesterOf(DateTime date)
    {
        var primero = date.Month <= 6;
        var start = new DateTime(date.Year, primero ? 1 : 7, 1);
        var end = primero ? new DateTime(date.Year, 6, 30) : new DateTime(date.Year, 12, 31);
        return (start, end);
    }
}
