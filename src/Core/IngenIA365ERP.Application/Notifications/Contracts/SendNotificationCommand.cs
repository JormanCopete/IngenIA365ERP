using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Notifications.Contracts;

/// <summary>
/// Contrato estable que cualquier handler (US1, US2, …) puede emitir para
/// disparar una notificación. El binding por defecto es
/// <see cref="NoopSendNotificationHandler"/> hasta que T118 (US6) lo
/// reemplace por la implementación real (persiste in-app, encola correo,
/// publica push SignalR). Los emisores no cambian al hacer el swap.
/// </summary>
public sealed record SendNotificationCommand(NotificationPayload Payload) : IRequest<Result>;
