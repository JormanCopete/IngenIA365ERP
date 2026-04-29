using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_PhysicalInventory] (inv_invfisico).</summary>
public class PhysicalInventory : AuditableEntityLong
{
    [MaxLength(10)]
    public string PeriodCode { get; set; } = string.Empty;

    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public int WarehouseId { get; set; }
    public int PhysicalCount { get; set; }
    public int TheoreticalCount { get; set; }
    public decimal Cost { get; set; }

    // Navigation
    public Product? Product { get; set; }
}
