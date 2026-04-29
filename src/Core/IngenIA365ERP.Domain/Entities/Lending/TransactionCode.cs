using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_TransactionCodes] (cop_codmov).</summary>
public class TransactionCode : AuditableEntity
{
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(25)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(3)]
    public string TransactionType { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AdjustAccrual { get; set; } = string.Empty;
    [MaxLength(2)]
    public string DebitCreditFlag { get; set; } = string.Empty;
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int SourceId { get; set; }
    [MaxLength(3)]
    public string? LegacyCodMovto { get; set; }
}
