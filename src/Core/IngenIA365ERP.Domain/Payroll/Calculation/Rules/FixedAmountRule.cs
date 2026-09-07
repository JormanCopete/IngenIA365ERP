using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Valor fijo (FR-009): el de la novedad si el concepto lo pide, si no el del parámetro
/// legal con vigencia, si no el fijo de la definición. Opcionalmente proporcional a los
/// días pagados (auxilio de transporte) y afectado por un porcentaje.
/// </summary>
public sealed class FixedAmountRule : ICalculationRule
{
    public CalculationKind Kind => CalculationKind.FixedAmount;

    public CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx)
    {
        var exp = new Explanation { Form = "Valor fijo", Novelty = RuleContext.Describe(novelty) };

        decimal valor;
        ExplanationParameter? parametro = null;
        if (novelty?.Amount is { } declarado && (concept.RequiresAmount || concept.FixedAmount is null))
        {
            valor = declarado;
            exp.Step("Valor registrado en la novedad", valor);
        }
        else if (!string.IsNullOrWhiteSpace(concept.AmountParameterCode))
        {
            valor = ctx.Parameters.Value(concept.AmountParameterCode);
            parametro = ctx.Parameters.Describe(concept.AmountParameterCode);
            exp.Parameter = parametro;
            exp.Step($"Valor mensual según parámetro {parametro.Code} (vigente desde {Fmt.Date(parametro.ValidFrom)})", valor);
        }
        else
        {
            valor = concept.FixedAmount ?? 0m;
            exp.Step("Valor fijo de la definición", valor);
        }

        decimal? cantidad = null;
        if (concept.ProrateByDays)
        {
            var dias = ctx.PaidDays;
            cantidad = dias;
            exp.Step("Días pagados del período", dias);
            valor = valor * dias / CalendarConventions.DaysPerMonth;
            exp.Step($"Proporción: valor × {dias} / {CalendarConventions.DaysPerMonth}", valor);
        }

        var pct = ctx.ResolvePercent(concept);
        if (pct is null)
        {
            ctx.Skip($"{concept.Code}: el porcentaje depende de la clase de riesgo ARL y el empleado no tiene una registrada.");
            return null;
        }
        if (pct.Value.Fraction != 1m)
        {
            exp.Factor = pct.Value.Fraction;
            exp.Parameter ??= pct.Value.Parameter;
            valor *= pct.Value.Fraction;
            exp.Step($"× {Fmt.Pct(pct.Value.Fraction)}", valor);
        }

        if (concept.MaxAmount is { } tope && valor > tope)
        {
            exp.Step($"Tope del concepto ({Fmt.Money(tope)})", tope);
            valor = tope;
        }

        exp.Summary = $"{concept.Name}: {Fmt.Money(valor)}";
        return LineFactory.Create(concept, valor, exp, novelty, quantity: cantidad,
            factor: exp.Factor, parameterCode: parametro?.Code);
    }
}
