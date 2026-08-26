using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.MfaReset;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

/// <summary>
/// El reset por doble aprobación tenía dos fallos encadenados, y estas pruebas
/// cubrían el terreno de forma que ninguno se veía:
///
/// <list type="number">
/// <item>Quien llama se resolvía con <c>ICurrentUserService.UserId</c>, que
/// parsea como <c>int</c> un claim que el emisor central no pone. Devolvía
/// <c>null</c> siempre, así que las rutas respondían 401 a cualquiera. Las
/// pruebas no lo veían porque inyectaban el sustituto ya resuelto.</item>
/// <item>El reset limpiaba <c>MfaSecret</c> sobre <c>SEC_Users</c> —el modelo de
/// Fase 0— cuando el login verifica contra <c>ADM_CentralUsers</c>. Las pruebas
/// afirmaban esa mutación, es decir, afirmaban el defecto.</item>
/// </list>
///
/// Ahora quien llama se siembra con su puente <c>CentralUserId</c>, como en
/// producción, y lo que se verifica es la llamada a la identidad central.
/// </summary>
public class MfaResetApproveCommandHandlerTests
{
    private const int IdObjetivo = 20;
    private static readonly Guid CentralIdDelObjetivo = GuidDe(IdObjetivo);

    /// <summary>Guid determinista por Id de SEC_Users, para no depender del azar.</summary>
    private static Guid GuidDe(int userId) => new($"00000000-0000-0000-0000-{userId:D12}");

    private static (ApproveMfaResetCommandHandler Handler, TestApplicationDbContext Db,
                    ISender Mediator, ICentralIdentityProvider Identidad)
        Build(int currentUserId, DateTime? now = null)
    {
        var db = TestDbContextFactory.Create();

        // Quien llama tiene que existir en SEC_Users con su puente a la
        // identidad central: es por ahí por donde el handler lo resuelve.
        db.Users.Add(new User
        {
            Id = currentUserId,
            Username = "approver-" + currentUserId,
            Email = $"approver-{currentUserId}@coop.co",
            PasswordHash = "h",
            IsActive = true,
            CentralUserId = GuidDe(currentUserId),
        });
        db.SaveChanges();

        var central = Substitute.For<ICurrentCentralUserContext>();
        central.CentralUserId.Returns(GuidDe(currentUserId));

        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now ?? new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc));

        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(IngenIA365ERP.Application.Common.Models.Result.Success());

        var identidad = Substitute.For<ICentralIdentityProvider>();

        return (new ApproveMfaResetCommandHandler(db, central, clock, mediator, identidad),
                db, mediator, identidad);
    }

    private static (MfaResetRequest Entry, User Target) SeedRequest(
        TestApplicationDbContext db, int requesterId = 10,
        DateTime? expiresAt = null, bool mfaEnabled = true, bool conPuenteCentral = true)
    {
        var target = new User
        {
            Id = IdObjetivo,
            Username = "target",
            Email = "target@coop.co",
            PasswordHash = "h",
            IsActive = true,
            IsMfaEnabled = mfaEnabled,
            MfaSecret = "protected-secret",
            CentralUserId = conPuenteCentral ? CentralIdDelObjetivo : null,
        };
        db.Users.Add(target);

        var entry = new MfaResetRequest
        {
            UserId = IdObjetivo,
            RequestedBy = requesterId,
            RequestedAt = new DateTime(2026, 5, 28, 11, 0, 0, DateTimeKind.Utc),
            Reason = "Pérdida del dispositivo",
            ExpiresAt = expiresAt ?? new DateTime(2026, 5, 29, 11, 0, 0, DateTimeKind.Utc),
            Status = MfaResetStatus.Pending
        };
        db.MfaResetRequests.Add(entry);
        db.SaveChanges();
        return (entry, target);
    }

    [Fact]
    public async Task Sin_sesion_central_devuelve_Unauthorized()
    {
        var db = TestDbContextFactory.Create();
        var central = Substitute.For<ICurrentCentralUserContext>();
        central.CentralUserId.Returns((Guid?)null);
        var clock = Substitute.For<IDateTimeService>();
        var handler = new ApproveMfaResetCommandHandler(
            db, central, clock,
            Substitute.For<ISender>(), Substitute.For<ICentralIdentityProvider>());

        var result = await handler.Handle(new ApproveMfaResetCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Generic.Unauthorized");
    }

    [Fact]
    public async Task Cannot_approve_own_request()
    {
        var (handler, db, _, _) = Build(currentUserId: 10);
        var (entry, _) = SeedRequest(db, requesterId: 10);

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetCannotApproveOwnRequest");
    }

    [Fact]
    public async Task First_approval_marks_first_approver_and_returns_Approved()
    {
        var (handler, db, _, _) = Build(currentUserId: 30);
        var (entry, _) = SeedRequest(db);

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Approved");
        var stored = db.MfaResetRequests.Single();
        stored.FirstApproverId.Should().Be(30);
        stored.SecondApproverId.Should().BeNull();
        stored.Status.Should().Be(MfaResetStatus.Pending);
    }

    [Fact]
    public async Task Same_user_cannot_approve_twice()
    {
        var (handler, db, _, _) = Build(currentUserId: 30);
        var (entry, _) = SeedRequest(db);
        entry.FirstApproverId = 30;
        entry.FirstApprovalAt = DateTime.UtcNow;
        db.SaveChanges();

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetAlreadyApprovedBySameUser");
    }

    [Fact]
    public async Task Second_distinct_approval_executes_reset_and_notifies()
    {
        var (handler, db, mediator, identidad) = Build(currentUserId: 40);
        var (entry, _) = SeedRequest(db);
        entry.FirstApproverId = 30;
        entry.FirstApprovalAt = DateTime.UtcNow;
        db.SaveChanges();

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Executed");

        // Lo que de verdad importa: el segundo factor que verifica el login vive
        // en la identidad central. Sin esta llamada la persona sigue bloqueada
        // por más que la solicitud diga "Executed" y salga el correo.
        await identidad.Received(1).ResetMfaAsync(CentralIdDelObjetivo, Arg.Any<CancellationToken>());

        var stored = db.MfaResetRequests.Single();
        stored.Status.Should().Be(MfaResetStatus.Executed);
        stored.SecondApproverId.Should().Be(40);

        await mediator.Received(1).Send(
            Arg.Is<SendNotificationCommand>(c => c.Payload.Type == NotificationType.MfaReset),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_puente_a_identidad_central_falla_y_no_marca_ejecutada()
    {
        var (handler, db, mediator, identidad) = Build(currentUserId: 40);
        var (entry, _) = SeedRequest(db, conPuenteCentral: false);
        entry.FirstApproverId = 30;
        entry.FirstApprovalAt = DateTime.UtcNow;
        db.SaveChanges();

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetSinIdentidadCentral");

        // Nada de decir "listo" sin haber hecho nada: la solicitud sigue viva
        // para reintentarse, no se resetea nada, y no sale el correo que le
        // diría a la persona que ya puede volver a inscribirse.
        var stored = db.MfaResetRequests.Single();
        stored.Status.Should().Be(MfaResetStatus.Pending);
        stored.ExecutedAt.Should().BeNull();
        await identidad.DidNotReceiveWithAnyArgs().ResetMfaAsync(default, default);
        await mediator.DidNotReceive().Send(
            Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_after_24h_returns_expired()
    {
        var (handler, db, _, _) = Build(currentUserId: 30,
            now: new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc));
        var (entry, _) = SeedRequest(db,
            expiresAt: new DateTime(2026, 5, 29, 11, 0, 0, DateTimeKind.Utc));

        var result = await handler.Handle(new ApproveMfaResetCommand(entry.PublicId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaResetExpired");
        db.MfaResetRequests.Single().Status.Should().Be(MfaResetStatus.Expired);
    }
}
