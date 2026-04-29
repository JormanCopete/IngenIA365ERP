using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Transactions] (cop_movimto).</summary>
public class LendingTransaction : AuditableEntityLong
{
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    [MaxLength(3)]
    public string TransactionCode { get; set; } = string.Empty;
    public int Cycles { get; set; }
    [MaxLength(15)]
    public string SecondaryAccount { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    [MaxLength(8)]
    public string SearchCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BankCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CrossDocumentType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? CrossDocumentNumber { get; set; }
    public decimal LocalCheckAmount { get; set; }
    public decimal OtherCheckAmount { get; set; }
    [MaxLength(10)]
    public string SecondaryCostCenter { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsAdvancePayment { get; set; } = string.Empty;
    public decimal WithholdingBase { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
    public DateOnly SystemDate { get; set; }
    public int ExtraNumber { get; set; }
    [MaxLength(20)]
    public string? IdentificationNumber { get; set; }
    [MaxLength(20)]
    public string? InvoiceNumber { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    [MaxLength(8)]
    public string Period { get; set; } = string.Empty;
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    [MaxLength(20)]
    public string CheckNumber { get; set; } = string.Empty;
    [MaxLength(20)]
    public string CodeudorCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? OverdraftUser { get; set; }
    public decimal? OverdraftAmount { get; set; }
    public decimal ReliquidatedInstallment { get; set; }
    [MaxLength(3)]
    public string TransactionSource { get; set; } = string.Empty;
    public int ApplicationId { get; set; }
    public DateTime? CdatAccrualDate { get; set; }
    public int AuxiliaryApplicationId { get; set; }
    public long DependsOnSequence { get; set; }
    [MaxLength(2)]
    public string? ReliquidationFlag { get; set; }
    public int? SavingsLineId { get; set; }
    public long? LegacySecuencia { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
