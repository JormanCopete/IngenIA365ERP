using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Compliance;

/// <summary>
/// T126 — Consentimiento (o revocación) habeas data de un titular para
/// una versión específica de la política (US7).
///
/// <para>
/// El historial completo del titular es la secuencia cronológica de filas
/// con el mismo <see cref="PersonId"/>. El estado vigente del titular es
/// la última fila (<c>OrderByDescending(ActionAt).First()</c>): si
/// <see cref="Action"/> = <c>Accepted</c> está dando consentimiento; si
/// <c>Revoked</c>, lo revocó.
/// </para>
///
/// <para>
/// Auditable y soft-delete pero NUNCA se modifica una fila ya creada —
/// publicar una revocación = añadir nueva fila con <c>Action=Revoked</c>.
/// </para>
/// </summary>
public class HabeasDataConsent : AuditableEntity
{
    public int TenantId { get; set; }

    /// <summary>Titular del dato (FK a <c>Person</c>).</summary>
    public int PersonId { get; set; }

    /// <summary>Versión de la política sobre la que se actúa.</summary>
    public int PolicyVersionId { get; set; }

    /// <summary>Acción: <c>Accepted</c> | <c>Revoked</c>.</summary>
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    public DateTime ActionAt { get; set; }

    /// <summary>Quién registró la acción (operador o el titular vía portal).</summary>
    [MaxLength(100)]
    public string ActionBy { get; set; } = string.Empty;

    /// <summary>Canal por el que se obtuvo: <c>Portal</c>, <c>Email</c>, <c>Print</c>, <c>InPerson</c>…</summary>
    [MaxLength(50)]
    public string? Channel { get; set; }

    /// <summary>Notas libres (motivo de revocación, referencia documental, etc.).</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation
    public HabeasDataPolicyVersion? PolicyVersion { get; set; }
}
