using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Common;
using IngenIA365ERP.Application.Notifications.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Notifications.ListMyNotifications;

/// <summary>
/// Inbox del usuario actual. El handler resuelve <c>UserPublicId</c> por
/// <see cref="IActorActual"/> (feature 012, T39, T095): la persona de <c>SEC_Users</c> de la
/// cooperativa activa, por la identidad central del token. Hasta entonces lo tomaba de
/// <c>ICurrentUserService.UserId</c>, que es nulo para un usuario de identidad central, y la
/// bandeja respondía <c>Auth.Unauthorized</c> a todos. Los clientes NUNCA pueden listar
/// notificaciones de otros usuarios. Filtros opcionales: incluir archivadas, solo no leídas.
/// </summary>
public sealed record ListMyNotificationsQuery(
    bool IncludeArchived = false,
    bool OnlyUnread = false,
    int Take = 100) : IRequest<Result<NotificationInboxDto>>;

public sealed class ListMyNotificationsQueryHandler
    : IRequestHandler<ListMyNotificationsQuery, Result<NotificationInboxDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IActorActual _actor;

    public ListMyNotificationsQueryHandler(
        IApplicationDbContext db, IActorActual actor)
    {
        _db = db;
        _actor = actor;
    }

    public async Task<Result<NotificationInboxDto>> Handle(
        ListMyNotificationsQuery request, CancellationToken ct)
    {
        var recipient = (await _actor.ObtenerAsync(ct)).UserPublicId;
        if (recipient is null)
        {
            return Result.Failure<NotificationInboxDto>(
                "Auth.Unauthorized",
                "El usuario actual no está identificado.");
        }

        var baseQuery = _db.Notifications
            .Where(n => n.RecipientUserPublicId == recipient.Value);

        var totalCount = await baseQuery.CountAsync(ct);
        var unreadCount = await baseQuery.CountAsync(n => n.ReadAt == null, ct);

        var filtered = baseQuery;
        if (!request.IncludeArchived) filtered = filtered.Where(n => n.ArchivedAt == null);
        if (request.OnlyUnread) filtered = filtered.Where(n => n.ReadAt == null);

        var take = request.Take is > 0 and <= 500 ? request.Take : 100;
        var items = await filtered
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationItemDto(
                n.PublicId,
                ParseType(n.Type),
                n.Subject,
                n.Body,
                (NotificationChannels)n.ChannelsMask,
                n.EmailStatus,
                n.CreatedAt,
                n.ReadAt,
                n.ArchivedAt,
                n.AlertPublicId))
            .ToListAsync(ct);

        return Result.Success(new NotificationInboxDto(items, unreadCount, totalCount));
    }

    private static NotificationType ParseType(string raw) =>
        Enum.TryParse<NotificationType>(raw, out var t) ? t : NotificationType.Generic;
}
