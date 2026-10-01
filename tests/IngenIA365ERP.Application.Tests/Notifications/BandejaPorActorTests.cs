using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Notifications.ListMyNotifications;
using IngenIA365ERP.Application.Notifications.MarkNotification;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Notifications;

/// <summary>
/// Feature 012, T095 (T39): la bandeja de notificaciones resuelve al usuario por <see cref="IActorActual"/> y no por
/// <c>ICurrentUserService.UserId</c>, que es nulo para un usuario de identidad central (hasta entonces respondía
/// <c>Auth.Unauthorized</c> a todos). Una alerta entregada trae su <c>AlertPublicId</c>.
/// </summary>
public class BandejaPorActorTests
{
    private static readonly Guid Yo = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly ICurrentUserService _usuarioDelToken = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public BandejaPorActorTests()
    {
        _usuarioDelToken.UserId.Returns((int?)null);
        _reloj.UtcNow.Returns(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Yo, Guid.NewGuid(), "Compras Uno", "compras@coop.test", ExecutionChannel.Web, "/api/notifications", null, null));
    }

    [Fact]
    public async Task Con_el_UserId_del_token_nulo_la_bandeja_es_la_de_la_persona_del_actor()
    {
        var alerta = Guid.NewGuid();
        _db.Notifications.AddRange(
            new Notification { RecipientUserPublicId = Yo, Type = nameof(NotificationType.Alert), Subject = "Quiebre", Body = "b", AlertPublicId = alerta },
            new Notification { RecipientUserPublicId = Guid.NewGuid(), Type = "Generic", Subject = "ajena", Body = "b" });
        await _db.SaveChangesAsync();

        var r = await new ListMyNotificationsQueryHandler(_db, _actor).Handle(new ListMyNotificationsQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var item = r.Value.Items.Should().ContainSingle().Subject;
        item.Type.Should().Be(NotificationType.Alert);
        item.AlertPublicId.Should().Be(alerta);
    }

    [Fact]
    public async Task Sin_persona_resuelta_sigue_siendo_Auth_Unauthorized()
    {
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion("Tarea:x"));

        var r = await new ListMyNotificationsQueryHandler(_db, _actor).Handle(new ListMyNotificationsQuery(), CancellationToken.None);

        r.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Marcar_leida_usa_la_persona_del_actor()
    {
        var mia = new Notification { RecipientUserPublicId = Yo, Type = "Generic", Subject = "s", Body = "b" };
        _db.Notifications.Add(mia);
        await _db.SaveChangesAsync();

        var r = await new MarkNotificationReadHandler(_db, _usuarioDelToken, _actor, _reloj)
            .Handle(new MarkNotificationReadCommand(mia.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await _db.Notifications.SingleAsync()).ReadAt.Should().NotBeNull();
    }
}
