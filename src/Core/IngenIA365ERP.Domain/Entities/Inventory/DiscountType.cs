using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_DiscountTypes] (inv_tipodstos).</summary>
public class DiscountType : AuditableEntity
{
    public int TypeCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortName { get; set; }

    public int? DiscountClass { get; set; }

    // Navigation
    public ICollection<Product> Products { get; set; } = [];
}
