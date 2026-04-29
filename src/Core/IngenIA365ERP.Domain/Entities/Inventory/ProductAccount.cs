using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_ProductAccounts] (inv_cuentas).</summary>
public class ProductAccount : AuditableEntity
{
    public int ProductGroupId { get; set; }
    public int TransactionTypeId { get; set; }
    public int WarehouseId { get; set; }
    public int LocationId { get; set; }

    [MaxLength(15)]
    public string? VatAccountCode { get; set; }

    [MaxLength(15)]
    public string? DiscountAccountCode { get; set; }

    [MaxLength(15)]
    public string? TaxableSalesAccountCode { get; set; }

    [MaxLength(15)]
    public string? NonTaxableSalesAccountCode { get; set; }

    [MaxLength(15)]
    public string? NetAccountCode { get; set; }
}
