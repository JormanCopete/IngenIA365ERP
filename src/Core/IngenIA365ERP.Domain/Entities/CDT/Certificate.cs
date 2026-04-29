using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_Certificates] (cdt_maecdats).</summary>
public class Certificate : AuditableEntity
{
    [MaxLength(20)]
    public string CertificateNumber { get; set; } = string.Empty;

    public int PersonId { get; set; }
    public int CreditLineId { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public DateOnly? AccrualDate { get; set; }
    public decimal Amount { get; set; }
    public decimal InterestRate { get; set; }
    public int Term { get; set; }

    [MaxLength(2)]
    public string? RenewalType { get; set; }

    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? PaymentMethod { get; set; }

    public int? Periodicity { get; set; }
    public int? BranchId { get; set; }

    [MaxLength(20)]
    public string? LegalRepId { get; set; }

    [MaxLength(50)]
    public string? LegalRepName { get; set; }

    [MaxLength(50)]
    public string? BusinessAddress { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? Mobile { get; set; }

    [MaxLength(20)]
    public string? SignatoryId1 { get; set; }

    [MaxLength(20)]
    public string? SignatoryId2 { get; set; }

    [MaxLength(20)]
    public string? SignatoryId3 { get; set; }

    [MaxLength(50)]
    public string? SignatoryName1 { get; set; }

    [MaxLength(50)]
    public string? SignatoryName2 { get; set; }

    [MaxLength(50)]
    public string? SignatoryName3 { get; set; }

    [MaxLength(20)]
    public string? BeneficiaryId1 { get; set; }

    [MaxLength(20)]
    public string? BeneficiaryId2 { get; set; }

    [MaxLength(20)]
    public string? BeneficiaryId3 { get; set; }

    [MaxLength(20)]
    public string? BeneficiaryId4 { get; set; }

    [MaxLength(20)]
    public string? BeneficiaryId5 { get; set; }

    [MaxLength(50)]
    public string? BeneficiaryName1 { get; set; }

    [MaxLength(50)]
    public string? BeneficiaryName2 { get; set; }

    [MaxLength(50)]
    public string? BeneficiaryName3 { get; set; }

    [MaxLength(50)]
    public string? BeneficiaryName4 { get; set; }

    [MaxLength(50)]
    public string? BeneficiaryName5 { get; set; }

    public decimal BeneficiaryPct1 { get; set; }
    public decimal BeneficiaryPct2 { get; set; }
    public decimal BeneficiaryPct3 { get; set; }
    public decimal BeneficiaryPct4 { get; set; }
    public decimal BeneficiaryPct5 { get; set; }

    [MaxLength(20)]
    public string? CancelledByUserId { get; set; }

    public DateTime? CancellationDate { get; set; }
    public bool IsCapitalized { get; set; }
    public int? PreviousCertificateId { get; set; }

    // Navigation
    public ICollection<CertificateEntry> Entries { get; set; } = [];
}
