using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// T115 — Bitácora de intentos fallidos de entrega por correo (US6).
/// Cada fallo del dispatcher se persiste para diagnóstico operativo. El
/// in-app de la notificación sigue visible aunque el correo falle — los
/// fallos viven aparte porque su ciclo de vida es distinto (best-effort
/// retry vs durabilidad del log).
/// </summary>
public class NotificationDeliveryFailure : AuditableEntityLong
{
    public long NotificationId { get; set; }

    [MaxLength(20)]
    public string Channel { get; set; } = "Email";

    public int AttemptNumber { get; set; }

    [MaxLength(2000)]
    public string ErrorMessage { get; set; } = string.Empty;

    public DateTime FailedAt { get; set; }

    public Notification? Notification { get; set; }
}
