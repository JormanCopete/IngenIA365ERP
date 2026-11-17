using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>Qué hizo el modo de prueba del correo con un mensaje (<c>ParametrosDeCorreo</c>). (nuevo)</summary>
public enum HeldEmailMode
{
    /// <summary>No salió: el envío estaba en «Suspender».</summary>
    Suspended = 1,

    /// <summary>Salió al buzón de prueba en lugar del destinatario real.</summary>
    Redirected = 2,
}

/// <summary>En qué quedó un correo retenido o desviado. (nuevo)</summary>
public enum HeldEmailStatus
{
    /// <summary>Suspendido y pendiente de reenviar (con <c>Correo.SuspendidosQuedanPendientes</c>).</summary>
    Pending = 1,

    /// <summary>Suspendido y descartado: sólo queda la constancia.</summary>
    Discarded = 2,

    /// <summary>Entregado al buzón de prueba.</summary>
    Redirected = 3,

    /// <summary>Reenviado después a su destinatario real.</summary>
    Resent = 4,
}

/// <summary>
/// <c>COR_HeldEmails</c> (2026-11-17): la constancia de cada correo de la cooperativa que el modo de prueba no dejó llegar a su
/// destinatario —suspendido o desviado al buzón de prueba—. Si quedó pendiente guarda el mensaje entero (cuerpo y adjuntos) para
/// reenviarlo tal cual; si no, sólo a quién iba y el asunto. La escribe sólo <c>CorreoSegunModoDeEnvio</c>. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class HeldEmail : AuditableEntity
{
    public HeldEmailMode Mode { get; set; }

    public HeldEmailStatus Status { get; set; }

    /// <summary>El destinatario real.</summary>
    public string OriginalTo { get; set; } = string.Empty;

    /// <summary>Adonde fue en su lugar (el buzón de prueba), si se desvió.</summary>
    public string? RedirectedTo { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>El cuerpo, sólo mientras está pendiente de reenviar.</summary>
    public string? BodyHtml { get; set; }

    public string? BodyText { get; set; }

    public string? FromOverride { get; set; }

    /// <summary>Cuántos adjuntos traía (se guardan sólo si quedó pendiente).</summary>
    public int AttachmentCount { get; set; }

    public DateTime? ResentAt { get; set; }

    public string? ResentBy { get; set; }

    public List<HeldEmailAttachment> Attachments { get; set; } = [];
}

/// <summary><c>COR_HeldEmailAttachments</c>: un adjunto de un correo pendiente de reenviar. (nuevo)</summary>
[SinDiffDeAuditoria]
public class HeldEmailAttachment : AuditableEntity
{
    public int HeldEmailId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];
}
