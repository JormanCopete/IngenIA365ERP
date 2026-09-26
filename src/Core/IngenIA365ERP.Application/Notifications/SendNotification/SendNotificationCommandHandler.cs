using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Notifications.SendNotification;

/// <summary>
/// T118 — Reemplaza <c>NoopSendNotificationHandler</c>. Crea el registro
/// en <c>COR_Notifications</c> y, si <see cref="NotificationPayload.Channels"/>
/// incluye <c>InApp</c>, publica el push en el hub SignalR (T120). El email
/// queda en cola (EmailStatus=Pending) — el background dispatcher (T119)
/// lo recoge y reintenta con backoff.
///
/// <para>
/// Si no hay tenant en el contexto (por ejemplo, fallo en handler de Login
/// antes de emitir tokens), se persiste con <c>TenantId = 0</c> y el push
/// se omite. La motivación: nunca queremos perder un evento de auditoría
/// de seguridad porque el usuario aún no estaba "anclado" a un tenant.
/// </para>
/// </summary>
public sealed class SendNotificationCommandHandler
    : IRequestHandler<SendNotificationCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly INotificationPusher? _pusher;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    public SendNotificationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ILogger<SendNotificationCommandHandler> logger,
        INotificationPusher? pusher = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
        _pusher = pusher;
    }

    public async Task<Result> Handle(SendNotificationCommand request, CancellationToken ct)
    {
        var payload = request.Payload;
        var tenantId = int.TryParse(_currentUser.TenantId, out var t) ? t : 0;
        var hasEmail = payload.Channels.HasFlag(NotificationChannels.Email);
        var hasInApp = payload.Channels.HasFlag(NotificationChannels.InApp);

        var actor = _currentUser.UserName ?? "SYSTEM";
        var notification = new Notification
        {
            TenantId = tenantId,
            RecipientUserPublicId = payload.RecipientUserPublicId,
            Type = payload.Type.ToString(),
            Subject = payload.Subject,
            Body = payload.Body,
            ChannelsMask = (int)payload.Channels,
            EmailStatus = hasEmail ? "Pending" : "Disabled",
            AlertPublicId = payload.AlertPublicId,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        if (hasInApp && _pusher is not null)
        {
            try
            {
                await _pusher.PushCreatedAsync(
                    payload.RecipientUserPublicId,
                    notification.PublicId,
                    payload.Type,
                    payload.Subject,
                    ct);
            }
            catch (Exception ex)
            {
                // El push real-time es best-effort: si SignalR está caído, la
                // notificación in-app aún aparece en el inbox del usuario al
                // siguiente refresh. NO bloqueamos el flujo emisor.
                _logger.LogWarning(ex,
                    "Push SignalR falló para notificación {PublicId} (usuario {User}); el inbox sigue funcionando.",
                    notification.PublicId, payload.RecipientUserPublicId);
            }
        }

        return Result.Success();
    }
}
