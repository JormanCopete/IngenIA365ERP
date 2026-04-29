using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_OrderDocuments] (inv_docs_orden).</summary>
public class OrderDocument : AuditableEntityLong
{
    public int TransactionTypeId { get; set; }
    public decimal SequenceNumber { get; set; }
    public int? CustomerId { get; set; }
    public DateTime EntryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal SubTotalAmount { get; set; }

    [MaxLength(5)]
    public string? Status { get; set; }

    [MaxLength(20)]
    public string? UserId { get; set; }

    public int PaymentClassId { get; set; }
    public decimal CashAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal DebitCardAmount { get; set; }
    public decimal CreditCardAmount { get; set; }
    public decimal CheckAmount { get; set; }

    [MaxLength(10)]
    public string? BankId { get; set; }

    public int ItemCount { get; set; }

    [MaxLength(20)]
    public string? BankAccountNumber { get; set; }

    public int? SalesPointId { get; set; }
    public int? ShiftId { get; set; }
    public decimal AuditAmount { get; set; }

    [MaxLength(300)]
    public string? Detail { get; set; }

    public long? InvoiceNumber { get; set; }
    public decimal ChangeAmount { get; set; }
    public int? Periodicity { get; set; }
    public int? Term { get; set; }
    public int? DeductionType { get; set; }
    public DateTime? DiscountDate { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal InterestRate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal WithholdingAmount { get; set; }
    public decimal IcaAmount { get; set; }
    public int? SalesPersonId { get; set; }
    public int? ReturnTypeId { get; set; }
    public decimal? ReturnSequence { get; set; }
    public int? TransferTypeId { get; set; }
    public decimal? TransferSequence { get; set; }

    // Navigation
    public InventoryTransactionType? TransactionType { get; set; }
}
