using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Ejercicio contable (año calendario) con sus doce períodos (feature 009, FR-021..FR-023).
/// Se cierra con los doce meses cerrados y el anterior cerrado; el cierre genera el comprobante
/// <see cref="ClosingDocument"/> y reabrir lo reversa.
/// </summary>
public class FiscalYear : AuditableEntity
{
    public int Year { get; set; }
    public PeriodStatus Status { get; set; } = PeriodStatus.Open;

    public long? ClosingDocumentId { get; set; }
    public AccountingDocument? ClosingDocument { get; set; }

    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }

    public ICollection<AccountingPeriod> Periods { get; set; } = [];

    public DateOnly StartDate => new(Year, 1, 1);
    public DateOnly EndDate => new(Year, 12, 31);
}
