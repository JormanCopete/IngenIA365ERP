using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Associates] — one-to-one with Person for associate-specific data.
/// Legacy: extracted from sys_maenit association fields.
/// </summary>
public class Associate : AuditableEntity
{
    public int PersonId { get; set; }

    public DateOnly? JoinDate { get; set; }

    public decimal ContributionRate { get; set; }

    public int? EmployerCompanyId { get; set; }

    public int? BranchId { get; set; }

    public int? CostCenterId { get; set; }

    public int? SectionId { get; set; }

    public short CutoffDay { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }

    public DateOnly? WithdrawalDate { get; set; }

    public int? WithdrawalReasonId { get; set; }

    public DateOnly? RejoinDate { get; set; }

    [MaxLength(2)]
    public string? CategoryRating { get; set; }

    public int? AdvisorId { get; set; }

    [MaxLength(2)]
    public string? DeductionPeriod { get; set; }

    [MaxLength(2)]
    public string? DeductionType { get; set; }

    [MaxLength(4)]
    public string? Rank { get; set; }

    [MaxLength(20)]
    public string? ReferredBy { get; set; }

    public bool IsInLegalCollection { get; set; }

    public short ContractNumber { get; set; }

    public DateOnly? ContractExpiryDate { get; set; }

    public int? CommitteeId { get; set; }

    [MaxLength(20)]
    public string? ZoneCode { get; set; }

    public decimal ContributionPledged { get; set; }

    [MaxLength(4)]
    public string? AssociateClass { get; set; }

    [MaxLength(4)]
    public string? PaymentType { get; set; }

    [MaxLength(10)]
    public string? SectorCode { get; set; }

    public DateOnly? LastTransferDate { get; set; }

    public short MailingOption { get; set; }

    [MaxLength(2)]
    public string? ManualRating { get; set; }

    [MaxLength(2)]
    public string? PreviousClass { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public EmployerCompany? EmployerCompany { get; set; }
    public Branch? Branch { get; set; }
    public CostCenter? CostCenter { get; set; }
    public Section? Section { get; set; }
    public WithdrawalReason? WithdrawalReason { get; set; }
    public Advisor? Advisor { get; set; }
    public Committee? Committee { get; set; }
}
