using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_SavingsAccounts] (cop_maeahor).</summary>
public class SavingsAccount : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int SavingsLineId { get; set; }
    public long AccountNumber { get; set; }
    public DateOnly CreationDate { get; set; }
    [MaxLength(20)]
    public string LegalRepresentative { get; set; } = string.Empty;
    [MaxLength(50)]
    public string RepresentativeName { get; set; } = string.Empty;
    [MaxLength(25)]
    public string CommercialAddress { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(20)]
    public string CellPhone { get; set; } = string.Empty;
    [MaxLength(2)]
    public string UniqueAccount { get; set; } = string.Empty;
    public DateOnly? ExemptionDate { get; set; }
    [MaxLength(2)]
    public string AutoDebit { get; set; } = string.Empty;
    public DateOnly? FirstDeductionDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    [MaxLength(2)]
    public string DeductionType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasSeal { get; set; } = string.Empty;
    [MaxLength(2)]
    public string HasProtector { get; set; } = string.Empty;
    public int RegisteredSignatures { get; set; }
    public int RequiredSignatures { get; set; }
    [MaxLength(20)]
    public string SignatoryId1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string SignatoryId2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string SignatoryId3 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SignatoryName1 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SignatoryName2 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string SignatoryName3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId4 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryId5 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string BeneficiaryName1 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string BeneficiaryName2 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string BeneficiaryName3 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string BeneficiaryName4 { get; set; } = string.Empty;
    [MaxLength(50)]
    public string BeneficiaryName5 { get; set; } = string.Empty;
    public decimal BeneficiaryPct1 { get; set; }
    public decimal BeneficiaryPct2 { get; set; }
    public decimal BeneficiaryPct3 { get; set; }
    public decimal BeneficiaryPct4 { get; set; }
    public decimal BeneficiaryPct5 { get; set; }
    public long? LegacyNumCuenta { get; set; }

    // Navigation
}
