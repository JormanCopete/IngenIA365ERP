using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Transactions] (inv_movtos).</summary>
public class InventoryTransaction : AuditableEntityLong
{
    public int TransactionTypeId { get; set; }
    public decimal SequenceNumber { get; set; }
    public DateOnly TransactionDate { get; set; }

    [MaxLength(20)]
    public string? InvoiceNumber { get; set; }

    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public byte CostFlag { get; set; }
    public DateTime SystemDate { get; set; }
    public int? CustomerId { get; set; }
    public decimal VatAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal UnitPrice { get; set; }

    [MaxLength(20)]
    public string? UserId { get; set; }

    public int? SalesPointId { get; set; }
    public int? ShiftId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal AdminFee { get; set; }
    public decimal AdminVat { get; set; }
    public decimal TicketVat { get; set; }
    public decimal OtherTax { get; set; }
    public decimal AirportTax { get; set; }
    public decimal FuelTax { get; set; }
    public bool IsPosTransaction { get; set; }
    public int? WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public long ConsecutiveNumber { get; set; }

    // Navigation
    public InventoryTransactionType? TransactionType { get; set; }
    public Product? Product { get; set; }
}
