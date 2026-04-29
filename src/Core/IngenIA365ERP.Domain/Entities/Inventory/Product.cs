using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Products] (inv_productos).</summary>
public class Product : AuditableEntity
{
    public int ProductCode { get; set; }

    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ShortName { get; set; }

    public int? GroupId { get; set; }
    public int? DiscountTypeId { get; set; }

    [MaxLength(10)]
    public string? UnitOfMeasure { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal VatRate { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int CurrentStock { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(30)]
    public string? Barcode { get; set; }

    public decimal OtherTax { get; set; }
    public bool ControlsStock { get; set; }
    public bool RestrictsLimit { get; set; }
    public int MaxSalesQuantity { get; set; }

    // Navigation
    public ProductGroup? Group { get; set; }
    public DiscountType? DiscountType { get; set; }
}
