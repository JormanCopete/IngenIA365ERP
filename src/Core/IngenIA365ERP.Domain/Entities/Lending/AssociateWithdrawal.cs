using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AssociateWithdrawals].</summary>
public class AssociateWithdrawal : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    [MaxLength(2)]
    public string PriorStatus { get; set; } = string.Empty;
    [MaxLength(2)]
    public string CurrentStatus { get; set; } = string.Empty;
    [MaxLength(5)]
    public string ReasonCode { get; set; } = string.Empty;
    public int WithdrawalReasonId { get; set; }
    public DateTime SystemDate { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    public int Period { get; set; }
    public DateOnly ExpirationDate { get; set; }
    [MaxLength(2)]
    public string? CurrentClass { get; set; }
    [MaxLength(2)]
    public string? PriorClass { get; set; }
}
