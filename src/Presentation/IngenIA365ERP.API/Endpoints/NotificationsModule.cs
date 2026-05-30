using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Notifications.ListMyNotifications;
using IngenIA365ERP.Application.Notifications.MarkNotification;
using MediatR;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T121 — Inbox del usuario actual (US6). Todos los endpoints operan sobre
/// las notificaciones DEL CALLER — no hay endpoint para que un admin liste
/// el inbox de otro usuario (por privacidad). Permiso genérico
/// <c>Notifications.ManageOwn</c> exigido para que un usuario sin permiso
/// (caso raro: built-in `ReadOnly` recibe pero no opera) reciba 404.
/// </summary>
public sealed class NotificationsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/", ListAsync).WithName("Notifications_ListMine")
            .RequirePermission("Notifications.ManageOwn");

        group.MapPost("/{publicId:guid}/read", MarkReadAsync)
            .WithName("Notifications_MarkRead")
            .RequirePermission("Notifications.ManageOwn");

        group.MapPost("/{publicId:guid}/archive", MarkArchivedAsync)
            .WithName("Notifications_MarkArchived")
            .RequirePermission("Notifications.ManageOwn");

        group.MapPost("/read-all", MarkAllReadAsync)
            .WithName("Notifications_MarkAllRead")
            .RequirePermission("Notifications.ManageOwn");
    }

    private static async Task<object?> ListAsync(
        ISender sender, bool? includeArchived, bool? onlyUnread, int? take, CancellationToken ct) =>
        await sender.Send(new ListMyNotificationsQuery(
            IncludeArchived: includeArchived ?? false,
            OnlyUnread: onlyUnread ?? false,
            Take: take ?? 100), ct);

    private static async Task<object?> MarkReadAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new MarkNotificationReadCommand(publicId), ct);

    private static async Task<object?> MarkArchivedAsync(
        Guid publicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new MarkNotificationArchivedCommand(publicId), ct);

    private static async Task<object?> MarkAllReadAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new MarkAllNotificationsReadCommand(), ct);
}
