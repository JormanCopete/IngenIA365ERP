using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_TransactionTypes] (inv_tipomovtos).</summary>
public class InventoryTransactionType : AuditableEntity
{
    public int TypeCode { get; set; }

    [MaxLength(150)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? ShortDescription { get; set; }

    [MaxLength(5)]
    public string? TransactionVoucherCode { get; set; }

    [MaxLength(5)]
    public string? CostVoucherCode { get; set; }

    public decimal SequenceNumber { get; set; }
    public int ControlsStock { get; set; }

    [MaxLength(5)]
    public string? DocumentClass { get; set; }

    public int UpdatesAccounting { get; set; }

    [MaxLength(5)]
    public string? PortfolioVoucherCode { get; set; }

    public int? CreditLineId { get; set; }

    [MaxLength(2)]
    public string? DeductionType { get; set; }

    [MaxLength(5)]
    public string? InvoiceControl { get; set; }

    public bool TotalInPurchase { get; set; }
    public bool CostsProducts { get; set; }
    public bool IsReturn { get; set; }
    public bool TransfersAccounting { get; set; }
    public bool OrderPedido_SustainPrice { get; set; }
    public bool AllowsBonus { get; set; }
    public bool ValidatesCreditLimit { get; set; }
}
