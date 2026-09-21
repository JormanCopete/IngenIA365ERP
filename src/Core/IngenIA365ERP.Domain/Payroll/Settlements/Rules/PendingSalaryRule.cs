using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Salario de los días pendientes en la definitiva (FR-018): los días del período
/// ordinario abierto donde cae el retiro, desde su inicio hasta la fecha de retiro, por
/// tramos de salario y con las ausencias descontadas, más el auxilio de transporte
/// proporcional si el empleado lo devenga. Es lo que la nómina ordinaria habría pagado y ya
/// no pagará porque la ficha se cierra al aprobar (FR-020).
/// </summary>
public static class PendingSalaryRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var pendiente = ctx.Input.PendingSalary;
        if (pendiente is null)
        {
            ctx.Skip(WellKnownConceptCodes.PendingSalary, SettlementReasonCodes.SinDiasPendientes,
                "Sin período ordinario abierto informado: la definitiva no paga salario pendiente (la última nómina ya lo pagó).");
            return;
        }

        var def = ctx.Concept(WellKnownConceptCodes.PendingSalary);
        if (def is null) return;

        var inicio = pendiente.PeriodStart.Date;
        var fin = ctx.EffectiveEnd < pendiente.PeriodEnd.Date ? ctx.EffectiveEnd : pendiente.PeriodEnd.Date;
        var tramos = ctx.Tranches(inicio, fin);
        if (tramos.Count == 0)
        {
            ctx.Skip(def.Code, SettlementReasonCodes.SinDiasPendientes, $"El retiro ({Fmt.Date(ctx.EffectiveEnd)}) es anterior al período pendiente ({Fmt.Date(inicio)} a {Fmt.Date(pendiente.PeriodEnd)}): no hay días de salario.");
            return;
        }

        var exp = new Explanation { Form = "Salario por tramos" };
        var total = 0m;
        var diasPagados = 0;
        foreach (var t in tramos)
        {
            var diario = t.MonthlySalary / CalendarConventions.DaysPerMonth;
            var valor = diario * t.PaidDays;
            var ausencia = t.AbsenceDays > 0 ? $" − {t.AbsenceDays} de ausencia" : string.Empty;
            exp.Step($"Tramo {Fmt.Date(t.From)} a {Fmt.Date(t.To)}: {t.Days} días{ausencia} = {t.PaidDays} días × {Fmt.Money(diario)} " +
                     $"(salario {Fmt.Money(t.MonthlySalary)} / {CalendarConventions.DaysPerMonth})", valor);
            total += valor;
            diasPagados += t.PaidDays;
        }
        exp.Step("Salario de los días pendientes", total);
        exp.Summary = $"{diasPagados} días: {Fmt.Money(total)}";
        var salarioCierre = SalaryTranches.SalaryAtEnd(tramos);
        ctx.Add(LineFactory.Create(def, total, exp, quantity: diasPagados, baseAmount: salarioCierre));

        // Auxilio de transporte proporcional a los mismos días (FR-012: el derecho lo decide el tope en SMMLV).
        var aux = ctx.TransportAllowanceFor(salarioCierre);
        if (aux > 0m && ctx.Concepts.Find(WellKnownConceptCodes.TransportAllowance) is { } auxDef)
        {
            var p = ctx.Parameters.Describe(LegalParameterCodes.TransportAllowance);
            var valorAux = aux / CalendarConventions.DaysPerMonth * diasPagados;
            var expAux = new Explanation { Form = "Valor fijo proporcional", Parameter = p };
            expAux.Step($"Auxilio mensual ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})", aux);
            expAux.Step($"Proporcional a {diasPagados} días / {CalendarConventions.DaysPerMonth}", valorAux);
            expAux.Note("Derecho", ctx.TransportAllowanceText(salarioCierre));
            expAux.Summary = $"{Fmt.Money(aux)} × {diasPagados}/{CalendarConventions.DaysPerMonth} = {Fmt.Money(valorAux)}";
            ctx.Add(LineFactory.Create(auxDef, valorAux, expAux, quantity: diasPagados, baseAmount: aux, parameterCode: p.Code));
        }
    }
}
