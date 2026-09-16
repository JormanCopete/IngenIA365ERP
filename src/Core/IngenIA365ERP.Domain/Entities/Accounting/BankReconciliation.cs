using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Conciliación de una cuenta bancaria en un período (feature 009, FR-055..FR-060): saldos del
/// extracto, saldo en libros al cerrar, estado. Reabrir el período contable la marca
/// <c>Outdated</c>; reabrirla exige permiso y motivo.
/// </summary>
public class BankReconciliation : AuditableEntity
{
    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }
    public int PeriodId { get; set; }
    public AccountingPeriod? Period { get; set; }

    public decimal StatementOpeningBalance { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public decimal? BookBalanceAtClose { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Open;

    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }

    public ICollection<BankStatementLine> StatementLines { get; set; } = [];
}
