using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_EmploymentTerminations]. La verdad del retiro de un empleado (feature 010,
/// R7). <c>Employee.TerminationDate</c>, <c>TerminationCause</c> y <c>Status = -1</c> se conservan
/// como hoy, pero los escribe únicamente la aprobación de la definitiva a partir de esta fila.
///
/// <para>
/// Registrar crea la terminación y la corrida <c>Settlement</c> en borrador en la misma acción.
/// <c>Registered → Settled</c> al aprobar la definitiva (misma transacción: ficha cerrada,
/// pagos en Cartera, <c>VacationMovement.SettlementPayout</c>); <c>Settled → Reinstated</c> al
/// reversarla (ficha vigente de nuevo); <c>Registered → Cancelled</c> al descartar el borrador.
/// Una sola terminación viva por ficha (dos índices únicos filtrados, <c>Status = 0</c> y
/// <c>= 1</c>); un <c>Reinstated</c> puede volver a terminarse con una fila nueva.
/// </para>
/// </summary>
public class EmploymentTermination : AuditableEntity
{
    public int EmployeeId { get; set; }

    /// <summary>FR-021: debe caer en un período <c>Open</c> del plan del empleado.</summary>
    public DateOnly TerminationDate { get; set; }

    public int TerminationReasonId { get; set; }

    /// <summary>Copiado de la ficha al registrar, editable aquí.</summary>
    public DianContractType? ContractTypeAtTermination { get; set; }

    /// <summary>Término fijo / obra: para «el tiempo que faltaba» de la indemnización.</summary>
    public DateOnly? ContractEndDate { get; set; }

    public TerminationStatus Status { get; set; } = TerminationStatus.Registered;

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>PDF para firma generado al aprobar (<c>COR_Attachments</c>, <c>OwnerEntityType = "EmploymentTermination"</c>), inmutable.</summary>
    public Guid? SettlementDocumentAttachmentPublicId { get; set; }

    /// <summary>Copia firmada que sube la responsable (opcional).</summary>
    public Guid? SignedDocumentAttachmentPublicId { get; set; }

    public DateTime? ReinstatedAt { get; set; }

    [MaxLength(100)]
    public string? ReinstatedBy { get; set; }

    [MaxLength(300)]
    public string? ReinstateReason { get; set; }

    public Employee? Employee { get; set; }
    public TerminationReason? TerminationReason { get; set; }
    public ICollection<SettlementDeduction> Deductions { get; set; } = [];

    public bool EstaViva => Status is TerminationStatus.Registered or TerminationStatus.Settled;
}
