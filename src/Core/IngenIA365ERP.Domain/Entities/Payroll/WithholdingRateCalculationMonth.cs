using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_WithholdingRateCalculationMonths]. Un mes de los sumados en el cálculo
/// del porcentaje del procedimiento 2 (R8): ingreso bruto y aportes obligatorios del mes, si
/// entraron liquidaciones especiales (la prima sí; cesantías e intereses <b>no</b>, ET art.
/// 386) y de qué corridas salió. Inmutable con su cálculo.
/// </summary>
public class WithholdingRateCalculationMonth : AuditableEntity
{
    public int CalculationId { get; set; }

    public short Year { get; set; }
    public byte Month { get; set; }

    public decimal GrossIncome { get; set; }
    public decimal MandatoryContributions { get; set; }

    /// <summary>Se sumó alguna corrida especial del mes (prima); las cesantías y sus intereses nunca entran.</summary>
    public bool IncludedSpecialRuns { get; set; }

    /// <summary>PublicId y versión de cada corrida sumada.</summary>
    public string SourceRunsJson { get; set; } = "[]";

    public WithholdingRateCalculation? Calculation { get; set; }
}
