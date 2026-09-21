using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements.Rules;

/// <summary>
/// Deducciones de ley del empleado sobre lo salarial de la liquidación (Ley 100/1993
/// arts. 18, 20 y 204; Ley 797/2003): salud y pensión como porcentaje del IBC y fondo de
/// solidaridad por tabla, con las definiciones de concepto de la nómina ordinaria
/// (<c>SALUD_EMP</c>, <c>PENSION_EMP</c>, <c>FSP</c>: porcentaje o tabla por parámetro,
/// clases a las que aplica). El IBC es la suma de las líneas cuyo concepto «entra al IBC»
/// (salario pendiente y vacaciones compensadas, data-model §1.7), al porcentaje del
/// salario integral y con el tope en SMMLV de un mes. La prima, las cesantías, los
/// intereses y la indemnización no cotizan y por eso no marcan esa base.
/// </summary>
public static class LegalDeductionsRule
{
    public static void Evaluate(SettlementContext ctx)
    {
        var devengos = ctx.Lines.Where(l => l.Nature == ConceptNature.Earning).ToList();
        var ibc = devengos.Where(l => ctx.Concepts.Find(l.Code) is { AffectsContributionBase: true }).Sum(l => l.Amount);
        var pasos = new List<ExplanationStep> { new("Devengos de esta liquidación que forman el IBC", ibc, null) };

        if (ibc <= 0m)
        {
            pasos.Add(new ExplanationStep("Aportes del empleado", 0m, "Ningún devengo de esta liquidación cotiza a seguridad social."));
            ctx.BaseSteps.AddRange(pasos);
            return;
        }

        if (ctx.Employee.Class == EmployeeClass.IntegralSalary)
        {
            var pct = ctx.Parameters.Fraction(LegalParameterCodes.IntegralSalaryBasePct);
            var p = ctx.Parameters.Describe(LegalParameterCodes.IntegralSalaryBasePct);
            ibc *= pct;
            pasos.Add(new ExplanationStep($"Salario integral: IBC al {Fmt.Pct(pct)} ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})", ibc, null));
        }
        var smmlv = ctx.Parameters.Value(LegalParameterCodes.Smmlv);
        var topeSmmlv = ctx.Parameters.Value(LegalParameterCodes.ContributionBaseCapSmmlv);
        var tope = topeSmmlv * smmlv;
        if (ibc > tope)
        {
            pasos.Add(new ExplanationStep($"Tope del IBC: {Fmt.Num(topeSmmlv)} SMMLV × {Fmt.Money(smmlv)}", tope, null));
            ibc = tope;
        }
        pasos.Add(new ExplanationStep("Base de aportes (IBC) de la liquidación", ibc, null));
        ctx.BaseSteps.AddRange(pasos);

        Porcentaje(ctx, WellKnownConceptCodes.HealthEmployee, ibc, ctx.Employee.Affiliations.Health, "salud");
        Porcentaje(ctx, WellKnownConceptCodes.PensionEmployee, ibc, ctx.Employee.Affiliations.Pension, "pensión");
        Tabla(ctx, WellKnownConceptCodes.SolidarityFund, ibc, ctx.Employee.Affiliations.Pension);
    }

    private static void Porcentaje(SettlementContext ctx, string code, decimal ibc, bool afiliado, string nombre)
    {
        var def = ctx.Concepts.Find(code);
        if (def is null || !def.AppliesTo(ctx.Employee.Class))
        {
            ctx.Skip(code, SettlementReasonCodes.NoCotizaSobreEstaLiquidacion, $"{code}: no aplica a la clase {ctx.Employee.Class} o no tiene versión vigente.");
            return;
        }
        if (!afiliado)
        {
            ctx.Flags |= SettlementFlags.MissingAffiliation;
            ctx.Refuse($"Sin afiliación a {nombre} registrada en la ficha: el aporte no tiene destinatario.");
        }
        var pct = Fraccion(ctx, def);
        if (pct is null) return;
        var (fraccion, parametro) = pct.Value;
        var valor = ibc * fraccion;
        var exp = new Explanation
        {
            Form = "Base × porcentaje",
            Base = new ExplanationBase("Base de aportes (IBC)", ibc),
            Factor = fraccion,
            Parameter = parametro,
        };
        exp.Step("Base de aportes (IBC)", ibc);
        exp.Step(parametro is null ? $"Porcentaje de la definición ({Fmt.Pct(fraccion)})" : $"Porcentaje legal {Fmt.Pct(fraccion)} ({parametro.Code}, vigente desde {Fmt.Date(parametro.ValidFrom)})", fraccion * 100m);
        exp.Step("Base × porcentaje", valor);
        exp.Summary = $"{Fmt.Money(ibc)} × {Fmt.Pct(fraccion)} = {Fmt.Money(valor)}";
        ctx.Add(LineFactory.Create(def, valor, exp, baseAmount: ibc, factor: fraccion, parameterCode: parametro?.Code));
    }

    private static void Tabla(SettlementContext ctx, string code, decimal ibc, bool afiliado)
    {
        var def = ctx.Concepts.Find(code);
        if (def is null || !def.AppliesTo(ctx.Employee.Class) || !afiliado) return;
        var tablaCode = def.TableParameterCode ?? LegalParameterCodes.SolidarityFundTable;
        var tabla = ctx.Parameters.Table(tablaCode);
        var busqueda = RangeTableLookup.Find(tabla, ibc, ctx.Parameters);
        var exp = new Explanation
        {
            Form = "Tabla por rangos",
            Base = new ExplanationBase("Base de aportes (IBC)", ibc),
            Parameter = new ExplanationParameter(tabla.Code, tabla.ValidFrom, null),
        };
        exp.Step("Base de aportes (IBC)", ibc);
        RangeTableLookup.Explain(busqueda, exp);
        exp.Factor = busqueda.Rate;
        exp.Summary = busqueda.Found
            ? $"{Fmt.Num(busqueda.BaseInUnits)} {busqueda.UnitName} → tramo desde {Fmt.Num(busqueda.Range!.FromValue)}: {Fmt.Money(busqueda.Value)}"
            : $"{def.Name}: no aplica";
        ctx.Add(LineFactory.Create(def, busqueda.Value, exp, baseAmount: ibc, factor: busqueda.Rate,
            rangeFrom: busqueda.Range?.FromValue, rangeTo: busqueda.Range?.ToValue, legalParameterId: tabla.Id, parameterCode: tabla.Code));
    }

    private static (decimal Fraction, ExplanationParameter? Parameter)? Fraccion(SettlementContext ctx, PayrollConceptDefinition concept)
    {
        if (!string.IsNullOrWhiteSpace(concept.PercentParameterCode))
            return (ctx.Parameters.Fraction(concept.PercentParameterCode), ctx.Parameters.Describe(concept.PercentParameterCode));
        if (concept.Percent is { } pct) return (pct / 100m, null);
        return null;
    }
}
