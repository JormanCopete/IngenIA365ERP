using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Presupuesto de un ejercicio (feature 009, FR-061..FR-064). Versionado: modificar uno aprobado
/// crea otra versión con motivo y deja la anterior <c>Superseded</c>; el informe puede mostrar
/// inicial y vigente.
/// </summary>
public class Budget : AuditableEntity
{
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }
    public int Version { get; set; } = 1;
    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ChangeReason { get; set; }

    public ICollection<BudgetLine> Lines { get; set; } = [];
}
