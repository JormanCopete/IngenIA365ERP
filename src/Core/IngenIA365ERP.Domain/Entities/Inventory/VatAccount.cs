using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_VatAccounts] (inv_cuentasiva).</summary>
public class VatAccount : AuditableEntity
{
    public int ProductGroupId { get; set; }
    public int TransactionTypeId { get; set; }
    public int WarehouseId { get; set; }
    public int LocationId { get; set; }
    public decimal VatRate { get; set; }

    [MaxLength(5)]
    public string? AccountType { get; set; }

    [MaxLength(15)]
    public string? AccountCode { get; set; }
}
