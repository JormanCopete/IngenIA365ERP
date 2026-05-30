using IngenIA365ERP.Application.Notifications.Contracts;

namespace IngenIA365ERP.Application.Notifications.Common;

/// <summary>Proyección de una notificación para el inbox del usuario (US6).</summary>
public sealed record NotificationItemDto(
    Guid PublicId,
    NotificationType Type,
    string Subject,
    string Body,
    NotificationChannels Channels,
    string EmailStatus,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? ArchivedAt);

public sealed record NotificationInboxDto(
    IReadOnlyList<NotificationItemDto> Items,
    int UnreadCount,
    int TotalCount);

public static class NotificationErrorCodes
{
    public const string NotForCurrentUser = "Notifications.NotForCurrentUser";
}
