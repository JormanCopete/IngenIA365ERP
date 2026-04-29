using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_OrderTransactions] (inv_movtos_orden).</summary>
public class OrderTransaction : AuditableEntityLong
{
    public int TransactionTypeId { get; set; }
    public decimal SequenceNumber { get; set; }
    public DateOnly TransactionDate { get; set; }

    [MaxLength(20)]
    public string? InvoiceNumber { get; set; }

    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal VatRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal CostAmount { get; set; }
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
    public int? PeriodCode { get; set; }

    [MaxLength(5)]
    public string? SaleType { get; set; }

    [MaxLength(5)]
    public string? MovementClass { get; set; }

    public decimal AdminFee { get; set; }
    public decimal AdminVat { get; set; }
    public decimal TicketVat { get; set; }
    public decimal OtherTax { get; set; }
    public decimal AirportTax { get; set; }
    public decimal FuelTax { get; set; }
    public decimal WithholdingRate { get; set; }
    public decimal WithholdingAmount { get; set; }
    public decimal IcaAmount { get; set; }
    public decimal IcaRate { get; set; }
    public bool IsPosTransaction { get; set; }
    public int? WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public long ConsecutiveNumber { get; set; }
    public bool IsOrderApplied { get; set; }

    [MaxLength(100)]
    public string? TransferRecord { get; set; }

    // Navigation
    public InventoryTransactionType? TransactionType { get; set; }
    public Product? Product { get; set; }
}
