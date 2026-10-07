using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionItems</c> (feature 013, T0213; data-model §3.10; FR-039, FR-040, T64): un punto del acta sobre un sujeto, que decide
/// o sólo recomienda. Lo que la spec llama «con acta» referencia un punto por su <c>PublicId</c>. No es hecho: queda fijo al cerrar
/// la sesión, que sella el resultado, los votos contados y la decisión.
/// </summary>
public class SessionItem : AuditableEntity
{
    public int SessionId { get; set; }

    public int Order { get; set; }

    public SessionItemKind Kind { get; set; }

    /// <summary>Calificada con vinculados en entidad con actividad financiera (FR-040).</summary>
    public VotingRule Rule { get; set; }

    /// <summary>Sujeto de decisión (<c>LendingDecisionSubjects</c>).</summary>
    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectPublicId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    /// <summary>JSON <c>[{personPublicId, motivo}]</c>: radicador, solicitante, persona del vínculo.</summary>
    public string? ExcludedPersons { get; set; }

    /// <summary>Recomendación a otro órgano (p. ej. riesgos → consejo).</summary>
    public int? RecommendsToBodyId { get; set; }

    /// <summary>Al cerrar.</summary>
    public CreditDecisionOutcome? Outcome { get; set; }

    /// <summary>JSON de condiciones, al cerrar.</summary>
    public string? Conditions { get; set; }

    public int? VotesFor { get; set; }

    public int? VotesAgainst { get; set; }

    public int? Abstentions { get; set; }

    /// <summary>Explicación de <c>EvaluadorDeVotacion</c> (quórum, base, fracción).</summary>
    public string? EvaluationExplanation { get; set; }

    /// <summary>Decisión escrita al cerrar (sólo puntos <c>Decision</c>).</summary>
    public int? DecisionId { get; set; }
}
