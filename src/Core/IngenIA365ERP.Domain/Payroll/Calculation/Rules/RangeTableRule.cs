using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Tabla por rangos sobre una base (FR-009): retención en la fuente (tramos en UVT,
/// tarifa marginal más fijo) y fondo de solidaridad (tramos en SMMLV, tarifa plana
/// sobre toda la base). La unidad y el modo son datos del parámetro
/// (<c>RangeUnitParameterCode</c>, <c>RangeIsMarginal</c>); el motor no conoce ninguna
/// tabla por su nombre, salvo para aproximar la retención al múltiplo que diga el
/// parámetro opcional <c>RETEFTE_REDONDEO</c>.
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
        var unidadValor = 1m;
        var unidadNombre = "pesos";
        if (!string.IsNullOrWhiteSpace(tabla.RangeUnitParameterCode))
        {
            unidadValor = ctx.Parameters.Value(tabla.RangeUnitParameterCode);
            unidadNombre = tabla.RangeUnitParameterCode;
        }

        var exp = new Explanation
        {
            Form = "Tabla por rangos",
            Base = new ExplanationBase(Bases.Label(baseKind), baseValor),
            Parameter = new ExplanationParameter(tabla.Code, tabla.ValidFrom, null),
            Novelty = RuleContext.Describe(novelty),
        };
        exp.Step(Bases.Label(baseKind), baseValor);
        // De dónde salen los tramos: la norma, o la tabla propia del plan de nómina (Parámetros de retención).
        if (!string.IsNullOrWhiteSpace(tabla.Source)) exp.Note("Tabla", tabla.Source);

        var enUnidades = baseValor / unidadValor;
        if (unidadValor != 1m)
        {
            var pu = ctx.Parameters.Describe(tabla.RangeUnitParameterCode!);
            exp.Step($"Base en {unidadNombre} (1 {unidadNombre} = {Fmt.Money(unidadValor)}, vigente desde {Fmt.Date(pu.ValidFrom)})", enUnidades);
        }

        var tramo = tabla.Ranges
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Order).ThenBy(r => r.FromValue)
            .FirstOrDefault(r => r.Contains(enUnidades));

        if (tramo is null)
        {
            exp.Note("Tramo", $"La base ({Fmt.Num(enUnidades)} {unidadNombre}) no cae en ningún tramo de {tabla.Code}: no aplica.");
            exp.Summary = $"{concept.Name}: no aplica";
            return LineFactory.Create(concept, 0m, exp, novelty, baseAmount: baseValor, parameterCode: tabla.Code, legalParameterId: tabla.Id);
        }

        exp.Range = new ExplanationRange(tramo.FromValue, tramo.ToValue, tramo.Rate, tramo.FixedValue, unidadNombre, enUnidades);
        var hasta = tramo.ToValue is { } t ? Fmt.Num(t) : "en adelante";
        exp.Note("Tramo", $"{Fmt.Num(tramo.FromValue)} a {hasta} {unidadNombre}: tarifa {Fmt.Pct((tramo.Rate ?? 0m) / 100m)}" +
                          (tramo.FixedValue is { } f && f != 0m ? $" más {Fmt.Num(f)} {unidadNombre} fijos" : string.Empty));

        var tarifa = (tramo.Rate ?? 0m) / 100m;
        decimal valor;
        if (tabla.RangeIsMarginal)
        {
            var exceso = enUnidades - tramo.FromValue;
            exp.Step($"Exceso sobre el inicio del tramo ({unidadNombre})", exceso);
            var enUnidadesResultado = exceso * tarifa + (tramo.FixedValue ?? 0m);
            exp.Step($"Exceso × tarifa + fijo ({unidadNombre})", enUnidadesResultado);
            valor = enUnidadesResultado * unidadValor;
            exp.Step("En pesos", valor);
        }
        else
        {
            valor = baseValor * tarifa + (tramo.FixedValue ?? 0m) * unidadValor;
            exp.Step("Base × tarifa del tramo", valor);
        }

        if (concept.Code.Equals(WellKnownConceptCodes.Withholding, StringComparison.OrdinalIgnoreCase)
            && ctx.Parameters.Has(LegalParameterCodes.WithholdingRoundingMultiple))
        {
            var multiplo = ctx.Parameters.Value(LegalParameterCodes.WithholdingRoundingMultiple);
            if (multiplo > 0m)
            {
                var aproximado = Math.Round(valor / multiplo, 0, MidpointRounding.AwayFromZero) * multiplo;
                if (aproximado != valor)
                {
                    exp.Step($"Aproximado al múltiplo de {Fmt.Money(multiplo)} ({LegalParameterCodes.WithholdingRoundingMultiple})", aproximado);
                    valor = aproximado;
                }
            }
        }

        exp.Factor = tarifa;
        exp.Summary = $"{Fmt.Num(enUnidades)} {unidadNombre} → tramo desde {Fmt.Num(tramo.FromValue)}: {Fmt.Money(valor)}";
        return LineFactory.Create(concept, valor, exp, novelty, baseAmount: baseValor, factor: tarifa,
            rangeFrom: tramo.FromValue, rangeTo: tramo.ToValue, legalParameterId: tabla.Id, parameterCode: tabla.Code);
    }
}
