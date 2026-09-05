using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Cantidad × unidad (FR-009): horas extra y recargos (hora = salario / horas del mes
/// según parámetro, por el factor de recargo), días de incapacidad, licencia o
/// vacaciones (día = salario / 30, por el porcentaje que corresponda). La cantidad es
/// la de la novedad o los días de sus fechas dentro del período; el salario, el vigente
/// en la fecha de la novedad.
/// </summary>
public sealed class QuantityTimesUnitRule : ICalculationRule
{
    public CalculationKind Kind => CalculationKind.QuantityTimesUnit;

    public CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx)
    {
        var unitKind = concept.UnitKind
            ?? throw new CalculationRefusedException($"El concepto {concept.Code} es «cantidad × unidad» y no declara la unidad.", [concept.Code]);

        var exp = new Explanation { Form = "Cantidad × unidad", Novelty = RuleContext.Describe(novelty) };

        // --- cantidad ---
        decimal cantidad;
        if (novelty?.Quantity is { } q)
        {
            cantidad = q;
            exp.Step(unitKind == UnitKind.Day ? "Días registrados en la novedad" : "Cantidad registrada en la novedad", q);
        }
        else if (novelty is { StartDate: not null, EndDate: not null })
        {
            var total = CalendarConventions.Days(novelty.StartDate.Value, novelty.EndDate.Value);
            cantidad = SalaryTranches.DaysWithinPeriod(ctx.Period, novelty.StartDate, novelty.EndDate);
            exp.Step($"Días de la novedad ({Fmt.Date(novelty.StartDate.Value)} a {Fmt.Date(novelty.EndDate.Value)})", total);
            if (cantidad != total)
                exp.Step("Días que caen dentro del período (el resto se traslada al siguiente)", cantidad);
            else
                exp.Step("Días dentro del período", cantidad);
        }
        else if (concept.IsAutomatic && unitKind == UnitKind.Day)
        {
            cantidad = ctx.PaidDays;
            exp.Step("Días pagados del período", cantidad);
        }
        else
        {
            ctx.Skip($"{concept.Code}: la novedad no trae cantidad ni fechas.");
            return null;
        }

        if (concept.MaxQuantity is { } topeCant && cantidad > topeCant)
        {
            exp.Step($"Tope de cantidad del concepto ({Fmt.Num(topeCant)})", topeCant);
            cantidad = topeCant;
        }

        // --- unidad ---
        var fecha = novelty?.StartDate ?? ctx.Period.EndDate;
        var salario = SalaryTranches.SalaryAt(ctx.Tranches, fecha);
        exp.Step($"Salario mensual vigente al {Fmt.Date(fecha)}", salario);

        decimal unidad;
        ExplanationParameter? parametro = null;
        switch (unitKind)
        {
            case UnitKind.Day:
                unidad = salario / CalendarConventions.DaysPerMonth;
                exp.Step($"Valor día: salario / {CalendarConventions.DaysPerMonth}", unidad);
                break;
            case UnitKind.OrdinaryHour:
            case UnitKind.HourWithSurcharge:
                var horas = ctx.Parameters.Value(LegalParameterCodes.HoursPerMonth);
                parametro = ctx.Parameters.Describe(LegalParameterCodes.HoursPerMonth);
                unidad = salario / horas;
                exp.Step($"Valor hora ordinaria: salario / {Fmt.Num(horas)} horas ({parametro.Code}, vigente desde {Fmt.Date(parametro.ValidFrom)})", unidad);
                break;
            default:
                throw new CalculationRefusedException($"Unidad {unitKind} no soportada en {concept.Code}.", [concept.Code]);
        }

        // --- factor y porcentaje ---
        var factor = concept.UnitFactor ?? 1m;
        if (factor != 1m) exp.Step(unitKind == UnitKind.HourWithSurcharge ? $"Factor de recargo ({Fmt.Num(factor)})" : $"Factor ({Fmt.Num(factor)})", factor);

        var pct = ctx.ResolvePercent(concept);
        if (pct is null)
        {
            ctx.Skip($"{concept.Code}: el porcentaje depende de la clase de riesgo ARL y el empleado no tiene una registrada.");
            return null;
        }
        var fraccion = pct.Value.Fraction;
        if (fraccion != 1m)
        {
            exp.Parameter = pct.Value.Parameter ?? exp.Parameter;
            exp.Step(pct.Value.Parameter is { } pp
                ? $"Porcentaje según parámetro {pp.Code} vigente desde {Fmt.Date(pp.ValidFrom)} ({Fmt.Pct(fraccion)})"
                : $"Porcentaje de la definición ({Fmt.Pct(fraccion)})", fraccion * 100m);
        }
        exp.Parameter ??= parametro;

        var valor = cantidad * unidad * factor * fraccion;
        exp.Factor = factor * fraccion;
        exp.Step("Resultado: cantidad × unidad × factor", valor);
        exp.Summary = $"{Fmt.Num(cantidad)} × {Fmt.Money(unidad)}" +
                      (factor != 1m ? $" × {Fmt.Num(factor)}" : string.Empty) +
                      (fraccion != 1m ? $" × {Fmt.Pct(fraccion)}" : string.Empty) +
                      $" = {Fmt.Money(valor)}";

        return LineFactory.Create(concept, valor, exp, novelty, quantity: cantidad, baseAmount: unidad,
            factor: exp.Factor, parameterCode: exp.Parameter?.Code);
    }
}
