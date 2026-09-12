using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Maps to [dbo].[PAY_PayrollRuns]. Una corrida de liquidación de un período. Es una
/// transacción de nómina (Principio XI): nunca se edita ni se borra. Cada cálculo crea
/// una corrida nueva (<see cref="Version"/> + 1) y marca la anterior en borrador como
/// <c>Superseded</c>; la aprobada queda inmutable y sólo se corrige con una reversión
/// que deja asiento reverso con referencia.
/// </summary>
public class PayrollRun : AuditableEntity
{
    public int PayPeriodId { get; set; }
    public int Version { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    public DateTime CalculatedAt { get; set; }

    [MaxLength(100)]
    public string CalculatedBy { get; set; } = string.Empty;

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    public DateTime? ReversedAt { get; set; }

    [MaxLength(100)]
    public string? ReversedBy { get; set; }

    [MaxLength(300)]
    public string? ReversalReason { get; set; }

    /// <summary>
    /// Borrador descartado (feature 006): la corrida queda <c>Superseded</c> sin que exista
    /// una versión nueva, el período vuelve a Abierto y aquí queda quién, cuándo y por qué.
    /// Nunca se borra (Principio XI).
    /// </summary>
    public DateTime? DiscardedAt { get; set; }
    public string? DiscardedBy { get; set; }
    public string? DiscardReason { get; set; }

    /// <summary>SHA-256 de los insumos normalizados: mismo hash, mismo resultado (FR-014).</summary>
    [MaxLength(64)]
    public string InputsHash { get; set; } = string.Empty;

    public int EmployeeCount { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public decimal TotalProvisions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal RoundingAdjustment { get; set; }

    /// <summary>Comprobante NM de la aprobación. La navegación existe para que EF fije la clave en el mismo SaveChanges de la aprobación (FR-023).</summary>
    public long? AccountingDocumentId { get; set; }
    public Accounting.AccountingDocument? AccountingDocument { get; set; }

    /// <summary>Comprobante reverso de la reversión (FR-032).</summary>
    public long? ReversalAccountingDocumentId { get; set; }
    public Accounting.AccountingDocument? ReversalAccountingDocument { get; set; }

    /// <summary>FR-021: la misma persona registró novedades y aprobó, con la política que lo permite.</summary>
    public bool ApprovedWithoutSegregation { get; set; }

    /// <summary>Excepciones autorizadas al aprobar: empleado, bandera, autorizador, motivo.</summary>
    public string? ExceptionsJson { get; set; }

    public PayPeriod? PayPeriod { get; set; }
    public ICollection<PayrollRunEmployee> Employees { get; set; } = [];

    public bool IsEditableDraft => Status is PayrollRunStatus.Draft or PayrollRunStatus.Stale;
}
