using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_Audit] (cdt_maeaud).</summary>
public class CdtAudit : AuditableEntityLong
{
    [MaxLength(1)]
    public string Action { get; set; } = string.Empty;

    public DateTime? ActionDate { get; set; }

    [MaxLength(50)]
    public string? UserName { get; set; }

    [MaxLength(50)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }
    public int? PersonId { get; set; }
    public int? CreditLineId { get; set; }
    public DateOnly? IssueDateOld { get; set; }
    public DateOnly? IssueDateNew { get; set; }

    [MaxLength(20)]
    public string? LegalRepIdOld { get; set; }

    [MaxLength(20)]
    public string? LegalRepIdNew { get; set; }

    [MaxLength(50)]
    public string? LegalRepNameOld { get; set; }

    [MaxLength(50)]
    public string? LegalRepNameNew { get; set; }

    [MaxLength(50)]
    public string? AddressOld { get; set; }

    [MaxLength(50)]
    public string? AddressNew { get; set; }

    [MaxLength(20)]
    public string? PhoneOld { get; set; }

    [MaxLength(20)]
    public string? PhoneNew { get; set; }

    [MaxLength(20)]
    public string? MobileOld { get; set; }

    [MaxLength(20)]
    public string? MobileNew { get; set; }

    [MaxLength(2)]
    public string? StatusOld { get; set; }

    [MaxLength(2)]
    public string? StatusNew { get; set; }

    public DateOnly? AccrualDateOld { get; set; }
    public DateOnly? AccrualDateNew { get; set; }
    public DateOnly? MaturityDateOld { get; set; }
    public DateOnly? MaturityDateNew { get; set; }
    public int? TermOld { get; set; }
    public int? TermNew { get; set; }
    public decimal? InterestRateOld { get; set; }
    public decimal? InterestRateNew { get; set; }
    public decimal? AmountOld { get; set; }
    public decimal? AmountNew { get; set; }

    [MaxLength(20)]
    public string? CancelledByOld { get; set; }

    [MaxLength(20)]
    public string? CancelledByNew { get; set; }

    public DateTime? CancellationDateOld { get; set; }
    public DateTime? CancellationDateNew { get; set; }

    [MaxLength(2)]
    public string? IsCapitalizedOld { get; set; }

    [MaxLength(2)]
    public string? IsCapitalizedNew { get; set; }

    public int? PreviousCertIdOld { get; set; }
    public int? PreviousCertIdNew { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
