using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_EmployeeWithholdingRates]. Porcentaje fijo de retención en la
/// fuente del procedimiento 2, con vigencia semestral (FR-039). Lo registra la
/// cooperativa; calcularlo con los doce meses anteriores queda para otra feature.
/// </summary>
public class EmployeeWithholdingRate : AuditableEntity
{
    public int EmployeeId { get; set; }
    public decimal RatePercent { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    public Employee? Employee { get; set; }

    public bool IsValidAt(DateTime date) =>
        ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
