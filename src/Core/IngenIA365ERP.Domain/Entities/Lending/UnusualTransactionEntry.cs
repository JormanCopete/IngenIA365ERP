using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_UnusualTransactionEntries].</summary>
public class UnusualTransactionEntry : AuditableEntityLong
{
    public int UnusualTransactionId { get; set; }
    public DateTime EntryDate { get; set; }
    [MaxLength(2)]
    public string CurrentStatus { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? PriorStatus { get; set; }
    [MaxLength(250)]
    public string Remarks { get; set; } = string.Empty;
    [MaxLength(20)]
    public string RegisteredBy { get; set; } = string.Empty;
}
