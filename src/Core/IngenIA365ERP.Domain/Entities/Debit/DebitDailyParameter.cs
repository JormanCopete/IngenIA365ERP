using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_DailyParameters] (deb_pardiario).</summary>
public class DebitDailyParameter : AuditableEntity
{
    public int ParameterCode { get; set; }

    [MaxLength(10)]
    public string BankId { get; set; } = string.Empty;

    [MaxLength(5)]
    public string BatchVoucherCode { get; set; } = string.Empty;

    [MaxLength(5)]
    public string OnlineVoucherCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;

    public DateOnly? LastUpdateDate { get; set; }
    public int NewCardsCount { get; set; }

    [MaxLength(25)]
    public string? LastCardNumber { get; set; }

    public DateOnly? ClosingDate { get; set; }

    [MaxLength(5)]
    public string? ClosingVoucherCode { get; set; }

    public long ClosingSequenceNumber { get; set; }

    [MaxLength(5)]
    public string? PosClosingVoucherCode { get; set; }

    public int PosClosingSequence { get; set; }

    [MaxLength(2)]
    public string? ClosingFlag { get; set; }

    public decimal NetworkCommission { get; set; }
    public decimal OtherNetworkCommission { get; set; }
}
