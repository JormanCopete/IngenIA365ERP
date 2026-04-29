using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_AssociateReferences] (cdt_asoreferencia).</summary>
public class CdtAssociateReference : AuditableEntity
{
    public int CertificateId { get; set; }
    public int PersonId { get; set; }
    public int CreditLineId { get; set; }

    [MaxLength(5)]
    public string RelationshipType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ReferenceName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ReferenceAddress { get; set; }

    public int? CityId { get; set; }

    [MaxLength(30)]
    public string? ReferencePhone { get; set; }

    [MaxLength(30)]
    public string? ReferenceMobile { get; set; }

    // Navigation
    public Certificate? Certificate { get; set; }
}
