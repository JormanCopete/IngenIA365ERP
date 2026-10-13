using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_RiskPolicyVersions</c> (feature 013, US9, T0696; data-model §23.10; FR-099, FR-103, FR-104, FR-106; T45, R11): la política SARC del
/// consejo, sobreescrituras del esquema parcial del Anexo 1 (<c>PoliticaSarc</c>) que <b>sólo endurecen</b> la norma celda por celda. Hecho
/// (<see cref="IHechoInmutable"/>): lo único que se escribe después, y una sola vez, es la validación con el punto de acta del consejo
/// (<see cref="EscrituraUnicaAttribute"/>). El estado se calcula (<see cref="Status"/>). (nuevo)
/// </summary>
public class RiskPolicyVersion : AuditableEntity, IHechoInmutable
{
    public int VersionNumber { get; init; }

    /// <summary>Rige desde aquí una vez validada; nunca antes de hoy.</summary>
    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>JSON canónico con las celdas que endurece.</summary>
    public string Overrides { get; init; } = "{}";

    /// <summary>Huella del contenido canónico; la sella el cierre.</summary>
    public string OverridesSha256 { get; init; } = string.Empty;

    /// <summary>La copia de <c>SarcAnexo1</c> contra la que se comparó cada celda (sin FK).</summary>
    public Guid BaseDocumentPublicId { get; init; }

    public string BaseDocumentSha256 { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    /// <summary>Con <c>Lending.RiskPolicy.Manage</c>.</summary>
    public int RegisteredByUserId { get; init; }

    [EscrituraUnica]
    public DateTime? ValidatedAt { get; set; }

    /// <summary>Con <c>Lending.RiskPolicy.Validate</c>; nunca quien la registró.</summary>
    [EscrituraUnica]
    public int? ValidatedByUserId { get; set; }

    /// <summary>El punto de acta del consejo (sujeto <c>RiskPolicyVersion</c>, C52).</summary>
    [EscrituraUnica]
    public int? SessionItemId { get; set; }

    /// <summary>Calculado: <c>PendingValidation</c> sin validación, <c>Validated</c> con ella.</summary>
    public RiskPolicyVersionStatus Status => ValidatedAt is null ? RiskPolicyVersionStatus.PendingValidation : RiskPolicyVersionStatus.Validated;
}
