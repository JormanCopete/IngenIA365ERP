using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_RecreationApplications].</summary>
public class RecreationApplication : AuditableEntity
{
    public int ApplicationNumber { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public int CreditNumber { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal? AdditionalInterest { get; set; }
    [MaxLength(2)]
    public string FullOrPartial { get; set; } = string.Empty;
    public DateOnly MaturityDate { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
