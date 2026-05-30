using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IngenIA365ERP.API.Hubs;

/// <summary>
/// Hub de SignalR (T031). Solo accesible con JWT válido. Al conectar, envía
/// al cliente <c>unreadCount</c> con la cantidad de notificaciones no leídas
/// (stub en 0 hasta que aterricen los productores reales en T118/T120).
///
/// El push <c>notification.created</c> se invoca desde el handler
/// <c>SendNotificationCommandHandler</c> en US6 cuando llegue T120 — esta
/// fase solo expone el endpoint y la auth.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Stub: el contador real lo entregará US6 (T118). De momento dejamos
        // el contrato del cliente fijo: siempre recibe `unreadCount` al
        // conectar, lo que evita branchings condicionales en el cliente.
        await Clients.Caller.SendAsync("unreadCount", 0);
        await base.OnConnectedAsync();
    }
}
