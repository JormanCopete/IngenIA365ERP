using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_EmployeeWithholdingRates]. Porcentaje fijo de retención en la
/// fuente del procedimiento 2, con vigencia semestral (FR-039). Lo registra la
/// cooperativa a mano o lo abre la aprobación de un cálculo (feature 010, R8): en ese caso
/// la aprobación <b>cierra</b> la vigencia anterior (<c>ValidTo</c> = día anterior) en vez de
/// reemplazarla, y la nueva sabe de qué cálculo salió.
/// </summary>
public class EmployeeWithholdingRate : AuditableEntity
{
    public int EmployeeId { get; set; }
    public decimal RatePercent { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    /// <summary>Digitada o calculada. Default 0 en la base: las anteriores a la 010 fueron todas manuales.</summary>
    public WithholdingRateOrigin Origin { get; set; } = WithholdingRateOrigin.Manual;

    /// <summary>
    /// El cálculo (<c>PAY_WithholdingRateCalculations</c>) que abrió esta vigencia, si fue
    /// calculada. Sin navegación a propósito: el cálculo apunta de vuelta con
    /// <c>ResultingRateId</c> y dos navegaciones cruzadas las emparejaría la convención de EF
    /// como una relación uno a uno con clave ambigua.
    /// </summary>
    public int? SourceCalculationId { get; set; }

    public Employee? Employee { get; set; }

    public bool IsValidAt(DateTime date) =>
        ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
