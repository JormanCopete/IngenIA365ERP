using FluentAssertions;
using IngenIA365ERP.API.Hubs;
using IngenIA365ERP.Application.Notifications.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace IngenIA365ERP.API.IntegrationTests.Notifications;

/// <summary>
/// T114 — Validación estructural del push SignalR (US6, T120). NO requiere
/// Docker: instancia <see cref="SignalRNotificationPusher"/> con un
/// <see cref="IHubContext{T}"/> fake en proceso y verifica que el evento
/// se despacha al usuario correcto con el payload esperado.
///
/// El test end-to-end (cliente WebSocket conectado recibe el evento) vive
/// en una suite Playwright separada que necesita la API corriendo.
/// </summary>
public class SignalRPushTests
{
    [Fact]
    public async Task Pusher_sends_to_correct_user_with_payload_shape()
    {
        var capturedProxy = new RecordingClientProxy();
        var clients = new RecordingHubClients(capturedProxy);
        var hubCtx = new RecordingHubContext(clients);

        var pusher = new SignalRNotificationPusher(hubCtx);
        var recipient = Guid.NewGuid();
        var notification = Guid.NewGuid();

        await pusher.PushCreatedAsync(
            recipientUserPublicId: recipient,
            notificationPublicId: notification,
            type: NotificationType.RoleAssigned,
            subject: "Rol asignado",
            ct: CancellationToken.None);

        clients.LastUserId.Should().Be(recipient.ToString());
        capturedProxy.LastMethod.Should().Be("notification.created");
        capturedProxy.LastArgs.Should().NotBeNull();
        capturedProxy.LastArgs!.Length.Should().Be(1);
    }

    // === Mocks manuales (mantenemos IntegrationTests libre de NSubstitute) ===

    private sealed class RecordingClientProxy : IClientProxy
    {
        public string? LastMethod { get; private set; }
        public object?[]? LastArgs { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
        {
            LastMethod = method;
            LastArgs = args;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly RecordingClientProxy _proxy;
        public string? LastUserId { get; private set; }
        public RecordingHubClients(RecordingClientProxy proxy) => _proxy = proxy;

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excluded) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy Group(string groupName) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) { LastUserId = userId; return _proxy; }
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class RecordingHubContext : IHubContext<NotificationsHub>
    {
        public RecordingHubContext(IHubClients clients) { Clients = clients; Groups = new NoOpGroupManager(); }
        public IHubClients Clients { get; }
        public IGroupManager Groups { get; }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default) => Task.CompletedTask;
    }
}
