namespace IngenIA365ERP.Application.Notifications.Contracts;

/// <summary>
/// Empuja una notificación al cliente conectado vía push real-time. La impl
/// canónica es <c>SignalRNotificationPusher</c> en la API; vive como
/// contrato en Application para que el handler real (T118) no se acople a
/// Microsoft.AspNetCore.SignalR.
/// </summary>
public interface INotificationPusher
{
    /// <summary>
    /// Notifica al usuario destinatario que tiene una nueva entrada. El
    /// cliente Blazor refresca su contador y la lista.
    /// </summary>
    Task PushCreatedAsync(
        Guid recipientUserPublicId,
        Guid notificationPublicId,
        NotificationType type,
        string subject,
        CancellationToken ct);
}
