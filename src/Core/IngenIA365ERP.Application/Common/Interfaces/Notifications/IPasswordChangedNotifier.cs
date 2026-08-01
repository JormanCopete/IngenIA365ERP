namespace IngenIA365ERP.Application.Common.Interfaces.Notifications;

/// <summary>
/// Envía la notificación de seguridad post-cambio de contraseña (T079d).
/// Usa la plantilla <c>PasswordChangedNotification.html</c> con IP, user-agent
/// y fecha del cambio. Es fail-soft: si el envío falla el handler NO aborta
/// el cambio (la notificación es informativa, no transaccional).
/// </summary>
public interface IPasswordChangedNotifier
{
    Task NotifyAsync(
        string toEmail,
        string recipientName,
        DateTime occurredAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct);
}
