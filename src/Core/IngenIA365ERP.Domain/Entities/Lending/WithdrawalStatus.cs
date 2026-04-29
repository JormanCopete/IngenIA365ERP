using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_WithdrawalStatuses].</summary>
public class WithdrawalStatus : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public DateOnly RequestDate { get; set; }
    [MaxLength(5)]
    public string ReasonCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public string? Remarks { get; set; }
    public int Period { get; set; }
}
