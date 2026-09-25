using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Audit;

namespace IngenIA365ERP.Domain.Entities.Audit;

/// <summary>
/// Un ancla de la cadena de auditoría (<c>COR_AuditAnchors</c>; feature 012, T38; data-model §23): el
/// hash de un punto de la cadena firmado con HMAC. Quien reescribiera la cadena entera en Mongo y en SQL
/// no podría rehacer las anclas sin la clave de <c>AuditSignature</c>. Hecho inmutable.
/// </summary>
[SinDiffDeAuditoria]
public class AuditAnchor : AuditableEntity, IHechoInmutable
{
    public string Stream { get; set; } = string.Empty;

    public AuditAnchorKind Kind { get; set; }

    public long Seq { get; set; }

    public string Hash { get; set; } = string.Empty;

    /// <summary>Sólo en <see cref="AuditAnchorKind.Daily"/>: el día anclado.</summary>
    public DateOnly? AnchorDate { get; set; }

    /// <summary>HMAC-SHA256 en Base64 de <c>stream|seq|hash|anchoredAt</c>.</summary>
    public string Hmac { get; set; } = string.Empty;

    /// <summary>Versión de la clave de anclaje (<c>AuditSignature:AnchorKeyVersion</c>; nunca <c>dev-v1</c>).</summary>
    public string KeyVersion { get; set; } = string.Empty;

    public DateTime AnchoredAt { get; set; }
}
