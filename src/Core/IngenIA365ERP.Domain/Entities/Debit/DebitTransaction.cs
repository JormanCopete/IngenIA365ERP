using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_Transactions] (deb_movto).</summary>
public class DebitTransaction : AuditableEntityLong
{
    public int? CardId { get; set; }

    [MaxLength(30)]
    public string SequenceCode { get; set; } = string.Empty;

    [MaxLength(25)]
    public string CardNumber { get; set; } = string.Empty;

    public DateTime? TransactionDate { get; set; }
    public decimal? Amount { get; set; }

    [MaxLength(5)]
    public string? TransactionType { get; set; }

    [MaxLength(5)]
    public string? CausalCode { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }

    [MaxLength(20)]
    public string? SourceSystem { get; set; }

    [MaxLength(10)]
    public string? TransactionTime { get; set; }

    [MaxLength(10)]
    public string? NetworkCode { get; set; }

    public int? MessageCode { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? VatBase { get; set; }
    public decimal? CommissionAmount { get; set; }
    public int? MethodCode { get; set; }

    [MaxLength(5)]
    public string? ErrorCode { get; set; }

    [MaxLength(20)]
    public string? MerchantCode { get; set; }

    [MaxLength(20)]
    public string? AuthorizationCode { get; set; }

    // Navigation
    public DebitCard? Card { get; set; }
}
