using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditScores</c> (feature 013; data-model §2.5; FR-037): el puntaje de una persona en una solicitud con el modelo de la
/// versión de la línea, explicado variable por variable. Hecho de sólo inserción (<see cref="IHechoInmutable"/>).
/// </summary>
public class CreditScore : AuditableEntity, IHechoInmutable
{
    public int ApplicationId { get; init; }

    public int PersonId { get; init; }

    /// <summary>La versión de la línea cuyo modelo de puntaje se usó (§1.3).</summary>
    public int CreditLineVersionId { get; init; }

    public decimal Score { get; init; }

    public decimal? MinimumScore { get; init; }

    /// <summary>Por debajo del mínimo: sube la solicitud al nivel de excepción (FR-037).</summary>
    public bool BelowMinimum { get; init; }

    /// <summary>JSON por variable: valor, rango, puntos, peso y aporte.</summary>
    public string Explanation { get; init; } = "[]";

    public string InputsSha256 { get; init; } = string.Empty;

    public DateTime CalculatedAt { get; init; }
}
