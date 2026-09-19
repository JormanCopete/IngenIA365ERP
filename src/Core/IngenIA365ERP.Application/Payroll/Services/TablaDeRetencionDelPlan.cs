using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// La tabla de retención propia del plan de nómina (<c>/nomina/parametros-retencion</c>,
/// <c>PAY_WithholdingParameters</c>) entra a la liquidación por aquí (pedido del dueño, 2026-09-19):
/// si el plan del período tiene tramos, reemplazan a los de <c>RETEFTE_TABLA_UVT</c> de
/// Parámetros legales <b>sólo para esa corrida</b>; si no tiene, la tabla legal sigue tal cual.
/// El motor no cambia: recibe un parámetro con el mismo código y otros tramos, y la explicación
/// de la línea dice de dónde salieron (<see cref="PayrollLegalParameter.Source"/>).
/// </summary>
public static class TablaDeRetencionDelPlan
{
    /// <summary>Los parámetros con la tabla del plan en lugar de la legal, o los mismos si el plan no trae tramos.</summary>
    public static IReadOnlyList<PayrollLegalParameter> Aplicar(
        IReadOnlyList<PayrollLegalParameter> parametros, IReadOnlyList<WithholdingParameter> tramosDelPlan, PayrollPlan plan, DateTime asOf)
    {
        var tramos = tramosDelPlan.Where(t => !t.IsDeleted && t.PayrollPlanId == plan.Id).OrderBy(t => t.UvtRangeStart).ToList();
        if (tramos.Count == 0) return parametros;

        var legal = parametros
            .Where(p => !p.IsDeleted && p.Code.Equals(LegalParameterCodes.WithholdingTableUvt, StringComparison.OrdinalIgnoreCase) && p.IsValidAt(asOf.Date))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();

        var propia = new PayrollLegalParameter
        {
            Id = legal?.Id ?? 0,
            PublicId = legal?.PublicId ?? Guid.Empty,
            Code = LegalParameterCodes.WithholdingTableUvt,
            Name = $"Retención en la fuente por salarios: tabla del plan {plan.Code}",
            Kind = LegalParameterKind.RangeTable,
            ValidFrom = legal?.ValidFrom ?? asOf.Date,
            ValidTo = legal?.ValidTo,
            Source = $"Parámetros de retención del plan {plan.Code} ({plan.Name}); reemplaza la tabla legal para este plan",
            RangeUnitParameterCode = legal?.RangeUnitParameterCode ?? LegalParameterCodes.Uvt,
            RangeIsMarginal = true,
            CreatedBy = legal?.CreatedBy ?? "plan",
        };
        var orden = 0;
        foreach (var t in tramos)
        {
            propia.Ranges.Add(new PayrollLegalParameterRange
            {
                LegalParameter = propia,
                FromValue = t.UvtRangeStart,
                ToValue = t.UvtRangeEndOrNull,
                Rate = t.Rate,
                FixedValue = t.AdditionalUvt,
                Order = orden++,
            });
        }

        // Fuera todas las vigencias de la tabla legal (el ParameterSet elige la más reciente por código): manda la del plan.
        return parametros
            .Where(p => !p.Code.Equals(LegalParameterCodes.WithholdingTableUvt, StringComparison.OrdinalIgnoreCase))
            .Append(propia)
            .ToList();
    }
}
