using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRelatedParties</c> (feature 013, K1; data-model §29.3, parte <c>related-parties</c>): una persona vinculada a la
/// cooperativa, con su fundamento. Hecho sin diff de auditoría. Al activar nace <c>LND_RelatedParties</c>; lo registrado a mano
/// antes prevalece. Las mismas columnas de la plantilla 2 (plantillas.md §2, §5.3.16).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationRelatedPartyRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public RelatedPartyBasis Basis { get; init; }

    /// <summary>La persona con la que se tiene el vínculo, cuando el fundamento la nombra.</summary>
    public int? RelatedToPersonId { get; init; }

    /// <summary>Con <c>Relative</c> (<c>claseDeParentesco</c>).</summary>
    public KinshipKind? KinshipKind { get; init; }

    /// <summary>Con <c>Relative</c> (<c>grado</c>).</summary>
    public int? KinshipDegree { get; init; }

    /// <summary>Con <c>SignificantContributor</c> (<c>porcentajeDeAportes</c>), como fracción.</summary>
    public decimal? ContributionPercentage { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }

    public string SupportReference { get; init; } = string.Empty;
}
