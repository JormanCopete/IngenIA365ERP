using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Documents] (cop_docmto).</summary>
public class LendingDocument : AuditableEntityLong
{
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    [MaxLength(20)]
    public string AccountCode { get; set; } = string.Empty;
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    [MaxLength(5)]
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentSequence { get; set; }
    public DateOnly DocumentDate { get; set; }
    [MaxLength(15)]
    public string CheckNumber { get; set; } = string.Empty;
    [MaxLength(2)]
    public string RecordFlag { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BankCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsClosed { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IsVoided { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? ClosedInPortfolio { get; set; }
    [MaxLength(20)]
    public string BeneficiaryId { get; set; } = string.Empty;
    [MaxLength(20)]
    public string BeneficiaryCheckId { get; set; } = string.Empty;
    [MaxLength(5)]
    public string? LegacyCompronte { get; set; }
    public long? LegacyNumeroDomto { get; set; }
}
