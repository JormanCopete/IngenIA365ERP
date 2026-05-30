namespace IngenIA365ERP.Application.Notifications.Contracts;

/// <summary>
/// Carga de una notificación a entregar al destinatario. Independiente del
/// canal: el handler real (T118, US6) decide cómo despacharla según
/// <see cref="Channels"/>. El stub <see cref="NoopSendNotificationHandler"/>
/// simplemente devuelve éxito.
/// </summary>
public sealed record NotificationPayload(
    Guid RecipientUserPublicId,
    NotificationType Type,
    string Subject,
    string Body,
    NotificationChannels Channels);

public enum NotificationType
{
    AccountLocked,
    PasswordChanged,
    MfaReset,
    RoleAssigned,
    SuspiciousSessionActivity,
    UserInvitationCreated,
    Generic
}

[Flags]
public enum NotificationChannels
{
    None = 0,
    InApp = 1 << 0,
    Email = 1 << 1
}
