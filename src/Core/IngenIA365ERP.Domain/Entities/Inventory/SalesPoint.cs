using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_SalesPoints] (inv_puntos).</summary>
public class SalesPoint : AuditableEntity
{
    public int PointCode { get; set; }

    [MaxLength(20)]
    public string? UserId { get; set; }

    public int? TransactionTypeId { get; set; }

    [MaxLength(50)]
    public string? PrinterName { get; set; }

    public int Status { get; set; }
    public DateTime? DateId { get; set; }
    public int? ShiftId { get; set; }
    public decimal BaseAmount { get; set; }
    public int? WarehouseId { get; set; }
    public int? LocationId { get; set; }
}
