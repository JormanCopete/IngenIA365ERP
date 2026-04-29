using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Prices] (inv_precios).</summary>
public class Price : AuditableEntity
{
    public int PriceListTypeId { get; set; }
    public int ProductId { get; set; }

    [MaxLength(5)]
    public string CustomerType { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal PriceValue { get; set; }
    public int? PriceClass { get; set; }

    [MaxLength(50)]
    public string? Description { get; set; }

    // Navigation
    public PriceListType? PriceListType { get; set; }
    public Product? Product { get; set; }
}
