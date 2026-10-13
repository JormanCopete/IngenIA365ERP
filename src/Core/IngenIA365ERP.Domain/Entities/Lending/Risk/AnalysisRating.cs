using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_AnalysisRatings</c> (feature 013, US9, T0696; data-model §23.6; FR-101): la calificación por análisis de un crédito, que el motor
/// usa como una candidata más (causa <c>Analysis</c>): nunca mejora la de las reglas. Una vigente por crédito. No es hecho: se levanta una vez
/// (<see cref="Levantar"/>) y el cierre la lee a R —creada hasta R y no levantada hasta R—. (nuevo)
/// </summary>
public class AnalysisRating : AuditableEntity
{
    public int LoanId { get; init; }

    public RatingCategory Rating { get; init; }

    public RatingSubcategory Subcategory { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    /// <summary>Obligatorio.</summary>
    public string Reason { get; init; } = string.Empty;

    public Guid? SupportAttachmentPublicId { get; init; }

    public int SetByUserId { get; init; }

    public DateTime SetAt { get; init; }

    public DateTime? LiftedAt { get; private set; }

    public int? LiftedByUserId { get; private set; }

    public string? LiftReason { get; private set; }

    public DateOnly? LiftedEffectiveFrom { get; private set; }

    /// <summary>Vigente: no se ha levantado.</summary>
    public bool Vigente => LiftedAt is null && !IsDeleted;

    /// <summary>¿Rige a R? Creada hasta R y no levantada hasta R.</summary>
    public bool VigenteA(DateTime informacionHasta) => !IsDeleted && SetAt <= informacionHasta && (LiftedAt is not { } l || l > informacionHasta);

    /// <summary>La levanta una persona con motivo; se escribe una sola vez.</summary>
    public void Levantar(int usuarioId, DateTime ahora, DateOnly desde, string motivo)
    {
        if (LiftedAt is not null) throw new InvalidOperationException("La calificación por análisis ya se levantó.");
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        LiftedAt = ahora;
        LiftedByUserId = usuarioId;
        LiftReason = motivo.Trim();
        LiftedEffectiveFrom = desde;
    }
}
