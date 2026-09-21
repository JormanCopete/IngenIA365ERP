using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Cesantías (CST arts. 249 y 253; Ley 50/1990 art. 99; FR-010): base / 30 × días de
/// cesantías por año × días trabajados del año / 360. La base es el último salario más
/// auxilio si no cambió en la ventana de estabilidad (parámetro, hoy tres meses); si
/// cambió, el promedio ponderado del año o del tiempo servido. Los días de suspensión del
/// contrato se descuentan (art. 53). El saldo inicial entra como tramo propio. Los
/// variables prestacionales se promedian sobre la ventana. Ningún número aquí es de ley.
/// </summary>
public static class SeveranceRule
{
    /// <summary>Lo que la regla de intereses necesita saber de las cesantías que acaba de liquidar.</summary>
    public sealed record Outcome(decimal Amount, int DaysForInterest, int DaysAccrued, bool Produced, string? ExclusionCode);

    public static Outcome Evaluate(SettlementContext ctx, DateTime yearStart, DateTime yearEnd)
    {
        var code = WellKnownConceptCodes.Severance;
        if (ctx.BenefitsExclusion() is { } exclusion)
        {
            ctx.Skip(code, exclusion, SettlementContext.ExclusionText(exclusion));
            return new Outcome(0m, 0, 0, false, exclusion);
        }

        var def = ctx.Concept(code);
        if (def is null) return new Outcome(0m, 0, 0, false, null);

        var fin = ctx.EffectiveEnd < yearEnd ? ctx.EffectiveEnd : yearEnd.Date;
        var inicio = ctx.EmploymentStart > yearStart.Date ? ctx.EmploymentStart : yearStart.Date;

        // D-28: una corrida anual aprobada del año ya pagó las cesantías hasta su corte (y consumió el
        // saldo inicial); la definitiva registrada después liquida sólo los días posteriores a ese corte.
        var pagada = ctx.Input.SeverancePaidInRuns
            .Where(p => p.PaidThrough.Date >= yearStart.Date && p.PaidThrough.Date <= yearEnd.Date)
            .OrderBy(p => p.PaidThrough).LastOrDefault();
        if (pagada is not null && pagada.PaidThrough.Date.AddDays(1) > inicio) inicio = pagada.PaidThrough.Date.AddDays(1);
        if (pagada is not null && inicio > fin)
        {
            ctx.Skip(code, SettlementReasonCodes.YaPagadaEnCorridaAnual,
                SettlementContext.ExclusionText(SettlementReasonCodes.YaPagadaEnCorridaAnual) + $" Corrida {pagada.RunPublicId}: {Fmt.Money(pagada.Amount)} hasta el {Fmt.Date(pagada.PaidThrough)}.");
            return new Outcome(0m, 0, 0, false, SettlementReasonCodes.YaPagadaEnCorridaAnual);
        }

        var saldo = OpeningBalanceStep.Apply(ctx, inicio, fin, s => s.AccruedSeverance, s => s.SeveranceDaysAccrued, "cesantías");
        inicio = saldo.WindowStart;

        var tramos = ctx.Tranches(inicio, fin);
        if (tramos.Count == 0 && !saldo.Applies)
        {
            ctx.Skip(code, SettlementReasonCodes.SinDiasEnElAnio, SettlementContext.ExclusionText(SettlementReasonCodes.SinDiasEnElAnio));
            return new Outcome(0m, 0, 0, false, SettlementReasonCodes.SinDiasEnElAnio);
        }

        var diasPorAnio = ctx.Parameters.Value(SettlementParameterCodes.SeveranceDaysPerYear);
        var pDias = ctx.Parameters.Describe(SettlementParameterCodes.SeveranceDaysPerYear);
        var ventanaMeses = (int)ctx.Parameters.Value(SettlementParameterCodes.SeveranceStabilityWindowMonths);
        var pVentana = ctx.Parameters.Describe(SettlementParameterCodes.SeveranceStabilityWindowMonths);
        var variable = ctx.AverageVariable(inicio, fin, vacationBase: false);

        var exp = new Explanation { Form = "Cesantías", Parameter = pDias };
        exp.Note("Período", $"{Fmt.Date(yearStart)} a {Fmt.Date(yearEnd)}; días de cesantías por año: {Fmt.Num(diasPorAnio)} ({pDias.Code}, vigente desde {Fmt.Date(pDias.ValidFrom)}).");
        if (pagada is not null)
            exp.Note("Corrida anual", $"Las cesantías hasta el {Fmt.Date(pagada.PaidThrough)} ya se pagaron en la corrida anual aprobada {pagada.RunPublicId} ({Fmt.Money(pagada.Amount)}); aquí se liquidan los días desde el {Fmt.Date(inicio)} (D-28).");
        if (saldo.Applies)
        {
            exp.Note("Saldo inicial", saldo.Text!);
            exp.Step("Cesantías del saldo inicial", saldo.Amount);
        }

        var vinculados = tramos.Sum(t => t.Days);
        var suspension = ctx.SuspensionDays(inicio, fin);
        var dias = Math.Max(0, vinculados - suspension);
        exp.Step("Días vinculados en la ventana", vinculados);
        if (suspension > 0) exp.Step("− Días de suspensión del contrato (CST art. 53)", suspension);
        exp.Step("Días que causan cesantías", dias);

        // Base: último salario si no cambió en la ventana de estabilidad; si cambió, promedio ponderado.
        decimal baseSalarial;
        var desdeEstabilidad = fin.AddMonths(-ventanaMeses);
        var cambio = tramos.Count > 1 && ctx.SalaryChangedWithin(desdeEstabilidad, fin) && ctx.SalaryChangedWithin(inicio, fin);
        if (cambio)
        {
            var sumaSalario = tramos.Sum(t => (t.MonthlySalary + ctx.TransportAllowanceFor(t.MonthlySalary)) * t.Days);
            baseSalarial = vinculados > 0 ? sumaSalario / vinculados : 0m;
            foreach (var t in tramos)
                exp.Step($"Tramo {Fmt.Date(t.From)} a {Fmt.Date(t.To)}: {t.Days} días con salario {Fmt.Money(t.MonthlySalary)} y {ctx.TransportAllowanceText(t.MonthlySalary)}", t.MonthlySalary + ctx.TransportAllowanceFor(t.MonthlySalary));
            exp.Step($"El salario cambió en los últimos {ventanaMeses} meses ({pVentana.Code}, CST art. 253): promedio ponderado por días de salario y auxilio", baseSalarial);
        }
        else
        {
            var salario = tramos.Count > 0 ? tramos[^1].MonthlySalary : ctx.SalaryAt(fin);
            baseSalarial = salario + ctx.TransportAllowanceFor(salario);
            exp.Step($"Salario sin cambios en los últimos {ventanaMeses} meses ({pVentana.Code}): último salario {Fmt.Money(salario)} + {ctx.TransportAllowanceText(salario)}", baseSalarial);
        }
        if (variable != 0m) exp.Step("+ Promedio mensual de devengos variables prestacionales", variable);
        var baseMensual = baseSalarial + variable;
        exp.Base = new ExplanationBase("Base prestacional", baseMensual);

        var proporcional = baseMensual / CalendarConventions.DaysPerMonth * diasPorAnio * dias / 360m;
        exp.Step($"Base / {CalendarConventions.DaysPerMonth} × {Fmt.Num(diasPorAnio)} × {dias} / 360", proporcional);
        var total = saldo.Amount + proporcional;
        exp.Step("Cesantías", total);
        exp.Summary = $"{dias + saldo.Days} días sobre {Fmt.Money(baseMensual)}: {Fmt.Money(total)}";

        var line = LineFactory.Create(def, total, exp, quantity: dias + saldo.Days, baseAmount: baseMensual, parameterCode: pDias.Code);
        ctx.Add(line);
        // Los intereses corren sobre los días vinculados (sin descontar suspensiones, research R1) más los del saldo.
        return new Outcome(line.Amount, vinculados + saldo.Days, dias + saldo.Days, true, null);
    }

    /// <summary>Inicio y fin del año calendario de una fecha.</summary>
    public static (DateTime Start, DateTime End) YearOf(DateTime date) => (new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31));
}

/// <summary>
/// Intereses a las cesantías (Ley 52/1975 art. 1; Decreto 116/1976 art. 2; FR-011):
/// cesantías liquidadas × porcentaje anual (parámetro) × días / 360. Se calculan sobre el
/// valor ya redondeado que se paga, y comparten la exclusión de las cesantías.
/// </summary>
public static class SeveranceInterestRule
{
    public static void Evaluate(SettlementContext ctx, SeveranceRule.Outcome cesantias)
    {
        var code = WellKnownConceptCodes.SeveranceInterest;
        if (cesantias.ExclusionCode is { } exclusion)
        {
            ctx.Skip(code, exclusion, SettlementContext.ExclusionText(exclusion));
            return;
        }
        if (!cesantias.Produced) return;

        var def = ctx.Concept(code);
        if (def is null) return;

        var pct = ctx.Parameters.Fraction(SettlementParameterCodes.SeveranceInterestPct);
        var p = ctx.Parameters.Describe(SettlementParameterCodes.SeveranceInterestPct);
        var valor = cesantias.Amount * pct * cesantias.DaysForInterest / 360m;

        var exp = new Explanation
        {
            Form = "Intereses a las cesantías",
            Base = new ExplanationBase("Cesantías liquidadas", cesantias.Amount),
            Factor = pct,
            Parameter = p,
        };
        exp.Step("Cesantías liquidadas", cesantias.Amount);
        exp.Step($"Porcentaje anual ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})", pct * 100m);
        exp.Step("Días vinculados en el año", cesantias.DaysForInterest);
        exp.Step($"Cesantías × {Fmt.Pct(pct)} × {cesantias.DaysForInterest} / 360", valor);
        if (ctx.Input.OpeningBalance is { AccruedSeveranceInterest: > 0m } saldo)
            exp.Note("Saldo inicial", $"El saldo inicial traía intereses por {Fmt.Money(saldo.AccruedSeveranceInterest)}; no se suman porque los intereses se liquidan sobre el total de las cesantías del año.");
        exp.Summary = $"{Fmt.Money(cesantias.Amount)} × {Fmt.Pct(pct)} × {cesantias.DaysForInterest}/360 = {Fmt.Money(valor)}";
        ctx.Add(LineFactory.Create(def, valor, exp, quantity: cesantias.DaysForInterest, baseAmount: cesantias.Amount, factor: pct, parameterCode: p.Code));
    }
}
