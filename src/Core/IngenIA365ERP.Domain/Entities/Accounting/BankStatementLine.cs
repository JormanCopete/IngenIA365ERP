using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Una línea del extracto del banco (feature 009, FR-056..FR-059). <see cref="Fingerprint"/>
/// (fecha, referencia, descripción, valor y número de ocurrencia) detecta un archivo cargado dos
/// veces sin impedir dos movimientos idénticos legítimos. Se empareja con a lo sumo una línea
/// contable; desde una partida sin pareja se abre un borrador de comprobante.
/// </summary>
public class BankStatementLine : AuditableEntityLong
{
    public int ReconciliationId { get; set; }
    public BankReconciliation? Reconciliation { get; set; }

    public int LineNumber { get; set; }
    public DateOnly Date { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }

    /// <summary>Con signo: positivo entra al banco, negativo sale.</summary>
    public decimal Amount { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public MatchKind? MatchKind { get; set; }
    public DateTime? MatchedAt { get; set; }
    public string? MatchedBy { get; set; }

    public long? DraftDocumentId { get; set; }
    public AccountingDocument? DraftDocument { get; set; }
}
