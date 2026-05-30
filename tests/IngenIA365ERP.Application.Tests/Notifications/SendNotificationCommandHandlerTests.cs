using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Notifications.SendNotification;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Notifications;

/// <summary>
/// T112 — Tests del handler real (US6) que reemplaza el Noop stub.
/// Cubre persistencia in-app, encolado de email (Status=Pending) y push
/// real-time vía <see cref="INotificationPusher"/>.
/// </summary>
public class SendNotificationCommandHandlerTests
{
    private static (SendNotificationCommandHandler Handler, TestApplicationDbContext Db,
                    INotificationPusher Pusher)
        Build(string? tenantId = "1")
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("system@test");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        var pusher = Substitute.For<INotificationPusher>();
        return (new SendNotificationCommandHandler(
            db, cu, clock,
            NullLogger<SendNotificationCommandHandler>.Instance,
            pusher), db, pusher);
    }

    [Fact]
    public async Task Persists_notification_with_pending_email_when_email_channel_set()
    {
        var (handler, db, _) = Build();
        var recipient = Guid.NewGuid();

        var result = await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                RecipientUserPublicId: recipient,
                Type: NotificationType.AccountLocked,
                Subject: "Cuenta bloqueada",
                Body: "5 intentos fallidos. Espera 15 minutos.",
                Channels: NotificationChannels.InApp | NotificationChannels.Email)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = db.Notifications.Single();
        saved.TenantId.Should().Be(1);
        saved.RecipientUserPublicId.Should().Be(recipient);
        saved.Type.Should().Be("AccountLocked");
        saved.EmailStatus.Should().Be("Pending");
        saved.ChannelsMask.Should().Be((int)(NotificationChannels.InApp | NotificationChannels.Email));
    }

    [Fact]
    public async Task Marks_EmailStatus_Disabled_when_email_channel_not_requested()
    {
        var (handler, db, _) = Build();

        await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                Guid.NewGuid(), NotificationType.RoleAssigned,
                "Nuevo rol", "Te asignaron CajaJunior",
                Channels: NotificationChannels.InApp)),
            CancellationToken.None);

        db.Notifications.Single().EmailStatus.Should().Be("Disabled");
    }

    [Fact]
    public async Task Pushes_real_time_event_when_InApp_channel_set()
    {
        var (handler, _, pusher) = Build();
        var recipient = Guid.NewGuid();

        await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                recipient, NotificationType.PasswordChanged,
                "Cambio de contraseña", "Tu contraseña fue cambiada.",
                NotificationChannels.InApp)),
            CancellationToken.None);

        await pusher.Received(1).PushCreatedAsync(
            recipient,
            Arg.Any<Guid>(),
            NotificationType.PasswordChanged,
            "Cambio de contraseña",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_push_when_only_Email_channel_set()
    {
        var (handler, _, pusher) = Build();

        await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                Guid.NewGuid(), NotificationType.SuspiciousSessionActivity,
                "Actividad sospechosa", "Detectamos login desde nueva IP.",
                NotificationChannels.Email)),
            CancellationToken.None);

        await pusher.DidNotReceive().PushCreatedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Persists_with_tenant_zero_when_no_tenant_in_context()
    {
        // Caso: handler de Login dispara `AccountLocked` antes de emitir el
        // JWT con tenant_id. El handler NO debe perder el evento.
        var (handler, db, _) = Build(tenantId: null);

        var result = await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                Guid.NewGuid(), NotificationType.AccountLocked,
                "Cuenta bloqueada", "Demasiados intentos.",
                NotificationChannels.Email)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.Notifications.Single().TenantId.Should().Be(0);
    }

    [Fact]
    public async Task Push_failure_does_not_break_handler()
    {
        // El push real-time es best-effort: un hub caído NO debe abortar el
        // flujo del emisor (la notificación in-app sigue visible al refresh).
        var (handler, db, pusher) = Build();
        pusher
            .When(p => p.PushCreatedAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<NotificationType>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("SignalR caído"));

        var result = await handler.Handle(new SendNotificationCommand(
            new NotificationPayload(
                Guid.NewGuid(), NotificationType.Generic,
                "Test", "Body",
                NotificationChannels.InApp)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        db.Notifications.Should().HaveCount(1);
    }
}
