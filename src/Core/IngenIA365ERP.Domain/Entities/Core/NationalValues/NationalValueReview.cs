using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.NationalValues;

/// <summary>
/// <c>COR_NationalValueReviews</c> (feature 013, T41, R7; data-model parte 3 §4.4): la conformidad del contador o del
/// oficial de cumplimiento con una copia. No modifica la copia y no bloquea el cálculo: sin revisión la copia está
/// «pendiente de validar». Si hay varias, vale la más reciente (por <see cref="ReviewedAt"/>).
/// </summary>
public class NationalValueReview : AuditableEntity, IHechoInmutable
{
    public NationalValueReviewTarget TargetKind { get; init; }

    /// <summary><c>PublicId</c> de la copia revisada (<c>COR_NationalValues</c> o <c>COR_RegulatoryDocuments</c>); sin FK.</summary>
    public Guid TargetPublicId { get; init; }

    public NationalValueReviewDecision Decision { get; init; }

    /// <summary>Obligatoria en <see cref="NationalValueReviewDecision.Objected"/>.</summary>
    public string? Note { get; init; }

    /// <summary><c>SEC_Users.Id</c> de quien revisó, resuelto por <c>IActorActual</c>.</summary>
    public int ReviewedByUserId { get; init; }

    public string ReviewedByName { get; init; } = string.Empty;

    public DateTime ReviewedAt { get; init; }
}
