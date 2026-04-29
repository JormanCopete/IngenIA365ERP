using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_SiplaParameters].</summary>
public class SiplaParameter : AuditableEntity
{
    [MaxLength(3)]
    public string ConceptCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    public decimal MonthlyCreditMoves { get; set; }
    public decimal MaxBalance { get; set; }
    public int AccountCount { get; set; }
    public int AnnualTransactions { get; set; }
}
