using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

using IngenIA365ERP.Domain.Enums.Payroll;
namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Vacaciones (CST arts. 186, 189 y 192; Ley 995/2005; FR-014 a FR-017). Los días
/// causados son días trabajados × días de vacaciones por año (parámetro) / 360, menos
/// las suspensiones (art. 53), más el saldo inicial, menos lo disfrutado y compensado: el
/// saldo se deriva, nunca se guarda. El valor del día es el salario ordinario del corte
/// sin auxilio ni extras (los conceptos con «entra a la base de vacaciones» ya vienen
/// promediados en <c>MonthlyBases</c>) dividido en 30. En la definitiva se paga el total
/// pendiente; en un disfrute, los días calendario si la política paga anticipado (D-01);
/// en una compensación, los hábiles compensados hasta el máximo parametrizado (FR-016).
/// </summary>
public static class VacationRule
{
    /// <summary>Saldo al corte, con los pasos para el detalle. No produce línea.</summary>
    public static VacationBalance Balance(SettlementContext ctx, Explanation exp)
    {
        var fin = ctx.EffectiveEnd;
        var inicio = ctx.EmploymentStart;
        var saldo = ctx.Input.OpeningBalance;
        var diasSaldo = 0m;
        if (saldo is not null && saldo.AsOfDate.Date >= inicio && saldo.AsOfDate.Date <= fin)
        {
            diasSaldo = saldo.PendingVacationDays;
            exp.Note("Saldo inicial", $"Saldo inicial de vacaciones al {Fmt.Date(saldo.AsOfDate)} digitado por {(string.IsNullOrWhiteSpace(saldo.EnteredBy) ? "(sin registro)" : saldo.EnteredBy)}" +
                                      (saldo.EnteredAt is { } en ? $" el {Fmt.Date(en)}" : string.Empty) + $": {Fmt.Num(diasSaldo)} días hábiles pendientes.");
            inicio = saldo.AsOfDate.Date.AddDays(1);
        }

        var diasPorAnio = ctx.Parameters.Value(SettlementParameterCodes.VacationDaysPerYear);
        var p = ctx.Parameters.Describe(SettlementParameterCodes.VacationDaysPerYear);

        var vinculados = fin >= inicio ? CalendarConventions.Days(inicio, fin) : 0;
        var suspension = fin >= inicio ? ctx.SuspensionDays(inicio, fin) : 0;
        var trabajados = Math.Max(0, vinculados - suspension);
        var causados = trabajados * diasPorAnio / 360m;

        exp.Step($"Días trabajados del {Fmt.Date(inicio)} al {Fmt.Date(fin)}" + (suspension > 0 ? $" ({vinculados} vinculados − {suspension} de suspensión, CST art. 53)" : string.Empty), trabajados);
        exp.Step($"Días hábiles causados: {trabajados} × {Fmt.Num(diasPorAnio)} / 360 ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})", causados);
        if (diasSaldo != 0m) exp.Step("+ Saldo inicial (días hábiles)", diasSaldo);

        var consumidos = 0m;
        foreach (var m in ctx.Input.VacationMovements.Where(m => !m.IsCancelled))
        {
            switch (m.Kind)
            {
                case VacationMovementKind.Enjoyment:
                case VacationMovementKind.Compensation:
                case VacationMovementKind.SettlementPayout:
                    consumidos += m.BusinessDays;
                    exp.Step($"− {Etiqueta(m.Kind)}{Fechas(m)}", m.BusinessDays);
                    break;
                case VacationMovementKind.Adjustment:
                    consumidos -= m.BusinessDays;
                    exp.Step($"± Ajuste{Fechas(m)}{(m.Description is { } d ? $": {d}" : string.Empty)}", m.BusinessDays);
                    break;
            }
        }

        var pendientes = causados + diasSaldo - consumidos;
        exp.Step("Días hábiles pendientes", pendientes);
        var balance = new VacationBalance(causados, diasSaldo, consumidos, pendientes);
        ctx.Vacations = balance;
        return balance;
    }

    /// <summary>Valor del día de vacaciones: salario ordinario del corte más promedio de variables de vacaciones del último año, sobre 30.</summary>
    public static (decimal Daily, decimal MonthlyBase) DailyValue(SettlementContext ctx, Explanation exp)
    {
        var fin = ctx.EffectiveEnd;
        var salario = ctx.SalaryAt(fin);
        var variable = ctx.AverageVariable(fin.AddMonths(-12).AddDays(1), fin, vacationBase: true);
        var baseMensual = salario + variable;
        exp.Step("Salario ordinario vigente al corte (sin auxilio de transporte, CST art. 192)", salario);
        if (variable != 0m) exp.Step("+ Promedio mensual de devengos variables de vacaciones del último año", variable);
        var diario = baseMensual / CalendarConventions.DaysPerMonth;
        exp.Step($"Valor del día: base / {CalendarConventions.DaysPerMonth}", diario);
        exp.Base = new ExplanationBase("Salario ordinario para vacaciones", baseMensual);
        return (diario, baseMensual);
    }

    /// <summary>Definitiva: paga en dinero todos los días hábiles pendientes (CST art. 189 num. 2; Ley 995/2005).</summary>
    public static void EvaluateSettlementPayout(SettlementContext ctx)
    {
        var code = WellKnownConceptCodes.VacationCompensation;
        if (ctx.BenefitsExclusion(forVacations: true) is { } exclusion)
        {
            ctx.Skip(code, exclusion, SettlementContext.ExclusionText(exclusion));
            return;
        }
        var def = ctx.Concept(code);
        if (def is null) return;

        var exp = new Explanation { Form = "Vacaciones pendientes al retiro" };
        var balance = Balance(ctx, exp);
        if (balance.Pending <= 0m)
        {
            if (balance.Pending < 0m)
                ctx.Warn($"El empleado tiene {Fmt.Num(-balance.Pending)} días hábiles de vacaciones disfrutados de más: no se descuentan en la definitiva; revíselo con la responsable.");
            ctx.Skip(code, SettlementReasonCodes.SinDiasPendientes, $"Sin días de vacaciones pendientes al retiro ({Fmt.Num(balance.Pending)}).");
            return;
        }

        var (diario, baseMensual) = DailyValue(ctx, exp);
        var valor = balance.Pending * diario;
        exp.Step($"{Fmt.Num(balance.Pending)} días hábiles pendientes × {Fmt.Money(diario)}", valor);
        exp.Summary = $"{Fmt.Num(balance.Pending)} días × {Fmt.Money(diario)} = {Fmt.Money(valor)}";
        ctx.Add(LineFactory.Create(def, valor, exp, quantity: balance.Pending, baseAmount: baseMensual));
    }

    /// <summary>Liquidación de vacaciones: el disfrute o la compensación que se está pagando.</summary>
    public static void EvaluateMovement(SettlementContext ctx)
    {
        var movimiento = ctx.Input.MovementToSettle;
        if (movimiento is null)
        {
            ctx.Refuse("La liquidación de vacaciones no trae el movimiento (disfrute o compensación) que paga.");
            return;
        }
        if (ctx.BenefitsExclusion(forVacations: true) is { } exclusion)
        {
            ctx.Skip(WellKnownConceptCodes.VacationPayout, exclusion, SettlementContext.ExclusionText(exclusion));
            return;
        }

        var exp = new Explanation { Form = movimiento.Kind == VacationMovementKind.Compensation ? "Vacaciones compensadas en dinero" : "Vacaciones disfrutadas" };
        var balance = Balance(ctx, exp);

        switch (movimiento.Kind)
        {
            case VacationMovementKind.Enjoyment:
            {
                var code = WellKnownConceptCodes.VacationPayout;
                if (!ctx.Policies.VacacionesPagoAnticipado)
                {
                    ctx.Skip(code, SettlementReasonCodes.LoPagaLaNominaOrdinaria,
                        "La política de la empresa es que la nómina ordinaria pague los días de vacaciones (VacacionesPagoAnticipado = no): esta liquidación sólo registra el disfrute.");
                    return;
                }
                var def = ctx.Concept(code);
                if (def is null) return;
                if (movimiento.BusinessDays > balance.Pending)
                    ctx.Warn($"El disfrute consume {Fmt.Num(movimiento.BusinessDays)} días hábiles y el empleado tiene {Fmt.Num(balance.Pending)} pendientes: quedan {Fmt.Num(balance.Pending - movimiento.BusinessDays)} (anticipados).");
                var (diario, baseMensual) = DailyValue(ctx, exp);
                var valor = movimiento.CalendarDays * diario;
                exp.Note("Disfrute", $"{Fechas(movimiento).TrimStart()}: {Fmt.Num(movimiento.BusinessDays)} días hábiles, {movimiento.CalendarDays} calendario (D-01: se pagan los días calendario del descanso).");
                exp.Step($"{movimiento.CalendarDays} días calendario × {Fmt.Money(diario)}", valor);
                exp.Summary = $"{movimiento.CalendarDays} días × {Fmt.Money(diario)} = {Fmt.Money(valor)}";
                ctx.Add(LineFactory.Create(def, valor, exp, quantity: movimiento.CalendarDays, baseAmount: baseMensual));
                return;
            }
            case VacationMovementKind.Compensation:
            {
                var code = WellKnownConceptCodes.VacationCompensation;
                var def = ctx.Concept(code);
                if (def is null) return;
                var pct = ctx.Parameters.Fraction(SettlementParameterCodes.VacationCompensablePct);
                var p = ctx.Parameters.Describe(SettlementParameterCodes.VacationCompensablePct);
                var compensadasAntes = ctx.Input.VacationMovements
                    .Where(m => !m.IsCancelled && m.Kind == VacationMovementKind.Compensation)
                    .Sum(m => m.BusinessDays);
                var totalCausado = balance.Accrued + balance.OpeningBalanceDays;
                var maximo = totalCausado * pct;
                exp.Step($"Máximo compensable: {Fmt.Pct(pct)} de {Fmt.Num(totalCausado)} días causados ({p.Code}, CST art. 189)", maximo);
                exp.Step("Compensadas antes", compensadasAntes);
                if (compensadasAntes + movimiento.BusinessDays > maximo)
                {
                    var disponible = Math.Max(0m, maximo - compensadasAntes);
                    ctx.Refuse($"La compensación de {Fmt.Num(movimiento.BusinessDays)} días hábiles supera el máximo legal: sólo pueden compensarse {Fmt.Num(disponible)} días más " +
                               $"({Fmt.Pct(pct)} de {Fmt.Num(totalCausado)} causados = {Fmt.Num(maximo)}, ya compensados {Fmt.Num(compensadasAntes)}).");
                    ctx.Skip(code, SettlementReasonCodes.CompensacionExcedeMaximo, $"Compensación rechazada: máximo permitido {Fmt.Num(disponible)} días.");
                    return;
                }
                if (movimiento.BusinessDays > balance.Pending)
                    ctx.Warn($"La compensación de {Fmt.Num(movimiento.BusinessDays)} días supera los {Fmt.Num(balance.Pending)} pendientes.");
                var (diario, baseMensual) = DailyValue(ctx, exp);
                var valor = movimiento.BusinessDays * diario;
                exp.Step($"{Fmt.Num(movimiento.BusinessDays)} días hábiles compensados × {Fmt.Money(diario)}", valor);
                exp.Summary = $"{Fmt.Num(movimiento.BusinessDays)} días × {Fmt.Money(diario)} = {Fmt.Money(valor)}";
                ctx.Add(LineFactory.Create(def, valor, exp, quantity: movimiento.BusinessDays, baseAmount: baseMensual, factor: pct, parameterCode: p.Code));
                return;
            }
            default:
                ctx.Refuse($"Un movimiento de tipo {movimiento.Kind} no se liquida: sólo disfrutes y compensaciones.");
                return;
        }
    }

    private static string Etiqueta(VacationMovementKind kind) => kind switch
    {
        VacationMovementKind.Enjoyment => "Disfrute",
        VacationMovementKind.Compensation => "Compensación en dinero",
        VacationMovementKind.SettlementPayout => "Pago al retiro",
        _ => kind.ToString(),
    };

    private static string Fechas(VacationMovementInput m) =>
        m.StartDate is { } d ? $" {Fmt.Date(d)}" + (m.EndDate is { } h ? $" a {Fmt.Date(h)}" : string.Empty) : string.Empty;
}
