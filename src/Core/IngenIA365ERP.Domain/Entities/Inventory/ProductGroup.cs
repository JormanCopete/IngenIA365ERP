using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_ProductGroups] (inv_grupos).</summary>
public class ProductGroup : AuditableEntity
{
    public int GroupCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortName { get; set; }

    public int? SecondaryGroupId { get; set; }
    public bool RestrictsLimit { get; set; }
    public int MaxSalesQuantity { get; set; }

    // Navigation
    public SecondaryGroup? SecondaryGroup { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}
