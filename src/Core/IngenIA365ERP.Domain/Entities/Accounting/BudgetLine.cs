using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Valor presupuestado de una cuenta de movimiento en un mes, opcionalmente por sucursal y centro de costo.</summary>
public class BudgetLine : AuditableEntity
{
    public int BudgetId { get; set; }
    public Budget? Budget { get; set; }
    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public byte Month { get; set; }
    public decimal Amount { get; set; }
}
