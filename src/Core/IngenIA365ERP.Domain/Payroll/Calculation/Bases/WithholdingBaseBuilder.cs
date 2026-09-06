using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Bases;

/// <summary>
/// Depuración de la base de retención por salarios (FR-039, D-11), todo desde
/// parámetros con vigencia: ingreso gravado − aportes obligatorios del empleado −
/// deducciones declaradas con sus topes (intereses de vivienda, medicina prepagada,
/// dependientes) − rentas exentas por aportes voluntarios con su tope − renta exenta
/// del porcentaje legal con su tope; y el tope global de deducciones más rentas
/// exentas. Los topes mensuales en UVT se proporcionan a los días del período.
/// La explicación muestra cada paso, también cuando no hay nada declarado.
/// </summary>
public static class WithholdingBaseBuilder
{
    public sealed record Depuration(decimal Base, IReadOnlyList<ExplanationStep> Steps, bool HasDeclaredItems);

    private static readonly string[] AportesObligatorios =
    [
        WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.SolidarityFund,
    ];

    public static Depuration Build(RuleContext ctx)
    {
        var pasos = new List<ExplanationStep>();
        var uvt = ctx.Parameters.Value(LegalParameterCodes.Uvt);
        var proporcion = (decimal)ctx.Period.DaysInPeriod / CalendarConventions.DaysPerMonth;

        decimal TopeUvt(string code)
        {
            var tope = ctx.Parameters.Value(code) * uvt * proporcion;
            return tope;
        }

        // 1. Ingreso gravado del período.
        var bruto = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning && ctx.Concepts.Find(l.Code) is { AffectsWithholdingBase: true })
            .Sum(l => l.Amount);
        pasos.Add(new ExplanationStep("Ingreso gravado del período", bruto, null));

        // 2. Aportes obligatorios (ingreso no constitutivo).
        var aportes = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Deduction && AportesObligatorios.Contains(l.Code, StringComparer.OrdinalIgnoreCase))
            .Sum(l => l.Amount);
        pasos.Add(new ExplanationStep("− Aportes obligatorios a salud, pensión y fondo de solidaridad", aportes, null));
        var neto1 = Math.Max(0m, bruto - aportes);
        pasos.Add(new ExplanationStep("Ingreso neto antes de deducciones", neto1, null));

        // 3. Deducciones y rentas exentas declaradas por el empleado.
        var declaradas = ctx.Employee.TaxDeductions;
        var deducciones = 0m;
        var voluntarios = 0m;
        if (declaradas.Count == 0)
        {
            pasos.Add(new ExplanationStep("Deducciones y rentas exentas declaradas", 0m,
                "Ninguna registrada en la ficha: la base se depura sólo con los aportes obligatorios y la renta exenta legal."));
        }
        foreach (var d in declaradas)
        {
            var monto = d.MonthlyAmount ?? (d.Percent is { } pc ? bruto * pc / 100m : 0m);
            switch (d.Kind)
            {
                case TaxDeductionKind.HousingInterest:
                    deducciones += Con(monto, TopeUvt(LegalParameterCodes.WithholdingHousingInterestCapUvt), "Intereses de vivienda", LegalParameterCodes.WithholdingHousingInterestCapUvt, pasos);
                    break;
                case TaxDeductionKind.PrepaidHealth:
                    deducciones += Con(monto, TopeUvt(LegalParameterCodes.WithholdingPrepaidHealthCapUvt), "Medicina prepagada", LegalParameterCodes.WithholdingPrepaidHealthCapUvt, pasos);
                    break;
                case TaxDeductionKind.Dependents:
                    var pctDep = ctx.Parameters.Fraction(LegalParameterCodes.WithholdingDependentsPct);
                    var porPct = bruto * pctDep;
                    var montoDep = d.MonthlyAmount is { } m ? Math.Min(m, porPct) : porPct;
                    pasos.Add(new ExplanationStep($"Dependientes: {Fmt.Pct(pctDep)} del ingreso ({LegalParameterCodes.WithholdingDependentsPct})", porPct, null));
                    deducciones += Con(montoDep, TopeUvt(LegalParameterCodes.WithholdingDependentsCapUvt), "Dependientes", LegalParameterCodes.WithholdingDependentsCapUvt, pasos);
                    break;
                case TaxDeductionKind.VoluntaryPension:
                case TaxDeductionKind.AfcSavings:
                    voluntarios += monto;
                    pasos.Add(new ExplanationStep(d.Kind == TaxDeductionKind.AfcSavings ? "Ahorro AFC declarado" : "Aporte voluntario a pensión declarado", monto, null));
                    break;
            }
        }
        if (voluntarios > 0m)
        {
            var pctVol = ctx.Parameters.Fraction(LegalParameterCodes.WithholdingVoluntarySavingsPct);
            var topeVolPct = bruto * pctVol;
            var topeVolUvt = TopeUvt(LegalParameterCodes.WithholdingVoluntarySavingsCapUvt);
            var topeVol = Math.Min(topeVolPct, topeVolUvt);
            if (voluntarios > topeVol)
            {
                pasos.Add(new ExplanationStep($"Aportes voluntarios limitados al {Fmt.Pct(pctVol)} del ingreso y al tope en UVT", topeVol, null));
                voluntarios = topeVol;
            }
            else
            {
                pasos.Add(new ExplanationStep("Renta exenta por aportes voluntarios", voluntarios, null));
            }
        }

        // 4. Renta exenta legal sobre lo que queda.
        var pctExenta = ctx.Parameters.Fraction(LegalParameterCodes.WithholdingExemptIncomePct);
        var baseExenta = Math.Max(0m, neto1 - deducciones - voluntarios);
        var exenta = baseExenta * pctExenta;
        pasos.Add(new ExplanationStep($"Renta exenta del {Fmt.Pct(pctExenta)} sobre {Fmt.Money(baseExenta)} ({LegalParameterCodes.WithholdingExemptIncomePct})", exenta, null));
        var topeExenta = TopeUvt(LegalParameterCodes.WithholdingExemptIncomeCapUvt);
        if (exenta > topeExenta)
        {
            pasos.Add(new ExplanationStep($"Renta exenta limitada al tope en UVT ({LegalParameterCodes.WithholdingExemptIncomeCapUvt})", topeExenta, null));
            exenta = topeExenta;
        }

        // 5. Tope global: deducciones + rentas exentas.
        var totalDepuracion = deducciones + voluntarios + exenta;
        var pctGlobal = ctx.Parameters.Fraction(LegalParameterCodes.WithholdingDeductionsCapPct);
        var topeGlobal = Math.Min(neto1 * pctGlobal, TopeUvt(LegalParameterCodes.WithholdingDeductionsCapUvt));
        if (totalDepuracion > topeGlobal)
        {
            pasos.Add(new ExplanationStep($"Deducciones y rentas exentas limitadas al {Fmt.Pct(pctGlobal)} del ingreso neto y al tope en UVT", topeGlobal, null));
            totalDepuracion = topeGlobal;
        }
        else
        {
            pasos.Add(new ExplanationStep("Total de deducciones y rentas exentas", totalDepuracion, null));
        }

        var baseDepurada = Math.Max(0m, neto1 - totalDepuracion);
        pasos.Add(new ExplanationStep("Base de retención depurada", baseDepurada, null));

        ctx.Bases.WithholdingBase = baseDepurada;
        return new Depuration(baseDepurada, pasos, declaradas.Count > 0);
    }

    private static decimal Con(decimal monto, decimal tope, string nombre, string codigoTope, List<ExplanationStep> pasos)
    {
        if (monto > tope)
        {
            pasos.Add(new ExplanationStep($"{nombre}: {Fmt.Money(monto)} limitado al tope ({codigoTope})", tope, null));
            return tope;
        }
        pasos.Add(new ExplanationStep(nombre, monto, null));
        return monto;
    }
}
