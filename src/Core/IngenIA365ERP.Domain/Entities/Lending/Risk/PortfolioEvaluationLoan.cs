using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_PortfolioEvaluationLoans</c> (feature 013, K3, T0872; data-model §23.7; FR-102): un crédito evaluado con la calificación que tenía por
/// las reglas al corte, la propuesta (y su subcategoría en E) y la justificación. Se edita sólo mientras la evaluación está en borrador
/// (<see cref="PortfolioEvaluation.EditarLinea"/>). Único por evaluación y crédito. No es hecho. (nuevo)
/// </summary>
public class PortfolioEvaluationLoan : AuditableEntity
{
    public int PortfolioEvaluationId { get; init; }

    public int LoanId { get; init; }

    /// <summary>La calificación por las reglas al corte.</summary>
    public RatingCategory RuleRatingAtCutoff { get; init; }

    public RatingCategory ProposedRating { get; private set; }

    /// <summary>Sólo en E.</summary>
    public RatingSubcategory? ProposedSubcategory { get; private set; }

    public string Justification { get; private set; } = string.Empty;

    public string? CriteriaScoresJson { get; private set; }

    public Guid? SupportAttachmentPublicId { get; private set; }

    /// <summary>Una línea nueva con su propuesta (al preparar la evaluación).</summary>
    public static PortfolioEvaluationLoan Nueva(int evaluacionId, int creditoId, RatingCategory porReglas, RatingCategory propuesta,
        RatingSubcategory? subcategoria, string justificacion, string? puntajesJson = null, Guid? soporte = null)
    {
        var linea = new PortfolioEvaluationLoan { PortfolioEvaluationId = evaluacionId, LoanId = creditoId, RuleRatingAtCutoff = porReglas };
        linea.Fijar(propuesta, subcategoria, justificacion, puntajesJson, soporte);
        return linea;
    }

    internal void Fijar(RatingCategory propuesta, RatingSubcategory? subcategoria, string justificacion, string? puntajesJson, Guid? soporte)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justificacion);
        if (subcategoria is { } s && s != RatingSubcategory.None && propuesta != RatingCategory.E)
            throw new ArgumentException("Sólo la categoría E lleva subcategoría.", nameof(subcategoria));
        ProposedRating = propuesta;
        ProposedSubcategory = subcategoria is RatingSubcategory.None ? null : subcategoria;
        Justification = justificacion.Trim();
        CriteriaScoresJson = puntajesJson;
        SupportAttachmentPublicId = soporte;
    }
}
