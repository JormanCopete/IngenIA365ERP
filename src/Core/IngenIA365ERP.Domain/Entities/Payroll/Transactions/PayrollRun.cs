using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Maps to [dbo].[PAY_PayrollRuns]. Una corrida de liquidación. Es una transacción de
/// nómina (Principio XI): nunca se edita ni se borra. Cada cálculo crea una corrida nueva
/// (<see cref="Version"/> + 1) y marca la anterior en borrador como <c>Superseded</c>; la
/// aprobada queda inmutable y sólo se corrige con una reversión que deja asiento reverso
/// con referencia.
///
/// <para>
/// Feature 010 (R2): la corrida tiene <see cref="Kind"/>. Hasta entonces era siempre la de un
/// período; ahora la prima, las cesantías e intereses, las vacaciones y la definitiva son
/// corridas también, en estas mismas tablas, para heredar sin reescribir la relación de pago,
/// los comprobantes, la exportación y la reversión. El período sólo existe en la ordinaria
/// (<see cref="EsCoherente"/>); las otras cuatro llevan <see cref="CutoffDate"/> y, según el
/// tipo, año y semestre o empleado.
/// </para>
/// </summary>
public class PayrollRun : AuditableEntity
{
    /// <summary>Tipo de corrida. Default 0 en la base: toda corrida anterior a la feature 010 queda <c>Ordinary</c>.</summary>
    public PayrollRunKind Kind { get; set; } = PayrollRunKind.Ordinary;

    /// <summary>Sólo en la ordinaria. <c>Kind = Ordinary ⇔ PayPeriodId != null</c>.</summary>
    public int? PayPeriodId { get; set; }
    public int Version { get; set; }

    /// <summary>
    /// Fecha de corte del cálculo de una liquidación especial: 30-06 / 31-12 del semestre en la
    /// prima, 31-12 del año en cesantías, el día anterior al disfrute (o la fecha de la
    /// compensación) en vacaciones, la fecha de retiro en la definitiva. Es la fecha del
    /// comprobante contable (D-04). Obligatoria si <c>Kind ≠ Ordinary</c>.
    /// </summary>
    public DateOnly? CutoffDate { get; set; }

    /// <summary>Fecha de pago propia de la liquidación especial (FR-011a); la ordinaria sigue pagando al fin del período.</summary>
    public DateOnly? PayDate { get; set; }

    /// <summary>Obligatorio en <c>ServiceBonus</c> y <c>Severance</c>.</summary>
    public short? Year { get; set; }

    /// <summary>1 ó 2; obligatorio en <c>ServiceBonus</c>.</summary>
    public byte? Semester { get; set; }

    /// <summary>
    /// Sólo en <c>Vacation</c> y <c>Settlement</c>, corridas de un solo empleado: desnormalizado
    /// para indexar por empleado y corte. El comando exige que coincida con el único
    /// <see cref="PayrollRunEmployee.EmployeeId"/> de <see cref="Employees"/>.
    /// </summary>
    public int? EmployeeId { get; set; }

    /// <summary>Sólo <c>Settlement</c>: todas las versiones de la definitiva apuntan a la misma terminación.</summary>
    public int? TerminationId { get; set; }

    /// <summary>Sólo <c>Vacation</c>: el movimiento que la originó.</summary>
    public int? VacationMovementId { get; set; }
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
    public Accounting.Transactions.AccountingDocument? AccountingDocument { get; set; }

    /// <summary>Comprobante reverso de la reversión (FR-032).</summary>
    public long? ReversalAccountingDocumentId { get; set; }
    public Accounting.Transactions.AccountingDocument? ReversalAccountingDocument { get; set; }

    /// <summary>FR-021: la misma persona registró novedades y aprobó, con la política que lo permite.</summary>
    public bool ApprovedWithoutSegregation { get; set; }

    /// <summary>Excepciones autorizadas al aprobar: empleado, bandera, autorizador, motivo.</summary>
    public string? ExceptionsJson { get; set; }

    public PayPeriod? PayPeriod { get; set; }
    public Employee? Employee { get; set; }
    public EmploymentTermination? Termination { get; set; }
    public VacationMovement? VacationMovement { get; set; }
    public ICollection<PayrollRunEmployee> Employees { get; set; } = [];

    public bool IsEditableDraft => Status is PayrollRunStatus.Draft or PayrollRunStatus.Stale;

    /// <summary>Prima, cesantías, vacaciones o definitiva: todo lo que no es la nómina del período.</summary>
    public bool EsEspecial => Kind != PayrollRunKind.Ordinary;

    /// <summary>
    /// Invariante de la corrida: la ordinaria tiene período y no corte; la especial tiene corte
    /// y no período. Lo comprueban los comandos antes de guardar; el índice único filtrado por
    /// tipo es la red.
    /// </summary>
    public bool EsCoherente =>
        Kind == PayrollRunKind.Ordinary
            ? PayPeriodId is not null
            : PayPeriodId is null && CutoffDate is not null;

    /// <summary>
    /// <c>SourceType</c> del origen contable por tipo (R2). La ordinaria conserva el suyo,
    /// <c>PayrollRun</c>, que es el que ya llevan los comprobantes NM de producción.
    /// </summary>
    public string SourceTypeName => SourceTypeNameDe(Kind);

    public static string SourceTypeNameDe(PayrollRunKind kind) => kind switch
    {
        PayrollRunKind.Ordinary => "PayrollRun",
        PayrollRunKind.ServiceBonus => "ServiceBonusRun",
        PayrollRunKind.Severance => "SeveranceRun",
        PayrollRunKind.Vacation => "VacationRun",
        PayrollRunKind.Settlement => "SettlementRun",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de corrida sin origen contable."),
    };
}
