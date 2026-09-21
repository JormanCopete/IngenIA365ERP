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
/// </summary>
public static class ProvisionAdjustmentRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var liquidados = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning && SettlementConceptCodes.ProvisionPairFor(l.Code) is not null)
            .GroupBy(l => SettlementConceptCodes.ProvisionPairFor(l.Code)!.Value)
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

            var diferencia = liquidado - provision.Accrued;
            var exp = new Explanation
            {
                Form = "Ajuste de provisión",
                Base = new ExplanationBase($"Provisión acumulada {provisionCode}", provision.Accrued),
            };
            exp.Step($"Liquidado ({rubros})", liquidado);
            exp.Step($"− Provisión acumulada {provisionCode} (corridas aprobadas + saldo inicial − consumido)", provision.Accrued);
            exp.Step(diferencia >= 0m ? "Diferencia al gasto (la provisión se quedó corta)" : "Liberación de provisión (la provisión superó lo liquidado)", diferencia);
            exp.Summary = diferencia >= 0m
                ? $"Gasto adicional: {Fmt.Money(liquidado)} − {Fmt.Money(provision.Accrued)} = {Fmt.Money(diferencia)}"
                : $"Liberación: {Fmt.Money(liquidado)} − {Fmt.Money(provision.Accrued)} = {Fmt.Money(diferencia)}";
            ctx.Add(LineFactory.Create(def, diferencia, exp, baseAmount: provision.Accrued));
        }
    }
}
