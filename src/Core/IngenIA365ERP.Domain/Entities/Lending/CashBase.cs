using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CashBases].</summary>
public class CashBase : AuditableEntityLong
{
    [MaxLength(20)]
    public string CashierCode { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public DateOnly TransactionDate { get; set; }
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [MaxLength(20)]
    public string ReceivedByUser { get; set; } = string.Empty;
    [MaxLength(20)]
    public string DeliveredByUser { get; set; } = string.Empty;
}
