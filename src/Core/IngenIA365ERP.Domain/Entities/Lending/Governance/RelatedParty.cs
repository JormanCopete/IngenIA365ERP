using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_RelatedParties</c> (feature 013, T0213; data-model §3.12 y C118; FR-040, T42): una persona vinculada (Ley 454, art. 61) con
/// su fundamento y vigencia. Un vínculo vigente lleva el sujeto al consejo con la regla calificada. Leerla es lectura sensible
/// auditada. No es hecho; dos vigencias del mismo vínculo no se cruzan (lo valida el comando).
/// </summary>
public class RelatedParty : AuditableEntity
{
    public int PersonId { get; set; }

    public RelatedPartyBasis Basis { get; set; }

    /// <summary>En <c>Spouse</c> y <c>Relative</c>: la persona por la que existe el vínculo.</summary>
    public int? RelatedThroughPersonId { get; set; }

    public KinshipKind? KinshipKind { get; set; }

    /// <summary>Grado dentro del límite legal (<c>LIMITE</c>).</summary>
    public byte? KinshipDegree { get; set; }

    /// <summary>Porcentaje de aportes, como fracción (C118; plantilla <c>lending.related-parties</c>).</summary>
    public decimal? ContributionPercentage { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public RelatedPartySource Source { get; set; } = RelatedPartySource.Manual;

    public Guid? SupportAttachmentPublicId { get; set; }

    public string? Notes { get; set; }

    public int? EndedByUserId { get; set; }

    public string? EndReason { get; set; }
}
