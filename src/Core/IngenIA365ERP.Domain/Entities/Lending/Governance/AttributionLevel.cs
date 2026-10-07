using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_AttributionLevels</c> (feature 013, T0213; data-model §3.2; FR-037, FR-038, FR-040): un nivel de la matriz, individual
/// (usuario con <see cref="PermissionCode"/>) o colegiado (<see cref="GoverningBodyId"/>). Hecho, hijo de la matriz (C42).
/// </summary>
public class AttributionLevel : AuditableEntity, IHechoInmutable
{
    public int MatrixId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>Mayor = más alto; único dentro de la matriz.</summary>
    public int Rank { get; init; }

    public AttributionLevelKind Kind { get; init; }

    /// <summary>Individual: <c>Lending.Decisions.Analyst</c>, <c>.Coordinator</c>, <c>.Management</c> o <c>.Exceptions</c>.</summary>
    public string? PermissionCode { get; init; }

    /// <summary>Colegiado: órgano con <c>DecidesCredit</c>.</summary>
    public int? GoverningBodyId { get; init; }

    /// <summary>Techo de monto consolidado por sujeto, en pesos; nulo sólo en el nivel más alto.</summary>
    public decimal? MaxConsolidatedAmount { get; init; }

    /// <summary>Adonde sube un puntaje bajo o una alerta (FR-037); exactamente uno por matriz.</summary>
    public bool IsExceptionLevel { get; init; }

    /// <summary>El consejo, forzado con vinculados (FR-040); exactamente uno, de un órgano <c>BoardOfDirectors</c>.</summary>
    public bool IsBoardLevel { get; init; }
}
