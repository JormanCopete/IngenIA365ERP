using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;

/// <summary>
/// <c>TES_Reconciliations</c> (feature 015, US25, T0276; FR-131, FR-216, FR-217; data-model §12.2): la conciliación de una cuenta bancaria propia en un mes. Es la única
/// conciliación bancaria de la cooperativa (E-04 a la 009). Reabrir una cerrada no la cambia: crea una <see cref="Version"/> nueva y la anterior queda
/// <see cref="ReconciliationState.Reopened"/>; reabrir el período contable la deja <see cref="ReconciliationState.Outdated"/> por <c>IConciliacionBancariaParaContabilidad</c>.
/// Los saldos y la diferencia se recalculan con cada cruce o partida mientras está abierta; cerrar exige diferencia cero. (nuevo)
/// </summary>
public class Reconciliation : AuditableEntity
{
    public int BankAccountId { get; set; }

    public int PeriodYear { get; set; }

    public int PeriodMonth { get; set; }

    public int Version { get; set; } = 1;

    public ReconciliationKind Kind { get; set; } = ReconciliationKind.Minimal;

    /// <summary>Quien la abrió y la trabaja (<c>SEC_Users.Id</c>); la segregación lo compara con quienes prepararon o aprobaron pagos de la cuenta en el mes.</summary>
    public int ConciliatorUserId { get; set; }

    /// <summary>El saldo del extracto al último día del mes.</summary>
    public decimal StatementBalance { get; set; }

    /// <summary>El saldo en libros de la cuenta contable vinculada al último día del mes (E-04).</summary>
    public decimal LedgerBalance { get; set; }

    /// <summary>La suma con signo de las partidas conciliatorias (las que explican la diferencia).</summary>
    public decimal ReconcilingItemsTotal { get; set; }

    /// <summary><c>StatementBalance − LedgerBalance + ReconcilingItemsTotal</c>: cerrar exige cero (FR-216).</summary>
    public decimal Difference { get; set; }

    public ReconciliationState Status { get; set; } = ReconciliationState.Open;

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    /// <summary>El informe en PDF de la conciliación cerrada (adjunto <c>TreasuryReconciliationReport</c>, no borrable).</summary>
    public Guid? ReportAttachmentPublicId { get; set; }

    public string? ReopenReason { get; set; }

    public int? ReopenedByUserId { get; set; }

    public DateTime? ReopenedAt { get; set; }

    /// <summary>La versión de la que salió al reabrir (nula en la primera).</summary>
    public int? PreviousReconciliationId { get; set; }

    public DateTime? OutdatedAt { get; set; }

    public string? OutdatedReason { get; set; }
}

/// <summary>
/// <c>TES_ReconciliationMatches</c> (H; data-model §12.3): una línea del extracto cruzada con un registro de los libros. Un cruce de uno a varios es una fila por registro
/// con la misma <see cref="StatementLineId"/> y el mismo <see cref="GroupKey"/>. Deshacer un cruce de una conciliación abierta lo deja <see cref="IsLive"/> falso, nunca lo
/// borra; un registro y una línea se cruzan a lo sumo una vez en una conciliación (UK filtrados a lo vivo). (nuevo)
/// </summary>
public class ReconciliationMatch : AuditableEntityLong, IHechoInmutable
{
    public int ReconciliationId { get; set; }

    public long StatementLineId { get; set; }

    public ReconciliationRecordKind RecordKind { get; set; }

    public Guid RecordPublicId { get; set; }

    public string? RecordNumber { get; set; }

    public decimal Amount { get; set; }

    public ReconciliationMatchMode Mode { get; set; }

    /// <summary>La regla del motor (<c>ValueDateReference</c>, <c>StatementLink</c>, <c>ValueDateReferenceGroup</c>) o <c>Manual</c>.</summary>
    public string Rule { get; set; } = string.Empty;

    /// <summary>Las filas del mismo cruce (uno a varios) comparten la clave.</summary>
    public Guid GroupKey { get; set; }

    public int MatchedByUserId { get; set; }

    public DateTime MatchedAt { get; set; }

    [ColumnaMutable] public bool IsLive { get; set; } = true;

    [ColumnaMutable] public int? UndoneByUserId { get; set; }

    [ColumnaMutable] public DateTime? UndoneAt { get; set; }
}

/// <summary>
/// <c>TES_ReconcilingItems</c> (data-model §12.4): una partida conciliatoria de la conciliación, con su clase, valor y antigüedad (<see cref="Since"/>). El abono sin registro
/// enviado a Recaudo como partida por identificar guarda su <see cref="UnidentifiedItemPublicId"/>; el cargo no identificado, la alerta. (nuevo)
/// </summary>
public class ReconcilingItem : AuditableEntity
{
    public int ReconciliationId { get; set; }

    public ReconcilingItemKind Kind { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Since { get; set; }

    /// <summary>El registro o la línea de donde sale (el cheque, la remesa, la línea del extracto).</summary>
    public Guid? SourcePublicId { get; set; }

    public long? StatementLineId { get; set; }

    public Guid? UnidentifiedItemPublicId { get; set; }

    public string? Note { get; set; }

    public Guid? AlertPublicId { get; set; }

    public int CreatedByUserId { get; set; }

    /// <summary>La partida se quitó de una conciliación abierta (no se borra).</summary>
    public bool IsVoided { get; set; }
}
