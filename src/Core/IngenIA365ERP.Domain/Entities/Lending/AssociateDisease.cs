using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AssociateDiseases].</summary>
public class AssociateDisease : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int DiseaseCode { get; set; }
    public DateTime? SystemDate { get; set; }
    [MaxLength(60)]
    public string UserId { get; set; } = string.Empty;
}
