using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Período mensual de un ejercicio (feature 009, FR-020..FR-022). La fecha de todo comprobante
/// cae en un período abierto; cerrar exige que no queden borradores; reabrir exige permiso y
/// motivo y marca desactualizadas las conciliaciones cerradas del mes.
/// </summary>
public class AccountingPeriod : AuditableEntity
{
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }

    public byte Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PeriodStatus Status { get; set; } = PeriodStatus.Open;

    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }

    public bool Contiene(DateOnly fecha) => fecha >= StartDate && fecha <= EndDate;
}
