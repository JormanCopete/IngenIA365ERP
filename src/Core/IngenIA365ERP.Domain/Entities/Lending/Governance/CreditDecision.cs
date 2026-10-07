using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_CreditDecisions</c> (feature 013, T0213; data-model §3.4; FR-038, FR-039, FR-042, FR-052, T63, T64): la decisión de un
/// nivel, individual (<see cref="DecidedByUserId"/>) o colegiada (<see cref="SessionItemId"/>; exactamente uno de los dos, CHECK
/// <c>CK_LND_CreditDecisions_Decider</c>), sobre cualquier sujeto. Hecho (<see cref="IHechoInmutable"/>): otra decisión la reemplaza
/// por <see cref="SupersedesDecisionId"/>.
/// </summary>
public class CreditDecision : AuditableEntity, IHechoInmutable
{
    /// <summary>Sujeto de decisión (<c>LendingDecisionSubjects</c>).</summary>
    public string SubjectType { get; init; } = string.Empty;

    public Guid SubjectPublicId { get; init; }

    /// <summary>Expediente (data-model §3.13).</summary>
    public Guid CaseRootPublicId { get; init; }

    /// <summary>Nulo en lo que no enruta la matriz (aceptación de diferencias, política SARC).</summary>
    public int? AttributionMatrixId { get; init; }

    public int? LevelId { get; init; }

    public CreditDecisionOutcome Outcome { get; init; }

    /// <summary>JSON: monto o plazo menores, garantía adicional.</summary>
    public string? Conditions { get; init; }

    /// <summary>Obligatorio en <c>Deny</c>, <c>Return</c> y en toda excepción (lo valida el comando).</summary>
    public string? Reason { get; init; }

    public bool IsPolicyException { get; init; }

    /// <summary>Huella de lo decidido (sujeto, monto consolidado, <c>RoutingSha256</c>, condiciones).</summary>
    public string DecisionSha256 { get; init; } = string.Empty;

    /// <summary>Decisión individual.</summary>
    public int? DecidedByUserId { get; init; }

    public int? DecidedByPersonId { get; init; }

    /// <summary>Decisión colegiada: la escribe el cierre del acta.</summary>
    public int? SessionItemId { get; init; }

    public DateTime DecidedAt { get; init; }

    /// <summary>Vigencia de una aprobación (FR-042).</summary>
    public DateOnly? ValidUntil { get; init; }

    /// <summary>Decisión anterior que ésta reemplaza (vencimiento o reenrutamiento).</summary>
    public int? SupersedesDecisionId { get; init; }
}
