using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_LegalParameterRanges]. Un tramo de una tabla por rangos:
/// desde/hasta (en la unidad de la tabla: UVT o múltiplos de SMMLV), tarifa marginal
/// y valor fijo del tramo. <see cref="ToValue"/> nulo = sin tope.
/// </summary>
public class PayrollLegalParameterRange : AuditableEntity
{
    public int LegalParameterId { get; set; }
    public decimal FromValue { get; set; }
    public decimal? ToValue { get; set; }
    public decimal? Rate { get; set; }
    public decimal? FixedValue { get; set; }
    public int Order { get; set; }

    public PayrollLegalParameter? LegalParameter { get; set; }

    public bool Contains(decimal value) =>
        value >= FromValue && (ToValue is null || value < ToValue);
}
