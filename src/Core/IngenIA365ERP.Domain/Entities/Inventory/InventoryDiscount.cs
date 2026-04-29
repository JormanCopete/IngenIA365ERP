using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Discounts] (inv_dstos).</summary>
public class InventoryDiscount : AuditableEntityLong
{
    public int? ProductId { get; set; }
    public int? DiscountTypeId { get; set; }
    public int? DiscountClass { get; set; }

    [MaxLength(20)]
    public string? CustomerId { get; set; }

    [MaxLength(5)]
    public string? ProductClass { get; set; }

    [MaxLength(5)]
    public string? CustomerType { get; set; }

    public int? PaymentClassId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int QuantityStart { get; set; }
    public int QuantityEnd { get; set; }
    public int? PurchasePeriod { get; set; }
    public int PurchaseAmount { get; set; }
    public decimal DiscountRate { get; set; }
    public int? PeriodCode { get; set; }

    [MaxLength(10)]
    public string? GroupId { get; set; }

    // Navigation
    public Product? Product { get; set; }
    public DiscountType? DiscountType { get; set; }
}
