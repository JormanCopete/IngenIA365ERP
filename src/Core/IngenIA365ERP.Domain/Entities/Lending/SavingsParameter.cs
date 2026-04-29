using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_SavingsParameters] (cop_ahorro58).</summary>
public class SavingsParameter : AuditableEntity
{
    public int SavingsLineId { get; set; }
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(25)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(2)]
    public string InterestPaymentPeriod { get; set; } = string.Empty;
    public decimal MinInterestBalance { get; set; }
    public decimal MinTransactionAmount { get; set; }
    public decimal MinAccountBalance { get; set; }
    public decimal InterestPaymentRate { get; set; }
    public decimal MinWithholdingAmount { get; set; }
    public decimal WithholdingRate { get; set; }
    public int ClearingDays { get; set; }
    [MaxLength(2)]
    public string LiquidationForm { get; set; } = string.Empty;
    public int GraceDays { get; set; }
    public decimal MaxWithdrawalAmount { get; set; }
    [MaxLength(3)]
    public string InterestConceptCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string WithholdingConceptCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentForm { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    [MaxLength(2)]
    public string FourPerMillForm { get; set; } = string.Empty;
    [MaxLength(5)]
    public string FourPerMillConcept { get; set; } = string.Empty;
    public decimal FourPerMillCeiling { get; set; }
    [MaxLength(5)]
    public string FourPerMillVoucher { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Comment { get; set; } = string.Empty;
    public decimal MaxCashAmount { get; set; }
    public decimal Consecutive { get; set; }
    public int CheckWithdrawalGmf { get; set; }
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int SourceId { get; set; }
    public int InterestConceptId { get; set; }
    public int OtherClearingDays { get; set; }
    [MaxLength(2)]
    public string ValidateWithdrawal { get; set; } = string.Empty;
    public decimal WithdrawalCeiling { get; set; }
    [MaxLength(2)]
    public string ManagesPapForm { get; set; } = string.Empty;
    [MaxLength(15)]
    public string TreasuryAccount { get; set; } = string.Empty;
    public int? LegacyLinCred { get; set; }
}
