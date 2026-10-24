using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Statements;

/// <summary>
/// <c>TES_BankStatements</c> (feature 015, US6, T0191; R-06; FR-065, FR-196, FR-197; data-model §12.1): una carga del extracto de una cuenta bancaria propia, con el
/// formato del banco (ámbito <c>BankStatement = 7</c>), la plantilla genérica o a mano. Es <b>una sola</b> carga para Tesorería y Recaudo: Recaudo identifica sus abonos
/// por el puerto. El archivo se guarda como adjunto no borrable (<c>TreasuryStatement</c>) y se reconoce por su huella (<see cref="FileSha256"/>, UK filtrado). No
/// cambia después de cargarse. (nuevo)
/// </summary>
public class BankStatement : AuditableEntity, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public int BankAccountId { get; set; }

    public BankStatementSource Source { get; set; }

    public int? BankFileFormatId { get; set; }

    public Guid? FileAttachmentPublicId { get; set; }

    public string? FileName { get; set; }

    /// <summary>La huella del archivo entero; nula en la captura manual.</summary>
    public string? FileSha256 { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    /// <summary>Las líneas cargadas.</summary>
    public int Lines { get; set; }

    /// <summary>Las omitidas por huella (ya estaban cargadas, US6-1).</summary>
    public int Omitted { get; set; }

    /// <summary>Las que no se pudieron leer y no se cargaron (FR-196).</summary>
    public int Illegible { get; set; }

    /// <summary>Las cargadas como posibles duplicados (sin identificador del banco).</summary>
    public int PossibleDuplicates { get; set; }
}

/// <summary>
/// <c>TES_BankStatementLines</c> (H, <c>AuditableEntityLong</c>; data-model §12.1): un movimiento del extracto con su huella por formato (FR-197). Con identificador del
/// banco la huella es única en la cuenta (UK filtrado <see cref="HasBankIdentifier"/>); sin él, la repetida queda <see cref="PossibleDuplicateOfLineId"/> con
/// <see cref="DuplicateDecision"/> para que una persona decida. Lo que Recaudo identificó (<see cref="RecaudoStatus"/>, <see cref="RecaudoIdentifiedAmount"/>) y el estado
/// en la conciliación cambian; lo demás no. (nuevo)
/// </summary>
public class BankStatementLine : AuditableEntityLong, IHechoInmutable
{
    public int BankStatementId { get; set; }

    public int BankAccountId { get; set; }

    public int LineNumber { get; set; }

    public DateOnly MovementDate { get; set; }

    public DateOnly? ValueDate { get; set; }

    public StatementLineDirection Direction { get; set; }

    /// <summary>El valor sin signo (el sentido va en <see cref="Direction"/>).</summary>
    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? BankSequence { get; set; }

    public string? DepositorTaxId { get; set; }

    public string? DepositorName { get; set; }

    public string? Description { get; set; }

    public decimal? Balance { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>La huella sale del identificador del banco: única en la cuenta. (nuevo)</summary>
    public bool HasBankIdentifier { get; set; }

    public long? PossibleDuplicateOfLineId { get; set; }

    [ColumnaMutable] public StatementLineDuplicateDecision? DuplicateDecision { get; set; }

    [ColumnaMutable] public StatementLineRecaudoStatus RecaudoStatus { get; set; } = StatementLineRecaudoStatus.NotApplicable;

    /// <summary>Lo identificado por Recaudo hasta hoy (nuevo).</summary>
    [ColumnaMutable] public decimal RecaudoIdentifiedAmount { get; set; }

    [ColumnaMutable] public StatementLineReconciliationStatus ReconciliationStatus { get; set; } = StatementLineReconciliationStatus.Open;
}
