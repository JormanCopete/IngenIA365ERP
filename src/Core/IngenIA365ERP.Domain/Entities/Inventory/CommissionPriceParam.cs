using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_CommissionPriceParams] (inv_param_comisiones_Precios).</summary>
public class CommissionPriceParam : AuditableEntity
{
    public int InvoiceTypeId { get; set; }
    public int CommissionGroupId { get; set; }
    public int GroupId { get; set; }

    [MaxLength(5)]
    public string CustomerType { get; set; } = string.Empty;

    public decimal CommissionRate { get; set; }
}
