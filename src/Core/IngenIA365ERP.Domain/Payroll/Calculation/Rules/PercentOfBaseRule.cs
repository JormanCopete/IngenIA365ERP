using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Porcentaje sobre una base (FR-009): salud, pensión, aportes del empleador, ARL por
/// clase, parafiscales y provisiones. El porcentaje viene del parámetro legal con
/// vigencia o del fijo de la definición; la base, de las que el motor ya construyó.
/// </summary>
public sealed class PercentOfBaseRule : ICalculationRule
{
    public CalculationKind Kind => CalculationKind.PercentOfBase;

    public CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx)
    {
        var baseKind = concept.BaseKind
            ?? throw new CalculationRefusedException($"El concepto {concept.Code} es «porcentaje sobre base» y no declara la base.", [concept.Code]);
        var baseValor = ctx.Bases.Get(baseKind)
            ?? throw new CalculationRefusedException($"La base {Bases.Label(baseKind)} aún no está calculada para {concept.Code}: el orden de evaluación es incorrecto.", [concept.Code]);

        var pct = ctx.ResolvePercent(concept);
        if (pct is null)
        {
            ctx.Skip($"{concept.Code}: el porcentaje depende de la clase de riesgo ARL y el empleado no tiene una registrada.");
            return null;
        }
        var (fraccion, parametro) = pct.Value;

        var exp = new Explanation
        {
            Form = "Base × porcentaje",
            Base = new ExplanationBase(Bases.Label(baseKind), baseValor),
            Factor = fraccion,
            Parameter = parametro,
            Novelty = RuleContext.Describe(novelty),
        };
        exp.Step(Bases.Label(baseKind), baseValor);
        exp.Step(parametro is null
            ? $"Porcentaje de la definición ({Fmt.Pct(fraccion)})"
            : $"Porcentaje según parámetro {parametro.Code} vigente desde {Fmt.Date(parametro.ValidFrom)} ({Fmt.Pct(fraccion)})",
            fraccion * 100m);

        var valor = baseValor * fraccion;
        if (concept.MaxAmount is { } tope && valor > tope)
        {
            exp.Step($"Tope del concepto ({Fmt.Money(tope)})", tope);
            valor = tope;
        }
        exp.Step("Resultado", valor);
        exp.Summary = $"{Fmt.Money(baseValor)} × {Fmt.Pct(fraccion)} = {Fmt.Money(valor)}";

        return LineFactory.Create(concept, valor, exp, novelty, baseAmount: baseValor, factor: fraccion,
            parameterCode: parametro?.Code);
    }
}
