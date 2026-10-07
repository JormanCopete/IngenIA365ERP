using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_AttributionMatrices</c> (feature 013, T0213; data-model §3.1; FR-038, T63): una versión de la matriz de atribuciones,
/// publicada completa con sus niveles (<see cref="AttributionLevel"/>) y reglas (<see cref="AttributionRule"/>). Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>, C42) que se lee con <see cref="VigenciaBitemporal"/>; se sella en cada solicitud y en
/// cada sujeto que se enruta. Debe terminar en un nivel colegiado sin techo (<c>Lending.Attribution.MatrixWithoutTopLevel</c>).
/// </summary>
public class AttributionMatrix : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int VersionNumber { get; init; }

    /// <summary>Desde cuándo rige; no anterior a <c>HoyLocal</c> al publicar.</summary>
    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Fuente: el reglamento de crédito.</summary>
    public string? LegalSource { get; init; }

    /// <summary>Punto de acta que la aprobó, si la aprobó un órgano.</summary>
    public int? SessionItemId { get; init; }

    /// <summary>Huella canónica de la matriz con sus niveles y reglas.</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    public int RegisteredByUserId { get; init; }
}
