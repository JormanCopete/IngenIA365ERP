using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_CommissionParameters] (inv_param_comisiones).</summary>
public class CommissionParameter : AuditableEntity
{
    public int InvoiceTypeId { get; set; }
    public int CommissionGroupId { get; set; }
    public int GroupId { get; set; }
    public decimal SalesRangeStart { get; set; }
    public decimal SalesRangeEnd { get; set; }
    public decimal CommissionRate { get; set; }
}
