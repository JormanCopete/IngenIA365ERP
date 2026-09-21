using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_Novelties]. Un hecho de un período para un empleado y un
/// concepto (FR-001..006). Nunca se borra: corregir crea una novedad nueva que apunta
/// a la anterior (<see cref="SupersedesNoveltyId"/>) y la marca <c>Superseded</c>;
/// anular la marca <c>Cancelled</c>. Sustituye a <see cref="PayrollEntry"/> (legado),
/// que queda de sólo consulta.
/// </summary>
public class PayrollNovelty : AuditableEntity
{
    public int PayPeriodId { get; set; }
    public int EmployeeId { get; set; }

    /// <summary>Versión de la definición vigente al registrarla.</summary>
    public int ConceptDefinitionId { get; set; }

    /// <summary>Denormalizado para la regla de repetición y la lectura.</summary>
    [MaxLength(30)]
    public string ConceptCode { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Días que caen dentro del período (FR-003).</summary>
    public int DaysInPeriod { get; set; }

    /// <summary>Días que quedan para períodos siguientes (FR-003).</summary>
    public int CarryOverDays { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public NoveltyStatus Status { get; set; } = NoveltyStatus.Active;

    [MaxLength(300)]
    public string? StatusReason { get; set; }

    public NoveltyOrigin Origin { get; set; } = NoveltyOrigin.Manual;

    public int? SupersedesNoveltyId { get; set; }
    public int? RecurringNoveltyId { get; set; }
    public Guid? ImportBatchId { get; set; }
    public int? RetroactiveOfPeriodId { get; set; }
    public int? LoanPortfolioId { get; set; }
    public int? CarriedFromNoveltyId { get; set; }

    /// <summary>Feature 010 (R6): el movimiento de vacaciones que generó esta novedad (<see cref="NoveltyOrigin.VacationLeave"/>).</summary>
    public int? VacationMovementId { get; set; }

    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }

    public PayPeriod? PayPeriod { get; set; }
    public Employee? Employee { get; set; }
    public PayrollConceptDefinition? ConceptDefinition { get; set; }
}
