using IngenIA365ERP.Application.Notifications.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace IngenIA365ERP.API.Hubs;

/// <summary>
/// T120 — Implementación SignalR de <see cref="INotificationPusher"/>.
/// Publica el evento <c>notification.created</c> a los clientes conectados
/// al <see cref="NotificationsHub"/> que corresponden al destinatario.
///
/// <para>
/// El targeting se hace por <c>User identifier</c> de SignalR — en la auth
/// JWT se mapea al claim <c>sub</c> (User.PublicId) vía
/// <c>NameIdentifier</c>. Eso permite <c>Clients.User(publicId)</c> sin
/// mantener registry propio de connections.
/// </para>
/// </summary>
public sealed class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRNotificationPusher(IHubContext<NotificationsHub> hub)
    {
        _hub = hub;
    }

    public Task PushCreatedAsync(
        Guid recipientUserPublicId,
        Guid notificationPublicId,
        NotificationType type,
        string subject,
        CancellationToken ct)
    {
        var payload = new
        {
            publicId = notificationPublicId,
            type = type.ToString(),
            subject,
            occurredAt = DateTime.UtcNow
        };

        return _hub.Clients
            .User(recipientUserPublicId.ToString())
            .SendAsync("notification.created", payload, ct);
    }
}
