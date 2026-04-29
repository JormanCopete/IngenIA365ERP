using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_Agreements] (deb_enpacto).</summary>
public class DebitAgreement : AuditableEntity
{
    public long BatchId { get; set; }

    [MaxLength(10)]
    public string ProcessDate { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? ProcessTime { get; set; }

    [MaxLength(25)]
    public string CardNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? AuthCode { get; set; }

    [MaxLength(30)]
    public string? AccountNumber { get; set; }

    [MaxLength(2)]
    public string? NetworkCode { get; set; }

    [MaxLength(2)]
    public string? TransactionType { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(2)]
    public string? ConceptCode { get; set; }
}
