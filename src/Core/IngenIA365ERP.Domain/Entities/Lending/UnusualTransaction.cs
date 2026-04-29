using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_UnusualTransactions].</summary>
public class UnusualTransaction : AuditableEntityLong
{
    [MaxLength(3)]
    public string ConceptCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(8)]
    public string Period { get; set; } = string.Empty;
    public decimal MonthlyCreditMoves { get; set; }
    public decimal MaxBalance { get; set; }
    public int AccountCount { get; set; }
    public int AnnualTransactions { get; set; }
    public DateTime EntryDate { get; set; }
    [MaxLength(3)]
    public string UnusualType { get; set; } = string.Empty;
    [MaxLength(5)]
    public string? VoucherType { get; set; }
    public long? DocumentNumber { get; set; }
}
