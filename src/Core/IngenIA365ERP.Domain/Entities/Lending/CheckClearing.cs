using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CheckClearing].</summary>
public class CheckClearing : AuditableEntityLong
{
    public long AccountNumber { get; set; }
    public decimal CheckNumber { get; set; }
    public DateOnly DepositDate { get; set; }
    public int ClearingDays { get; set; }
    public DateOnly MaturityDate { get; set; }
    [MaxLength(2)]
    public string Plaza { get; set; } = string.Empty;
    [MaxLength(5)]
    public string? BankCode { get; set; }
    public decimal? Amount { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    public int ClearingType { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
}
