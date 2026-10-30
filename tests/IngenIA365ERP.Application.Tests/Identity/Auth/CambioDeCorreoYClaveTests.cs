using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Identity.Auth.CambioDeCorreo;
using IngenIA365ERP.Application.Identity.Auth.StepUp;
using IngenIA365ERP.Application.Identity.Correccion.CambiarCorreoPorMaestro;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Application.Security.Users.SendRecoveryLink;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// Feature 017 (T084, US7): sólo la persona cambia su correo —clave y segundo factor otra vez, y un código al
/// correo nuevo—; el maestro lo rescata con motivo y avisos a los dos; y el administrador ya no fija claves:
/// manda el enlace de recuperación, y sólo a un correo verificado. El olvido por usuario está en
/// <c>RequestPasswordResetCommandHandlerTests</c>.
/// </summary>
public class CambioDeCorreoYClaveTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Persona = Guid.NewGuid();
    private const string Correo = "ana@gmail.com";
    private const string Nuevo = "ana.perez@outlook.com";

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly ICurrentCentralUserContext _sesion = Substitute.For<ICurrentCentralUserContext>();
    private readonly IEmailSender _correo = Substitute.For<IEmailSender>();
    private readonly IIdentityEmailTemplates _plantillas = Substitute.For<IIdentityEmailTemplates>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private string? _codigoEnviado;

    public CambioDeCorreoYClaveTests()
    {
        _reloj.UtcNow.Returns(_ => Ahora);
        _sesion.IsAuthenticated.Returns(true);
        _sesion.CentralUserId.Returns(Persona);
        _sesion.Purpose.Returns(CentralJwtPurposes.StepUp);
        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Persona, Email = Correo, NormalizedEmail = Correo.ToUpperInvariant(), EmailConfirmed = true, TwoFactorEnabled = true });
        _identidad.ChangeEmailAsync(Persona, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, []));
        _plantillas.RenderAsync(default!, default!, default).ReturnsForAnyArgs(ci =>
        {
            var modelo = ci.ArgAt<IReadOnlyDictionary<string, string?>>(1);
            if (modelo.TryGetValue("Codigo", out var codigo)) _codigoEnviado = codigo;
            return "<p/>";
        });
    }

    private CorreosDeIdentidad Correos() => new(_correo, _plantillas, NullLogger<CorreosDeIdentidad>.Instance);
    private AuditoriaDeIdentidad Auditoria() => new(Substitute.For<IAuditAppendOnlyWriter>(), NullLogger<AuditoriaDeIdentidad>.Instance);
    private PropagadorDeIdentidad Propagador() => new(_db, Substitute.For<ITenantDbContextFactory>(), NullLogger<PropagadorDeIdentidad>.Instance);

    private SolicitarCambioDeCorreoCommandHandler Solicitar() => new(_sesion, _identidad, _db, Correos(), Auditoria(), _reloj);
    private ConfirmarCambioDeCorreoCommandHandler Confirmar() => new(_sesion, _identidad, _db, Propagador(), Correos(), Auditoria(), _reloj);

    // ---------- Step-up ----------

    [Fact]
    public async Task Pedir_el_cambio_exige_el_step_up_no_basta_la_sesion()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);

        var r = await Solicitar().Handle(new SolicitarCambioDeCorreoCommand(Nuevo), default);

        r.Error.Code.Should().Be("Identity.StepUpRequired");
        (await _db.EmailChangeRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_step_up_pide_clave_y_segundo_factor()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        var intentos = Substitute.For<ILoginAttemptCounter>();
        intentos.CheckAsync(default, default!, default).ReturnsForAnyArgs(new LoginLockoutState(false, 0, 0));
        var jwt = Substitute.For<ICentralJwtIssuer>();
        jwt.IssueChallengeToken(default, default!, default, default!, default, default)
            .ReturnsForAnyArgs(new CentralAccessTokenResult("token-step-up", Ahora.AddMinutes(5), "jti", CentralJwtPurposes.StepUp));
        var handler = new StepUpCommandHandler(_sesion, _identidad, intentos, jwt);
        _identidad.ValidatePasswordAsync(Persona, "clave-buena", Arg.Any<CancellationToken>()).Returns(true);
        _identidad.VerifyMfaCodeAsync(Persona, "123456", Arg.Any<CancellationToken>()).Returns(true);

        (await handler.Handle(new StepUpCommand("clave-mala", "123456"), default)).Error.Code.Should().Be("Identity.InvalidCredentials");
        (await handler.Handle(new StepUpCommand("clave-buena", "000000"), default)).Error.Code.Should().Be("Identity.MfaInvalid");
        var ok = await handler.Handle(new StepUpCommand("clave-buena", "123456"), default);

        ok.IsSuccess.Should().BeTrue(ok.IsFailure ? ok.Error.Message : null);
        jwt.Received(1).IssueChallengeToken(Persona, Correo, false, CentralJwtPurposes.StepUp, Arg.Any<MetodosMfa>(), TimeSpan.FromMinutes(5));
    }

    // ---------- Cambio de correo ----------

    [Fact]
    public async Task Un_correo_de_otra_cuenta_se_rechaza()
    {
        _identidad.FindByEmailAsync(Nuevo, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Guid.NewGuid(), Email = Nuevo, EmailConfirmed = true });

        var r = await Solicitar().Handle(new SolicitarCambioDeCorreoCommand(Nuevo), default);

        r.Error.Code.Should().Be("Identity.Email.Taken");
    }

    [Fact]
    public async Task Con_el_codigo_cambia_el_correo_rota_el_sello_y_avisa_al_anterior()
    {
        (await Solicitar().Handle(new SolicitarCambioDeCorreoCommand(Nuevo), default)).IsSuccess.Should().BeTrue();
        _codigoEnviado.Should().MatchRegex("^[0-9]{6}$");
        var guardado = await _db.EmailChangeRequests.SingleAsync();
        guardado.CodeHash.Should().NotBeEquivalentTo(System.Text.Encoding.UTF8.GetBytes(_codigoEnviado!), "el código se guarda hasheado");

        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        var r = await Confirmar().Handle(new ConfirmarCambioDeCorreoCommand(_codigoEnviado), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        // ChangeEmailAsync es quien rota el sello (lo prueba la e2e); aquí, que se llamó con el correo nuevo.
        await _identidad.Received(1).ChangeEmailAsync(Persona, Nuevo, Ahora, Arg.Any<CancellationToken>());
        await _correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == Correo), Arg.Any<CancellationToken>());
        (await _db.EmailChangeRequests.SingleAsync()).Status.Should().Be(EmailChangeStatus.Confirmed);
    }

    [Fact]
    public async Task Un_codigo_vencido_no_sirve()
    {
        await Solicitar().Handle(new SolicitarCambioDeCorreoCommand(Nuevo), default);
        _reloj.UtcNow.Returns(Ahora.AddMinutes(EmailChangeRequest.MinutosDeVigencia + 1));
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);

        var r = await Confirmar().Handle(new ConfirmarCambioDeCorreoCommand(_codigoEnviado), default);

        r.Error.Code.Should().Be("Identity.Email.CodeExpired");
        await _identidad.DidNotReceiveWithAnyArgs().ChangeEmailAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Agotados_los_intentos_ni_el_codigo_correcto_sirve()
    {
        await Solicitar().Handle(new SolicitarCambioDeCorreoCommand(Nuevo), default);
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        for (var i = 0; i < EmailChangeRequest.IntentosMaximos; i++)
            (await Confirmar().Handle(new ConfirmarCambioDeCorreoCommand("999999x"), default)).IsFailure.Should().BeTrue();

        var r = await Confirmar().Handle(new ConfirmarCambioDeCorreoCommand(_codigoEnviado), default);

        r.Error.Code.Should().Be("Identity.Email.CodeExpired");
    }

    // ---------- El maestro ----------

    [Fact]
    public async Task El_rescate_del_maestro_avisa_a_los_dos_correos_y_deja_el_registro()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        _sesion.IsGlobalMasterAdmin.Returns(true);
        _sesion.CentralUserId.Returns(Guid.NewGuid());
        var handler = new CambiarCorreoPorMaestroCommandHandler(_sesion, _identidad, _db, Propagador(), Correos(), Auditoria(), _reloj);

        var r = await handler.Handle(new CambiarCorreoPorMaestroCommand(Persona, Nuevo, "Perdió el buzón"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await _correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == Correo), Arg.Any<CancellationToken>());
        await _correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == Nuevo), Arg.Any<CancellationToken>());
        (await _db.EmailChangeRequests.SingleAsync()).ByMaster.Should().BeTrue();
    }

    [Fact]
    public async Task Quien_no_es_maestro_no_rescata_correos()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        var handler = new CambiarCorreoPorMaestroCommandHandler(_sesion, _identidad, _db, Propagador(), Correos(), Auditoria(), _reloj);

        var r = await handler.Handle(new CambiarCorreoPorMaestroCommand(Persona, Nuevo, "x"), default);

        r.Error.Code.Should().Be("Saas.MasterOnly");
    }

    // ---------- El enlace del administrador ----------

    [Fact]
    public async Task El_enlace_de_recuperacion_va_solo_a_un_correo_verificado()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        var cooperativa = TestDbContextFactory.Create();
        var fila = new Domain.Entities.Security.User { Username = Correo, Email = Correo, PasswordHash = "x", CentralUserId = Persona };
        cooperativa.Users.Add(fila);
        await cooperativa.SaveChangesAsync();
        var canal = Substitute.For<ICanalDeRecuperacion>();
        var handler = new SendRecoveryLinkCommandHandler(_sesion, cooperativa, _identidad, canal, Auditoria(), _reloj);

        (await handler.Handle(new SendRecoveryLinkCommand(fila.PublicId), default)).IsSuccess.Should().BeTrue();
        await canal.Received(1).EnviarEnlaceAsync(Arg.Is<CentralUser>(c => c.Id == Persona), null, Arg.Any<CancellationToken>());

        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Persona, Email = Correo, EmailConfirmed = false });
        var sinVerificar = await handler.Handle(new SendRecoveryLinkCommand(fila.PublicId), default);

        sinVerificar.Error.Code.Should().Be("Identity.Email.NotVerified");
        await canal.Received(1).EnviarEnlaceAsync(Arg.Any<CentralUser>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
