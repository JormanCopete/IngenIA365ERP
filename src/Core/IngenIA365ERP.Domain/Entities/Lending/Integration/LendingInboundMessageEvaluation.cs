using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Integration;

/// <summary>
/// <c>LND_InboundMessageEvaluations</c> (feature 013, C.3; data-model §21.2; FR-051, FR-052): una evaluación de un mensaje
/// recibido; un reproceso agrega otra. Hecho de sólo inserción (<see cref="IHechoInmutable"/>).
/// </summary>
public class LendingInboundMessageEvaluation : AuditableEntity, IHechoInmutable
{
    public int InboundMessageId { get; init; }

    public int EvaluationNumber { get; init; }

    /// <summary>Fecha a la que se evalúa: la de la venta (FR-051).</summary>
    public DateOnly AsOfDate { get; init; }

    /// <summary>Instante R de la evaluación.</summary>
    public DateTime EvaluatedAt { get; init; }

    public InboundEvaluationOutcome Outcome { get; init; }

    /// <summary>Línea resuelta: la del mensaje, la sugerida por el medio o la de defecto del tipo de tercero (FR-050).</summary>
    public string? CreditLineCode { get; init; }

    /// <summary>JSON con cada verificación de FR-051, su resultado y su evidencia.</summary>
    public string ChecksJson { get; init; } = "[]";

    public string? FailureCode { get; init; }

    public string? FailureReason { get; init; }

    /// <summary><c>NotConvertible</c> = verdadero; <c>NotIncorporated</c> = falso.</summary>
    public bool Retryable { get; init; }

    /// <summary>Texto (<c>Postable</c>, <c>NotPostable</c>, <c>NoResponse</c>): lo que respondió la validación previa contable (T56).</summary>
    public string? PrevalidationOutcome { get; init; }

    /// <summary><c>ObligacionRechazada</c> de esta evaluación (clave <c>Confirmation:{evaluationPublicId:N}</c>, T18, C95).</summary>
    public Guid? ResponseMessagePublicId { get; init; }
}
