using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Suma o porcentaje de otros conceptos del mismo período (FR-009): «+HEX_DIURNA*1;
/// +COMISION*1» con signo y peso por componente, y opcionalmente un porcentaje sobre
/// el total. Lee las líneas ya calculadas: el motor garantiza el orden con
/// <see cref="ConceptDependencyGraph"/>.
/// </summary>
public sealed class CompositeOfConceptsRule : ICalculationRule
{
    public CalculationKind Kind => CalculationKind.CompositeOfConcepts;

    public CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx)
    {
        var componentes = ConceptDependencyGraph.ParseComponents(concept.ComponentConceptCodes);
        if (componentes.Count == 0)
            throw new CalculationRefusedException($"El concepto compuesto {concept.Code} no declara componentes.", [concept.Code]);

        var exp = new Explanation { Form = "Suma de conceptos", Novelty = RuleContext.Describe(novelty) };
        var total = 0m;
        foreach (var comp in componentes)
        {
            var valorComp = ctx.Lines
                .Where(l => l.Code.Equals(comp.Code, StringComparison.OrdinalIgnoreCase))
                .Sum(l => l.Amount);
            var aporte = comp.Sign * comp.Weight * valorComp;
            var signo = comp.Sign < 0 ? "−" : "+";
            exp.Step($"{signo} {comp.Code}" + (comp.Weight != 1m ? $" × {Fmt.Num(comp.Weight)}" : string.Empty)
                     + (valorComp == 0m ? " (sin línea en este período)" : string.Empty), aporte);
            total += aporte;
        }
        exp.Step("Suma de componentes", total);

        var pct = ctx.ResolvePercent(concept);
        if (pct is null)
        {
            ctx.Skip($"{concept.Code}: el porcentaje depende de la clase de riesgo ARL y el empleado no tiene una registrada.");
            return null;
        }
        var fraccion = pct.Value.Fraction;
        if (fraccion != 1m)
        {
            exp.Factor = fraccion;
            exp.Parameter = pct.Value.Parameter;
            total *= fraccion;
            exp.Step($"× {Fmt.Pct(fraccion)}", total);
        }

        if (concept.MaxAmount is { } tope && total > tope)
        {
            exp.Step($"Tope del concepto ({Fmt.Money(tope)})", tope);
            total = tope;
        }

        exp.Summary = $"{concept.Name}: {Fmt.Money(total)}";
        return LineFactory.Create(concept, total, exp, novelty, factor: exp.Factor, parameterCode: exp.Parameter?.Code);
    }
}
