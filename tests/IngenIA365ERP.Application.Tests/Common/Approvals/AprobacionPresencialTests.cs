using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Approvals.RequestPresenceChallenge;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// T085 (feature 012; T33; contracts/api.md §15.2): la aprobación presencial. Sólo desde la sesión de quien pidió;
/// el desafío dura dos minutos y se consume; la prueba es la passkey o el TOTP del aprobador, de un solo uso; toda falla
/// es 422 <c>Approvals.Presence.*</c>, nunca 401, y nunca se pide contraseña. El motor se reemplaza por uno falso: aquí
/// sólo se prueba la identificación del aprobador.
/// </summary>
public class AprobacionPresencialTests
{
    private const int Cajero = 11;
    private const int Supervisor = 30;
    private const string Huella = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CentralSupervisor = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IMotorDeAprobaciones _motor = Substitute.For<IMotorDeAprobaciones>();
    private readonly IDesafiosDePresencia _desafios = Substitute.For<IDesafiosDePresencia>();
    private readonly IMfaDirectory _credenciales = Substitute.For<IMfaDirectory>();
    private readonly IWebAuthnService _webAuthn = Substitute.For<IWebAuthnService>();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ApprovalRequest _solicitud;

    public AprobacionPresencialTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _solicitud = new ApprovalRequest
        {
            Subject = ApprovalSubjects.DiscountOverCap,
            SourceType = ApprovalSourceTypes.DocumentLineDiscount,
            SourcePublicId = Guid.NewGuid(),
            SourceLabel = "FV-000045",
            Amount = 12_000m,
            OperationDate = new DateOnly(2026, 10, 5),
            CreatedByUserId = Cajero,
            RequestedByUserId = Cajero,
            Status = ApprovalRequestStatus.Pending,
            CurrentLevel = 1,
            ContentSha256 = Huella,
            RequestedAt = Ahora,
        };
        _solicitud.SellarNiveles([new NivelDeAprobacion(1, 0m, "Inventory.Discounts.Authorize")]);
        _db.ApprovalRequests.Add(_solicitud);
        _db.Users.Add(new User { Id = Supervisor, Username = "supervisor", Email = "supervisor@coop.co", CentralUserId = CentralSupervisor, IsActive = true });
        _db.SaveChanges();
        ComoUsuario(Cajero);
        _motor.DecidirAsync(Arg.Any<DecisionDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DecisionResultDto(_solicitud.PublicId, "Approved", 1, new EstadoDeFuenteDto(_solicitud.SourcePublicId, null, null, null))));
        _credenciales.ListarCifradosTotpActivosAsync(CentralSupervisor, Arg.Any<CancellationToken>())
            .Returns([new CredencialTotpCifrada(Guid.NewGuid(), "cifrado")]);
        _credenciales.ListarWebAuthnActivasAsync(CentralSupervisor, Arg.Any<CancellationToken>())
            .Returns(new List<CredencialWebAuthnPermitida>());
    }

    private void ComoUsuario(int userId) =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            "cajero@coop.co", "cajero@coop.co", ExecutionChannel.Pos, "POST /api/inventory/approvals", null, null));

    private DecideApprovalCommandHandler Decidir() =>
        new(_motor, _db, _actor, _desafios, _credenciales, _webAuthn, _identidad, _reloj);

    private RequestPresenceChallengeCommandHandler PedirDesafio() =>
        new(_db, _actor, _credenciales, _webAuthn, _desafios, _reloj);

    private DesafioDePresencia Desafio(DateTime? vence = null, IReadOnlyList<string>? metodos = null) =>
        new(Guid.NewGuid(), _solicitud.PublicId, Cajero, Supervisor, CentralSupervisor, "supervisor@coop.co", metodos ?? ["Totp"], null, vence ?? Ahora.AddMinutes(2));

    private static DecideApprovalCommand ConTotp(Guid solicitud, Guid desafio, string codigo = "123456") =>
        new(solicitud, ApprovalDecisionKind.Approve, null, ApprovalMethod.InPersonTotp, new PresenciaDto(desafio, null, codigo), Huella);

    [Fact]
    public async Task El_desafio_se_pide_desde_la_sesion_del_solicitante_y_dura_dos_minutos()
    {
        var r = await PedirDesafio().Handle(new RequestPresenceChallengeCommand(_solicitud.PublicId, "supervisor@coop.co"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.ExpiresAt.Should().Be(Ahora.AddMinutes(2));
        r.Value.Methods.Should().Equal("Totp");
        r.Value.PublicKeyOptions.Should().BeNull("sin passkeys no hay opciones WebAuthn");
        await _desafios.Received(1).GuardarAsync(
            Arg.Is<DesafioDePresencia>(d => d.ApproverUserId == Supervisor && d.RequesterUserId == Cajero && d.RequestPublicId == _solicitud.PublicId),
            TimeSpan.FromMinutes(2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otra_sesion_no_pide_ni_usa_la_aprobacion_presencial()
    {
        ComoUsuario(Supervisor);

        var desafio = await PedirDesafio().Handle(new RequestPresenceChallengeCommand(_solicitud.PublicId, "supervisor@coop.co"), CancellationToken.None);
        var decision = await Decidir().Handle(ConTotp(_solicitud.PublicId, Guid.NewGuid()), CancellationToken.None);

        desafio.Error.Code.Should().Be("Approvals.Presence.NotRequester");
        decision.Error.Code.Should().Be("Approvals.Presence.NotRequester");
        await _motor.DidNotReceiveWithAnyArgs().DecidirAsync(default!, default);
    }

    [Fact]
    public async Task Un_aprobador_desconocido_es_Invalid_y_nunca_401()
    {
        var r = await PedirDesafio().Handle(new RequestPresenceChallengeCommand(_solicitud.PublicId, "nadie@coop.co"), CancellationToken.None);

        r.Error.Code.Should().Be("Approvals.Presence.Invalid");
    }

    [Fact]
    public async Task Un_desafio_vencido_o_ya_usado_es_Expired()
    {
        var vencido = Desafio(Ahora.AddSeconds(-1));
        _desafios.ConsumirAsync(vencido.PublicId, Arg.Any<CancellationToken>()).Returns(vencido);

        var r1 = await Decidir().Handle(ConTotp(_solicitud.PublicId, vencido.PublicId), CancellationToken.None);
        var r2 = await Decidir().Handle(ConTotp(_solicitud.PublicId, Guid.NewGuid()), CancellationToken.None);

        r1.Error.Code.Should().Be("Approvals.Presence.Expired");
        r2.Error.Code.Should().Be("Approvals.Presence.Expired");
    }

    [Fact]
    public async Task Un_TOTP_correcto_identifica_al_aprobador_y_el_motor_decide_con_el()
    {
        var d = Desafio();
        _desafios.ConsumirAsync(d.PublicId, Arg.Any<CancellationToken>()).Returns(d);
        _identidad.VerifyMfaCodeAsync(CentralSupervisor, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _desafios.MarcarTotpUsadoAsync(CentralSupervisor, "123456", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);

        var r = await Decidir().Handle(ConTotp(_solicitud.PublicId, d.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await _motor.Received(1).DecidirAsync(
            Arg.Is<DecisionDeAprobacion>(x => x.Presente!.UserId == Supervisor && x.Presente.Method == ApprovalMethod.InPersonTotp),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_TOTP_incorrecto_es_Invalid_y_uno_repetido_es_TotpReused()
    {
        var d1 = Desafio();
        var d2 = Desafio();
        _desafios.ConsumirAsync(d1.PublicId, Arg.Any<CancellationToken>()).Returns(d1);
        _desafios.ConsumirAsync(d2.PublicId, Arg.Any<CancellationToken>()).Returns(d2);
        _identidad.VerifyMfaCodeAsync(CentralSupervisor, "000000", Arg.Any<CancellationToken>()).Returns(false);
        _identidad.VerifyMfaCodeAsync(CentralSupervisor, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _desafios.MarcarTotpUsadoAsync(CentralSupervisor, "123456", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);

        var malo = await Decidir().Handle(ConTotp(_solicitud.PublicId, d1.PublicId, "000000"), CancellationToken.None);
        var repetido = await Decidir().Handle(ConTotp(_solicitud.PublicId, d2.PublicId), CancellationToken.None);

        malo.Error.Code.Should().Be("Approvals.Presence.Invalid");
        repetido.Error.Code.Should().Be("Approvals.Presence.TotpReused");
        await _motor.DidNotReceiveWithAnyArgs().DecidirAsync(default!, default);
    }

    [Fact]
    public async Task Una_passkey_de_otra_persona_es_Invalid()
    {
        var d = Desafio(metodos: ["Passkey"]) with { OpcionesWebAuthnJson = "{}" };
        _desafios.ConsumirAsync(d.PublicId, Arg.Any<CancellationToken>()).Returns(d);
        _credenciales.BuscarWebAuthnPorCredentialIdAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new CredencialWebAuthnGuardada(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3], 0));

        var r = await Decidir().Handle(new DecideApprovalCommand(_solicitud.PublicId, ApprovalDecisionKind.Approve, null, ApprovalMethod.InPersonPasskey,
            new PresenciaDto(d.PublicId, "{\"rawId\":\"AQID\"}", null), Huella), CancellationToken.None);

        r.Error.Code.Should().Be("Approvals.Presence.Invalid");
        await _webAuthn.DidNotReceiveWithAnyArgs().VerificarIngresoAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public void El_validador_exige_la_prueba_del_metodo_presencial()
    {
        var v = new DecideApprovalCommandValidator();
        var id = Guid.NewGuid();

        v.Validate(new DecideApprovalCommand(id, ApprovalDecisionKind.Approve, null, ApprovalMethod.InPersonTotp, null, Huella)).IsValid.Should().BeFalse();
        v.Validate(new DecideApprovalCommand(id, ApprovalDecisionKind.Approve, null, ApprovalMethod.InPersonTotp, new PresenciaDto(id, null, null), Huella))
            .IsValid.Should().BeFalse();
        v.Validate(new DecideApprovalCommand(id, ApprovalDecisionKind.Approve, null, ApprovalMethod.InPersonPasskey, new PresenciaDto(id, null, "1"), Huella))
            .IsValid.Should().BeFalse();
        v.Validate(ConTotp(id, id)).IsValid.Should().BeTrue();
        v.Validate(new DecideApprovalCommand(id, ApprovalDecisionKind.Approve, null, ApprovalMethod.OwnSession, null, "")).IsValid.Should().BeFalse("sin huella");
    }
}
