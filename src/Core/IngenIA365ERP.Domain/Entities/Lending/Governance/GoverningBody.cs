using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_GoverningBodies</c> (feature 013, T0213; data-model §3.5; FR-039, FR-040, T64): un órgano de decisión (consejo, junta de
/// vigilancia, comités, gerencia). Catálogo con código de la cooperativa; no es <c>COR_Committees</c>. No es hecho.
/// </summary>
public class GoverningBody : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public GoverningBodyKind Kind { get; set; }

    public bool DecidesCredit { get; set; }

    /// <summary>Prefijo del acta; el consecutivo vive en <c>LND_NumberSequences</c> (<c>SessionMinutes</c>, ámbito = el órgano; C48).</summary>
    public string MinutesPrefix { get; set; } = string.Empty;

    /// <summary>Un solo <c>BoardOfDirectors</c> activo (lo valida el comando).</summary>
    public bool IsActive { get; set; } = true;
}
