using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_WithholdingRateCalculations]. El cálculo del porcentaje fijo del
/// procedimiento 2 (feature 010, R8; ET art. 386): los doce meses anteriores (o los de
/// vinculación) depurados, divididos por <c>RETEFTE_P2_DIVISOR</c> (o los meses), llevados a UVT
/// y a la tabla del art. 383. Versionado como una corrida: recalcular es una versión nueva y la
/// anterior queda <c>Superseded</c>; aprobar <b>cierra</b> la vigencia vigente de
/// <c>PAY_EmployeeWithholdingRates</c> y abre la del semestre con <c>Origin = Calculated</c>.
/// Nada de esto se edita ni se borra.
/// </summary>
public class WithholdingRateCalculation : AuditableEntity
{
    /// <summary>Empleado con <c>WithholdingProcedure = 2</c>.</summary>
    public int EmployeeId { get; set; }

    /// <summary>Semestre al que <b>regirá</b> el porcentaje (julio–diciembre o enero–junio).</summary>
    public short TargetYear { get; set; }
    public byte TargetSemester { get; set; }

    public int Version { get; set; }

    public DateTime CalculatedAt { get; set; }

    [MaxLength(100)]
    public string CalculatedBy { get; set; } = string.Empty;

    /// <summary>12 o los meses de vinculación.</summary>
    public byte MonthsConsidered { get; set; }

    /// <summary><c>RETEFTE_P2_DIVISOR</c> o los meses considerados.</summary>
    public decimal Divisor { get; set; }

    public decimal TotalGrossIncome { get; set; }
    public decimal TotalMandatoryContributions { get; set; }
    public decimal TotalDeclaredDeductions { get; set; }
    public decimal TotalExemptIncome { get; set; }
    public decimal DepuratedBase { get; set; }
    public decimal AverageMonthlyBase { get; set; }
    public decimal UvtValueUsed { get; set; }
    public decimal AverageInUvt { get; set; }
    public decimal TheoreticalWithholding { get; set; }

    /// <summary>Mismo largo que <c>EmployeeWithholdingRate.RatePercent</c> (6,3).</summary>
    public decimal RatePercent { get; set; }

    /// <summary>La política <c>P2SecuenciaDepuracion</c> aplicada.</summary>
    [MaxLength(30)]
    public string DepurationSequence { get; set; } = string.Empty;

    /// <summary>La tabla usada (<c>RETEFTE_TABLA_UVT</c> o la propia del plan).</summary>
    public int TableParameterId { get; set; }

    /// <summary>La tabla fue la del plan de nómina, no la legal.</summary>
    public bool PlanTableUsed { get; set; }

    public WithholdingRateCalculationStatus Status { get; set; } = WithholdingRateCalculationStatus.Calculated;

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    [MaxLength(300)]
    public string? RejectReason { get; set; }

    /// <summary>
    /// La vigencia de <c>PAY_EmployeeWithholdingRates</c> que abrió la aprobación. Sin navegación
    /// a propósito (la vigencia apunta de vuelta con <c>SourceCalculationId</c>).
    /// </summary>
    public int? ResultingRateId { get; set; }

    /// <summary>Pasos, mes a mes.</summary>
    public string ExplanationJson { get; set; } = "{}";

    public Employee? Employee { get; set; }
    public PayrollLegalParameter? TableParameter { get; set; }
    public ICollection<WithholdingRateCalculationMonth> Months { get; set; } = [];
}
