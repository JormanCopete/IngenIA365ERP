using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ApplicationStatuses] (cop_estsol).</summary>
public class ApplicationStatus : AuditableEntity
{
    public int ApplicationNumber { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    [MaxLength(2)]
    public string HasApplication { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasPromissory { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasPayrollDeduction { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasOtherDocs { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ApplicationComplete { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DataUpdated { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ObligationsOk { get; set; } = string.Empty;
    public decimal PaymentCapacity { get; set; }
    [MaxLength(2)]
    public string IncomeProof { get; set; } = string.Empty;
    public decimal CifinScore { get; set; }
    public decimal IndebtednessLevel { get; set; }
    [MaxLength(2)]
    public string? AssetQuality { get; set; }
    public decimal ContingencyLevel { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public string Evaluation { get; set; } = string.Empty;
    public string ReferenceVerification { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? UserId { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    public DateOnly SystemDate { get; set; }
    [MaxLength(2)]
    public string? ClaEne1 { get; set; }
    [MaxLength(2)]
    public string? ClaEne2 { get; set; }
    [MaxLength(2)]
    public string? ClaEne3 { get; set; }
    [MaxLength(2)]
    public string? ClaEne4 { get; set; }
    [MaxLength(2)]
    public string? ClaEne5 { get; set; }
    [MaxLength(2)]
    public string? ClaFeb1 { get; set; }
    [MaxLength(2)]
    public string? ClaFeb2 { get; set; }
    [MaxLength(2)]
    public string? ClaFeb3 { get; set; }
    [MaxLength(2)]
    public string? ClaFeb4 { get; set; }
    [MaxLength(2)]
    public string? ClaFeb5 { get; set; }
    [MaxLength(2)]
    public string? ClaMar1 { get; set; }
    [MaxLength(2)]
    public string? ClaMar2 { get; set; }
    [MaxLength(2)]
    public string? ClaMar3 { get; set; }
    [MaxLength(2)]
    public string? ClaMar4 { get; set; }
    [MaxLength(2)]
    public string? ClaMar5 { get; set; }
    [MaxLength(2)]
    public string? ClaAbr1 { get; set; }
    [MaxLength(2)]
    public string? ClaAbr2 { get; set; }
    [MaxLength(2)]
    public string? ClaAbr3 { get; set; }
    [MaxLength(2)]
    public string? ClaAbr4 { get; set; }
    [MaxLength(2)]
    public string? ClaAbr5 { get; set; }
    [MaxLength(2)]
    public string? ClaMay1 { get; set; }
    [MaxLength(2)]
    public string? ClaMay2 { get; set; }
    [MaxLength(2)]
    public string? ClaMay3 { get; set; }
    [MaxLength(2)]
    public string? ClaMay4 { get; set; }
    [MaxLength(2)]
    public string? ClaMay5 { get; set; }
    [MaxLength(2)]
    public string? ClaJun1 { get; set; }
    [MaxLength(2)]
    public string? ClaJun2 { get; set; }
    [MaxLength(2)]
    public string? ClaJun3 { get; set; }
    [MaxLength(2)]
    public string? ClaJun4 { get; set; }
    [MaxLength(2)]
    public string? ClaJun5 { get; set; }
    [MaxLength(2)]
    public string? ClaJul1 { get; set; }
    [MaxLength(2)]
    public string? ClaJul2 { get; set; }
    [MaxLength(2)]
    public string? ClaJul3 { get; set; }
    [MaxLength(2)]
    public string? ClaJul4 { get; set; }
    [MaxLength(2)]
    public string? ClaJul5 { get; set; }
    [MaxLength(2)]
    public string? ClaAgo1 { get; set; }
    [MaxLength(2)]
    public string? ClaAgo2 { get; set; }
    [MaxLength(2)]
    public string? ClaAgo3 { get; set; }
    [MaxLength(2)]
    public string? ClaAgo4 { get; set; }
    [MaxLength(2)]
    public string? ClaAgo5 { get; set; }
    [MaxLength(2)]
    public string? ClaSep1 { get; set; }
    [MaxLength(2)]
    public string? ClaSep2 { get; set; }
    [MaxLength(2)]
    public string? ClaSep3 { get; set; }
    [MaxLength(2)]
    public string? ClaSep4 { get; set; }
    [MaxLength(2)]
    public string? ClaSep5 { get; set; }
    [MaxLength(2)]
    public string? ClaOct1 { get; set; }
    [MaxLength(2)]
    public string? ClaOct2 { get; set; }
    [MaxLength(2)]
    public string? ClaOct3 { get; set; }
    [MaxLength(2)]
    public string? ClaOct4 { get; set; }
    [MaxLength(2)]
    public string? ClaOct5 { get; set; }
    [MaxLength(2)]
    public string? ClaNov1 { get; set; }
    [MaxLength(2)]
    public string? ClaNov2 { get; set; }
    [MaxLength(2)]
    public string? ClaNov3 { get; set; }
    [MaxLength(2)]
    public string? ClaNov4 { get; set; }
    [MaxLength(2)]
    public string? ClaNov5 { get; set; }
    [MaxLength(2)]
    public string? ClaDic1 { get; set; }
    [MaxLength(2)]
    public string? ClaDic2 { get; set; }
    [MaxLength(2)]
    public string? ClaDic3 { get; set; }
    [MaxLength(2)]
    public string? ClaDic4 { get; set; }
    [MaxLength(2)]
    public string? ClaDic5 { get; set; }
    public DateTime StudyDate { get; set; }
    public decimal PayrollPayment { get; set; }
    public decimal CashPayment { get; set; }
    public decimal AssetHousing { get; set; }
    public decimal AssetVehicle { get; set; }
    public decimal AssetContributions { get; set; }
    public decimal AssetOther { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal LiabilityDebts { get; set; }
    public decimal LiabilityOther { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal Equity { get; set; }
    public decimal TotalLiabilitiesEquity { get; set; }
    public decimal PersonalExpenses { get; set; }
    [MaxLength(2)]
    public string DebtConsolidation { get; set; } = string.Empty;
    public int AccountNumber { get; set; }
    public decimal AverageSalary { get; set; }
    public decimal AssetCashBank { get; set; }
    public decimal AssetReceivables { get; set; }
    public decimal LiabilityBankLoans { get; set; }
    public decimal LiabilityMortgage { get; set; }
    [MaxLength(2)]
    public string PercentageFlag { get; set; } = string.Empty;
    public decimal AssetSavings { get; set; }
    public decimal DatacreditoScore { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal DiscoveredAmount { get; set; }
    [MaxLength(3)]
    public string DatacreditoRating { get; set; } = string.Empty;
    public decimal ExternalDebtInstallment { get; set; }
    public decimal ExternalDebtBalance { get; set; }
    public decimal DatacreditoOverdue { get; set; }
    public decimal InsuranceExtra { get; set; }
    public decimal CapitalAtRisk { get; set; }
    public decimal PaymasterPercentage { get; set; }
    [MaxLength(2)]
    public string? PaymasterDeductionType { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
