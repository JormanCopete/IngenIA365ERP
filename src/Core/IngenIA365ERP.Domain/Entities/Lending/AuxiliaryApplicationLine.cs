using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AuxiliaryApplicationLines] (cop_linsolaux).</summary>
public class AuxiliaryApplicationLine : AuditableEntityLong
{
    public int ApplicationId { get; set; }
    [MaxLength(5)]
    public string LineCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
    public int Installments { get; set; }
}
