using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationAuthorizations</c> (feature 013, K1; data-model §29.3, parte <c>authorizations</c>): una autorización de
/// tratamiento de datos de una persona. Hecho sin diff de auditoría. Al activar nace <c>CMP_HabeasDataConsents</c> con
/// <c>Origin = Migration</c> por <c>IAutorizacionDeDatos.Agregar</c>; sin evidencia, el crédito sigue vivo y cobrable pero sin
/// centrales ni mensajes de cobranza (FR-006).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationAuthorizationRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    /// <summary>Nombre de <c>DataAuthorizationPurpose</c> (Compliance) como texto.</summary>
    public string Purpose { get; init; } = string.Empty;

    /// <summary>Canal de contacto autorizado (enum de Compliance como texto); nulo si la finalidad no lo distingue.</summary>
    public string? ContactChannel { get; init; }

    public DateOnly AuthorizedOn { get; init; }

    public string PolicyVersionText { get; init; } = string.Empty;

    public string? EvidenceReference { get; init; }

    public bool HasEvidence { get; init; }
}
