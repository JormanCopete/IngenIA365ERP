using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DepositEntries].</summary>
public class DepositEntry : AuditableEntityLong
{
    public int DepositLineId { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public long AccountNumber { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly CreationDate { get; set; }
    public DateOnly FirstDeductionDate { get; set; }
    [MaxLength(2)]
    public string DeductionType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    public decimal InstallmentAmount { get; set; }
    public int Term { get; set; }
    public DateOnly? MaturityDate { get; set; }
    [MaxLength(2)]
    public string EntryType { get; set; } = string.Empty;
    [MaxLength(15)]
    public string UserId { get; set; } = string.Empty;
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    public DateOnly SystemDate { get; set; }
}
