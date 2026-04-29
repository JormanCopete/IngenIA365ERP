using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Warehouses] (inv_bodegas).</summary>
public class Warehouse : AuditableEntity
{
    public int WarehouseCode { get; set; }
    public int LocationId { get; set; }

    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ShortDescription { get; set; }

    // Navigation
    public Location? Location { get; set; }
}
