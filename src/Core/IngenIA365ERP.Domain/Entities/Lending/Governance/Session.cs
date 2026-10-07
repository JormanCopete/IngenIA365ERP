using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_Sessions</c> (feature 013, T0213; data-model §3.8; FR-039, FR-040, FR-095, FR-102, FR-105, FR-141, D17, T64): la sesión de
/// un órgano con su acta, <c>Draft → Open → Closed</c>. Documento con estado, no hecho; cerrada no cambia
/// (<c>Lending.Session.Closed</c>) y se corrige con otra sesión.
/// </summary>
public class Session : AuditableEntity
{
    public int GoverningBodyId { get; set; }

    /// <summary>Prefijo del órgano + consecutivo, asignado al cerrar; nulo mientras no esté cerrada.</summary>
    public string? Number { get; set; }

    public SessionKind Kind { get; set; }

    public SessionMode Mode { get; set; }

    /// <summary>Fecha de la sesión; puede ser anterior al registro (acta digitada después).</summary>
    public DateOnly HeldOn { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? Location { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Draft;

    /// <summary>Regla de quórum y mayorías sellada al abrir: la vigente en <see cref="HeldOn"/>.</summary>
    public int? RuleVersionId { get; set; }

    /// <summary>Sellado al abrir y al cerrar.</summary>
    public int? CompositionCount { get; set; }

    public int? QuorumRequired { get; set; }

    public int? PresentEligibleCount { get; set; }

    public bool? QuorumMet { get; set; }

    /// <summary>Desarrollo de la sesión.</summary>
    public string? Notes { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    /// <summary>Huella canónica del acta al cerrar.</summary>
    public string? ContentSha256 { get; set; }

    /// <summary>PDF del acta emitido al cerrar.</summary>
    public Guid? MinutesAttachmentPublicId { get; set; }
}
