using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_PriceListTypes] (inv_tipolistas).</summary>
public class PriceListType : AuditableEntity
{
    public int TypeCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortName { get; set; }

    public int? PriceClass { get; set; }
}
