using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Compliance;

/// <summary>
/// T126 — Versión publicada de la política habeas data del tenant (US7).
///
/// <para>
/// El tenant publica versiones sucesivas con <see cref="VersionNumber"/>
/// monótono creciente. Cuando una nueva versión se publica, el handler
/// (T128) cierra la anterior fijándole <see cref="EffectiveTo"/> = hora de
/// publicación de la nueva. En todo momento existe a lo sumo UNA versión
/// vigente (<see cref="EffectiveTo"/> nulo) por tenant.
/// </para>
///
/// <para>
/// <see cref="Sha256Hex"/> se calcula sobre <see cref="ContentMarkdown"/>
/// al publicar — permite probar que el texto vigente en un consent
/// específico no ha sido alterado posteriormente.
/// </para>
/// </summary>
public class HabeasDataPolicyVersion : AuditableEntity
{
    public int TenantId { get; set; }

    /// <summary>Número incremental por tenant (1, 2, 3, …).</summary>
    public int VersionNumber { get; set; }

    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Texto canónico de la política (Markdown).</summary>
    public string ContentMarkdown { get; set; } = string.Empty;

    /// <summary>SHA-256 hex del <see cref="ContentMarkdown"/> al publicar.</summary>
    [MaxLength(64)]
    public string Sha256Hex { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    /// <summary><c>null</c> = vigente. Se llena al publicar la siguiente versión.</summary>
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(100)]
    public string PublishedBy { get; set; } = string.Empty;
}
