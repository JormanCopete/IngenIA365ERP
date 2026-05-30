using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Common;
using IngenIA365ERP.Application.Notifications.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Notifications.ListMyNotifications;

/// <summary>
/// Inbox del usuario actual. El handler resuelve <c>UserPublicId</c> del
/// <see cref="ICurrentUserService"/> — los clientes NUNCA pueden listar
/// notificaciones de otros usuarios. Filtros opcionales: incluir archivadas,
/// solo no leídas.
/// </summary>
public sealed record ListMyNotificationsQuery(
    bool IncludeArchived = false,
    bool OnlyUnread = false,
    int Take = 100) : IRequest<Result<NotificationInboxDto>>;

public sealed class ListMyNotificationsQueryHandler
    : IRequestHandler<ListMyNotificationsQuery, Result<NotificationInboxDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ListMyNotificationsQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationInboxDto>> Handle(
        ListMyNotificationsQuery request, CancellationToken ct)
    {
        var recipient = await ResolveCurrentUserPublicIdAsync(ct);
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
                n.ArchivedAt))
            .ToListAsync(ct);

        return Result.Success(new NotificationInboxDto(items, unreadCount, totalCount));
    }

    private async Task<Guid?> ResolveCurrentUserPublicIdAsync(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return null;
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.PublicId)
            .FirstOrDefaultAsync(ct);
    }

    private static NotificationType ParseType(string raw) =>
        Enum.TryParse<NotificationType>(raw, out var t) ? t : NotificationType.Generic;
}
