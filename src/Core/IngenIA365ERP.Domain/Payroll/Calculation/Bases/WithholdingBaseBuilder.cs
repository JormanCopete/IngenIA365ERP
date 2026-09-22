using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Bases;

/// <summary>
/// Depuración de la base de retención por salarios del período (FR-039, D-11): toma de
/// las líneas ya calculadas el ingreso gravado y los aportes obligatorios del empleado y
/// se los entrega a <see cref="DepuracionDeRetencion"/>, que aplica la norma (deducciones
/// declaradas con sus topes, aportes voluntarios, renta exenta del porcentaje legal y el
/// tope global). Los topes mensuales en UVT se proporcionan a los días del período; la
/// nómina ordinaria observa los cupos anuales en modo mensualizado, que es lo que hizo
/// siempre (research R5). La explicación muestra cada paso, también cuando no hay nada
/// declarado.
/// </summary>
public static class WithholdingBaseBuilder
{
    private static readonly string[] AportesObligatorios =
    [
        WellKnownConceptCodes.HealthEmployee, WellKnownConceptCodes.PensionEmployee, WellKnownConceptCodes.SolidarityFund,
    ];

    public static Depuration Build(RuleContext ctx)
    {
        var proporcion = (decimal)ctx.Period.DaysInPeriod / CalendarConventions.DaysPerMonth;

        var bruto = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Earning && ctx.Concepts.Find(l.Code) is { AffectsWithholdingBase: true })
            .Sum(l => l.Amount);

        var aportes = ctx.Lines
            .Where(l => l.Nature == ConceptNature.Deduction && AportesObligatorios.Contains(l.Code, StringComparer.OrdinalIgnoreCase))
            .Sum(l => l.Amount);

        var depuracion = DepuracionDeRetencion.Depurar(bruto, aportes, ctx.Employee.TaxDeductions, ctx.Parameters, proporcion);
        ctx.Bases.WithholdingBase = depuracion.Base;
        return depuracion;
    }
}
