using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Ajuste de la provisión (FR-004; research «Contabilización de las liquidaciones»): por
/// cada rubro liquidado que tiene provisión par, la diferencia entre lo liquidado y la
/// provisión acumulada del empleado. Positiva = la provisión se quedó corta (aumento de
/// salario reciente) y la diferencia va al gasto; negativa = se libera. La línea es de
/// naturaleza Provisión con signo, y el poster invierte débito y crédito cuando es
/// negativa. Sin provisión informada no se ajusta nada y se dice.
///
/// <para>
/// La provisión de vacaciones cubre <b>todos</b> los días hábiles pendientes del empleado, no
/// sólo los del movimiento que se paga: un disfrute o una compensación parcial (tipo
/// <see cref="SettlementKind.Vacation"/>) compara lo liquidado con la parte de la provisión
/// que corresponde a sus días —provisión acumulada × días del movimiento / días pendientes
/// antes del movimiento— y deja el resto provisionado para los días que siguen pendientes. Si el
/// movimiento consume todos los días pendientes (o más, vacaciones anticipadas) cancela toda la
/// provisión. La definitiva paga todos los días pendientes y cancela la provisión completa, como
/// la prima y las cesantías (D-30). Hasta el 2026-09-21 todo disfrute parcial liberaba la provisión
/// entera y el siguiente salía «corto» al gasto (SC-003).
/// </para>
/// </summary>
public static class ProvisionAdjustmentRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var liquidados = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning && WellKnownConceptCodes.ProvisionPairFor(l.Code) is not null)
            .GroupBy(l => WellKnownConceptCodes.ProvisionPairFor(l.Code)!.Value)
            .ToList();

        foreach (var grupo in liquidados)
        {
            var (provisionCode, ajusteCode) = grupo.Key;
            var liquidado = grupo.Sum(l => l.Amount);
            var rubros = string.Join(" + ", grupo.Select(l => l.Code));

            var provision = ctx.Input.Provisions.FirstOrDefault(p => p.ProvisionConceptCode.Equals(provisionCode, StringComparison.OrdinalIgnoreCase));
            if (provision is null)
            {
                ctx.Skip(ajusteCode, SettlementReasonCodes.SinProvisionInformada,
                    $"{ajusteCode}: no se informó la provisión acumulada {provisionCode} del empleado; el comprobante lleva {rubros} contra la provisión sin ajustar la diferencia.");
                continue;
            }

            var def = ctx.Concept(ajusteCode);
            if (def is null) continue;

            var exp = new Explanation
            {
                Form = "Ajuste de provisión",
                Base = new ExplanationBase($"Provisión acumulada {provisionCode}", provision.Accrued),
            };
            exp.Step($"Liquidado ({rubros})", liquidado);
            exp.Step($"Provisión acumulada {provisionCode} (corridas aprobadas + saldo inicial − consumido)", provision.Accrued);

            var provisionDelMovimiento = ProvisionDeLosDiasLiquidados(ctx, provisionCode, provision.Accrued, exp);
            var diferencia = liquidado - provisionDelMovimiento;
            exp.Step(diferencia >= 0m ? "Diferencia al gasto (la provisión se quedó corta)" : "Liberación de provisión (la provisión superó lo liquidado)", diferencia);
            exp.Summary = diferencia >= 0m
                ? $"Gasto adicional: {Fmt.Money(liquidado)} − {Fmt.Money(provisionDelMovimiento)} = {Fmt.Money(diferencia)}"
                : $"Liberación: {Fmt.Money(liquidado)} − {Fmt.Money(provisionDelMovimiento)} = {Fmt.Money(diferencia)}";
            ctx.Add(LineFactory.Create(def, diferencia, exp, baseAmount: provisionDelMovimiento));
        }
    }

    /// <summary>
    /// La parte de la provisión que este rubro cancela: toda, salvo en una liquidación de vacaciones
    /// parcial, donde es la proporción de los días hábiles del movimiento sobre los pendientes antes de él.
    /// </summary>
    private static decimal ProvisionDeLosDiasLiquidados(SettlementContext ctx, string provisionCode, decimal acumulada, Explanation exp)
    {
        var esVacacionesParcial = ctx.Input.Kind == SettlementKind.Vacation
                                  && provisionCode.Equals(WellKnownConceptCodes.VacationProvision, StringComparison.OrdinalIgnoreCase)
                                  && ctx.Input.MovementToSettle is not null;
        if (!esVacacionesParcial)
        {
            exp.Step("Provisión que cancela esta liquidación (todos los días pendientes)", acumulada);
            return acumulada;
        }

        var dias = ctx.Input.MovementToSettle!.BusinessDays;
        var pendientes = ctx.Vacations.Pending;
        if (pendientes <= 0m || dias >= pendientes)
        {
            exp.Step($"El movimiento consume {Fmt.Num(dias)} días hábiles y el empleado tenía {Fmt.Num(pendientes)} pendientes: cancela toda la provisión", acumulada);
            return acumulada;
        }

        var proporcional = acumulada * dias / pendientes;
        exp.Step("Días hábiles pendientes antes del movimiento", pendientes);
        exp.Step("Días hábiles que liquida el movimiento", dias);
        exp.Step($"Provisión de los días liquidados: {Fmt.Money(acumulada)} × {Fmt.Num(dias)} / {Fmt.Num(pendientes)} (el resto sigue provisionado para los días pendientes)", proporcional);
        return proporcional;
    }
}
