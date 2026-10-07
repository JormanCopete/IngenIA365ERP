using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationChecks</c> (feature 013; data-model §2.4; FR-036, FR-038, FR-041, FR-042): cada validación de la solicitud
/// con su resultado y su evidencia. Hecho de sólo inserción (<see cref="IHechoInmutable"/>, C42): re-evaluar agrega filas y la vigente
/// es la última por (<see cref="Kind"/>, <see cref="PartyId"/>, <see cref="Stage"/>). Una <c>Blocking</c> impide decidir.
/// </summary>
public class CreditApplicationCheck : AuditableEntity, IHechoInmutable
{
    public int ApplicationId { get; init; }

    /// <summary>Nulo = validación de la solicitud, no de una parte.</summary>
    public int? PartyId { get; init; }

    public ApplicationCheckKind Kind { get; init; }

    public CheckStage Stage { get; init; }

    public CheckOutcome Outcome { get; init; }

    /// <summary>Falso en capacidad de pago, autorización, listas y usura (<c>NoExceptuables</c>).</summary>
    public bool IsExceptionable { get; init; }

    /// <summary>El código <c>Lending.*</c> que explica el resultado.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>JSON con las cifras: capacidad disponible, exceso sobre el límite y su fuente, cupo disponible.</summary>
    public string? Detail { get; init; }

    public int? ScreeningResultId { get; init; }

    public int? BureauQueryId { get; init; }

    public int? CreditScoreId { get; init; }

    /// <summary>Huella de entradas y resultado.</summary>
    public string EvidenceSha256 { get; init; } = string.Empty;

    public DateTime EvaluatedAt { get; init; }
}
