using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Integration;

/// <summary>
/// <c>LND_ProcessRuns</c> (feature 013, C.3; data-model §22.1; FR-062–FR-067, FR-107, SC-007, SC-020; T5, T6, T28, T47): una
/// corrida de <c>ProcesadorDeCartera</c> (ciclo diario, cierre mensual, reapertura, corrección por valor nacional). No es hecho:
/// avanza <c>Requested</c> → <c>Running</c> → <c>Completed</c> | <c>CompletedWithErrors</c> | <c>Failed</c> y se reintenta. Una sola
/// corriendo por cooperativa (<c>UK_LND_ProcessRuns_Running</c>). Forma sin comportamiento.
/// </summary>
public class LendingProcessRun : AuditableEntity
{
    public LendingProcessKind Kind { get; set; }

    /// <summary><c>DailyCycle</c>: el día d (causa (d−1, d]).</summary>
    public DateOnly? ProcessDate { get; set; }

    public short? PeriodYear { get; set; }

    public byte? PeriodMonth { get; set; }

    /// <summary><c>MonthlyClose</c>: la versión que produce; <c>CloseReopen</c>: la que reabre.</summary>
    public int? CloseVersion { get; set; }

    /// <summary><c>NationalValueCorrection</c>: la réplica que trajo la corrección (T28).</summary>
    public int? NationalValueReplicationId { get; set; }

    public DateOnly? AffectedFrom { get; set; }

    public DateOnly? AffectedTo { get; set; }

    public LendingProcessRunStatus Status { get; set; } = LendingProcessRunStatus.Requested;

    /// <summary>Paso del ciclo (1 réplica … 8 verificación); en el cierre, 1 líneas, 2 transacción final.</summary>
    public byte? CurrentStep { get; set; }

    /// <summary><c>Process</c> para lo programado; <c>Person</c> para una orden manual (T8).</summary>
    public ActorKind RequestedByKind { get; set; }

    public int? RequestedByUserId { get; set; }

    public Guid? RequestedByCentralUserId { get; set; }

    public string? RequestedByName { get; set; }

    public string? RequestedByIp { get; set; }

    /// <summary><c>Tarea:cartera.diario:{fecha}</c>, <c>Tarea:cartera.cierre-programado</c>, <c>Orden:{PublicId}</c>, <c>Replica:{PublicId}</c>.</summary>
    public string ExecutionOrigin { get; set; } = string.Empty;

    /// <summary>Obligatorio en <c>CloseReopen</c> y en cada reintento manual.</summary>
    public string? Reason { get; set; }

    /// <summary>R sellado: en el cierre, el de T47; en el ciclo, el inicio de la corrida.</summary>
    public DateTime? InformationCutoffAt { get; set; }

    /// <summary><c>Sequence</c> de la última réplica de valores nacionales usada (T39).</summary>
    public long? NationalValuesWatermark { get; set; }

    /// <summary>Último <c>LoanId</c> procesado en orden (sin FK): la corrida retoma desde aquí.</summary>
    public int? CursorLoanId { get; set; }

    public int TotalLoans { get; set; }

    public int ProcessedLoans { get; set; }

    public int FailedLoans { get; set; }

    /// <summary>Sin valor nacional o ya al día por una operación.</summary>
    public int SkippedLoans { get; set; }

    public int BatchCount { get; set; }

    public int PartCount { get; set; }

    public int RetryCount { get; set; }

    /// <summary>JSON con los totales por tipo de hecho, concepto y sucursal: el evento resumen de la corrida (T67).</summary>
    public string? TotalsJson { get; set; }

    public string? ResultSha256 { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public DateTime? LastHeartbeatAt { get; set; }
}
