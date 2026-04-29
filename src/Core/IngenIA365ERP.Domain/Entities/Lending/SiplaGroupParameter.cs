using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_SiplaGroupParameters].</summary>
public class SiplaGroupParameter : AuditableEntity
{
    [MaxLength(3)]
    public string ConceptCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
