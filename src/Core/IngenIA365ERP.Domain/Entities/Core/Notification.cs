using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// T115 — Notificación a un usuario (US6). Persiste el log canónico en
/// <c>[dbo].[COR_Notifications]</c>. Mapea 1:1 al <c>NotificationPayload</c>
/// de Application — el handler real (T118) crea una fila por destinatario y
/// la cola del email dispatcher la lee para enviar correo según
/// <see cref="EmailStatus"/>.
/// </summary>
public class Notification : AuditableEntityLong
{
    public int TenantId { get; set; }

    /// <summary>PublicId del User destinatario (Principio VI).</summary>
    public Guid RecipientUserPublicId { get; set; }

    /// <summary>Tipo canónico (AccountLocked, PasswordChanged, RoleAssigned…).</summary>
    [MaxLength(80)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Canales a despachar: combinación InApp/Email.</summary>
    public int ChannelsMask { get; set; }

    /// <summary>Estado de la entrega por correo (Pending/Sent/Failed/Disabled).</summary>
    [MaxLength(20)]
    public string EmailStatus { get; set; } = "Pending";

    public DateTime? EmailSentAt { get; set; }

    public int EmailAttemptCount { get; set; }

    /// <summary>Marcada leída por el destinatario en la UI (in-app).</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Archivada por el destinatario.</summary>
    public DateTime? ArchivedAt { get; set; }
}
