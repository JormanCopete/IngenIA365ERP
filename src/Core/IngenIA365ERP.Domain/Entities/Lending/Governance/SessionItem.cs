using System.Text.Json;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Sessions;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionItems</c> (feature 013, T0213; data-model §3.10; FR-039, FR-040, T64): un punto del acta sobre un sujeto, que decide
/// o sólo recomienda. Lo que la spec llama «con acta» referencia un punto por su <c>PublicId</c>. No es hecho: queda fijo al cerrar
/// la sesión, que sella el resultado, los votos contados y la decisión.
///
/// <para>
/// T0282: <see cref="Agregar"/> exige la sesión no cerrada, un sujeto de <see cref="LendingDecisionSubjects"/> y guarda la regla que
/// calculó el comando (<c>RelatedPartyQualified</c> con vinculado en entidad con actividad financiera) y los excluidos
/// (<c>[{personPublicId, motivo}]</c> canónico, <see cref="Excluidos"/>). <see cref="SellarResultado"/> y
/// <see cref="AsignarDecision"/> los escribe el cierre, con la sesión todavía abierta; después nada cambia.
/// </para>
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

    // ------------------------------------------------------------------------------------------------- comportamiento --

    /// <summary>Un punto nuevo de la sesión (abierta o en borrador).</summary>
    public static SessionItem Agregar(
        Session sesion, int order, SessionItemKind kind, VotingRule rule, string subjectType, Guid subjectPublicId, string title,
        IReadOnlyList<ExclusionDePunto> excluidos, string? summary = null, int? recommendsToBodyId = null)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(excluidos);
        ReglaDeCarteraException.SiHay(sesion.ExigirEditable());
        if (!LendingDecisionSubjects.Existe(subjectType))
            throw new ArgumentException($"«{subjectType}» no es un sujeto que decida un órgano de Cartera.", nameof(subjectType));
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfLessThan(order, 1);
        if (kind == SessionItemKind.Decision && recommendsToBodyId is not null)
            throw new ArgumentException("Un punto que decide no recomienda a otro órgano.", nameof(recommendsToBodyId));

        return new SessionItem
        {
            SessionId = sesion.Id,
            Order = order,
            Kind = kind,
            Rule = rule,
            SubjectType = subjectType,
            SubjectPublicId = subjectPublicId,
            Title = title.Trim(),
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary,
            ExcludedPersons = ExclusionDePunto.ComoJson(excluidos),
            RecommendsToBodyId = recommendsToBodyId,
        };
    }

    /// <summary>Los excluidos del punto, en el orden guardado.</summary>
    public IReadOnlyList<ExclusionDePunto> Excluidos() => ExclusionDePunto.DeJson(ExcludedPersons);

    /// <summary>El título y el resumen se corrigen mientras la sesión no esté cerrada.</summary>
    public void Corregir(Session sesion, string title, string? summary)
    {
        ExigirDeLaSesion(sesion);
        ReglaDeCarteraException.SiHay(sesion.ExigirEditable());
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Title = title.Trim();
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    /// <summary>
    /// Sella, al cerrar y con la sesión todavía abierta, lo que calculó <see cref="EvaluadorDeVotacion"/>: los conteos, la explicación
    /// y el resultado. Aprobado → <paramref name="resultadoSiAprueba"/> (con o sin condiciones); no aprobado → <c>Deny</c>. Un punto sin
    /// quórum no se sella: la sesión no se cierra sin quórum.
    /// </summary>
    public void SellarResultado(Session sesion, EvaluacionDeVotacion evaluacion, CreditDecisionOutcome resultadoSiAprueba = CreditDecisionOutcome.Approve,
        string? conditions = null)
    {
        ExigirDeLaSesion(sesion);
        ArgumentNullException.ThrowIfNull(evaluacion);
        ReglaDeCarteraException.SiHay(sesion.ExigirAbierta());
        if (evaluacion.Resultado == ResultadoDeVotacion.SinQuorum)
            ReglaDeCarteraException.SiHay(sesion.PuedeCerrar(evaluacion.Quorum));
        if (resultadoSiAprueba is not (CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions))
            throw new ArgumentException("Lo que se aprueba en un punto es «aprobar» o «aprobar con condiciones».", nameof(resultadoSiAprueba));

        VotesFor = evaluacion.AFavor;
        VotesAgainst = evaluacion.EnContra;
        Abstentions = evaluacion.Abstenciones;
        EvaluationExplanation = evaluacion.TextoDeLaExplicacion;
        Outcome = evaluacion.Resultado == ResultadoDeVotacion.Aprobado ? resultadoSiAprueba : CreditDecisionOutcome.Deny;
        Conditions = Outcome == CreditDecisionOutcome.ApproveWithConditions ? conditions : null;
    }

    /// <summary>La decisión que escribió el cierre (sólo los puntos que deciden).</summary>
    public void AsignarDecision(Session sesion, int decisionId)
    {
        ExigirDeLaSesion(sesion);
        ReglaDeCarteraException.SiHay(sesion.ExigirAbierta());
        if (Kind != SessionItemKind.Decision)
            throw new InvalidOperationException("Un punto que sólo recomienda no escribe una decisión.");
        if (Outcome is null) throw new InvalidOperationException("Primero se sella el resultado del punto.");
        ArgumentOutOfRangeException.ThrowIfLessThan(decisionId, 1);
        DecisionId = decisionId;
    }

    private void ExigirDeLaSesion(Session sesion)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        if (sesion.Id != SessionId) throw new ArgumentException("El punto es de otra sesión.", nameof(sesion));
    }
}
