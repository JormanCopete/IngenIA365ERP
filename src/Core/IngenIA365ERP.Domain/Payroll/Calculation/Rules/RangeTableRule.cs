using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Tabla por rangos sobre una base (FR-009): retención en la fuente (tramos en UVT,
/// tarifa marginal más fijo) y fondo de solidaridad (tramos en SMMLV, tarifa plana
/// sobre toda la base). La unidad y el modo son datos del parámetro
/// (<c>RangeUnitParameterCode</c>, <c>RangeIsMarginal</c>); el motor no conoce ninguna
/// tabla por su nombre, salvo para aproximar la retención al múltiplo que diga el
/// parámetro opcional <c>RETEFTE_REDONDEO</c>. La búsqueda del tramo vive en
/// <see cref="RangeTableLookup"/> desde la feature 010, porque las liquidaciones
/// especiales la comparten; esta regla sólo la envuelve en una línea.
/// </summary>
public sealed class RangeTableRule : ICalculationRule
{
    public CalculationKind Kind => CalculationKind.RangeTable;

    public CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx)
    {
        var baseKind = concept.BaseKind
            ?? throw new CalculationRefusedException($"El concepto {concept.Code} es «tabla por rangos» y no declara la base.", [concept.Code]);
        var tablaCode = concept.TableParameterCode
            ?? throw new CalculationRefusedException($"El concepto {concept.Code} es «tabla por rangos» y no declara la tabla.", [concept.Code]);
        var baseValor = ctx.Bases.Get(baseKind)
            ?? throw new CalculationRefusedException($"La base {Bases.Label(baseKind)} aún no está calculada para {concept.Code}.", [concept.Code]);

        var tabla = ctx.Parameters.Table(tablaCode);
        var busqueda = RangeTableLookup.Find(tabla, baseValor, ctx.Parameters);

        var exp = new Explanation
        {
            Form = "Tabla por rangos",
            Base = new ExplanationBase(Bases.Label(baseKind), baseValor),
            Parameter = new ExplanationParameter(tabla.Code, tabla.ValidFrom, null),
            Novelty = RuleContext.Describe(novelty),
        };
        exp.Step(Bases.Label(baseKind), baseValor);
        RangeTableLookup.Explain(busqueda, exp);

        if (!busqueda.Found)
        {
            exp.Summary = $"{concept.Name}: no aplica";
            return LineFactory.Create(concept, 0m, exp, novelty, baseAmount: baseValor, parameterCode: tabla.Code, legalParameterId: tabla.Id);
        }

        var tramo = busqueda.Range!;
        var tarifa = busqueda.Rate;
        var valor = busqueda.Value;

        if (concept.Code.Equals(WellKnownConceptCodes.Withholding, StringComparison.OrdinalIgnoreCase))
            valor = RangeTableLookup.RoundToParameterMultiple(valor, LegalParameterCodes.WithholdingRoundingMultiple, ctx.Parameters, exp);

        exp.Factor = tarifa;
        exp.Summary = $"{Fmt.Num(busqueda.BaseInUnits)} {busqueda.UnitName} → tramo desde {Fmt.Num(tramo.FromValue)}: {Fmt.Money(valor)}";
        return LineFactory.Create(concept, valor, exp, novelty, baseAmount: baseValor, factor: tarifa,
            rangeFrom: tramo.FromValue, rangeTo: tramo.ToValue, legalParameterId: tabla.Id, parameterCode: tabla.Code);
    }
}
