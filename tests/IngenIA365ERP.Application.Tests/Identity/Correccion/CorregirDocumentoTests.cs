using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Identity.Correccion.CorregirDocumento;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Correccion;

/// <summary>
/// Feature 017 (T072, US6): corregir el documento. El administrador corrige a quien sólo está en su
/// cooperativa y el servidor lo decide; el maestro no tiene ese límite; queda el historial con lo anterior,
/// el usuario se recalcula y la persona recibe el aviso.
/// </summary>
public class CorregirDocumentoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid Coop = Guid.NewGuid();
    private static readonly Guid Otra = Guid.NewGuid();

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly ICurrentCentralUserContext _sesion = Substitute.For<ICurrentCentralUserContext>();
    private readonly IEmailSender _correo = Substitute.For<IEmailSender>();

    public CorregirDocumentoTests()
    {
        _sesion.IsAuthenticated.Returns(true);
        _sesion.CentralUserId.Returns(Actor);
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>()).Returns(new CentralUser
        {
            Id = Persona, Email = "ana@gmail.com", FirstName = "Ana", LastName = "Pérez",
            DocumentType = "TI", DocumentNumber = "1012345678", DocumentUsername = "TI1012345678",
        });
        _identidad.SetDocumentAsync(Persona, Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, []));
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Persona, Coop, false, Actor, Ahora));
        _db.SaveChanges();
    }

    private CorreccionDeDocumento Correccion()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        var plantillas = Substitute.For<IIdentityEmailTemplates>();
        plantillas.RenderAsync(default!, default!, default).ReturnsForAnyArgs("<p/>");
        return new CorreccionDeDocumento(
            _sesion, _identidad, _db,
            new PropagadorDeIdentidad(_db, Substitute.For<ITenantDbContextFactory>(), NullLogger<PropagadorDeIdentidad>.Instance),
            new CorreosDeIdentidad(_correo, plantillas, NullLogger<CorreosDeIdentidad>.Instance),
            new AuditoriaDeIdentidad(Substitute.For<IAuditAppendOnlyWriter>(), NullLogger<AuditoriaDeIdentidad>.Instance),
            reloj, NullLogger<CorreccionDeDocumento>.Instance);
    }

    private static CorregirDocumentoCommand Datos(string motivo = "Update") =>
        new(Guid.Empty, "CC", "1.012.345.678", "1012345678", motivo, "Cumplió la mayoría de edad", null);

    [Fact]
    public async Task La_actualizacion_deja_el_historial_con_lo_anterior_y_avisa_a_la_persona()
    {
        var r = await Correccion().CorregirAsync(Persona, DocumentChangeActor.TenantAdmin, Coop, Datos(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.DocumentUsername.Should().Be("1012345678");
        r.Value.PreviousUsername.Should().Be("TI1012345678");
        var h = await _db.DocumentHistory.SingleAsync();
        h.Reason.Should().Be(DocumentChangeReason.Update);
        h.ActorKind.Should().Be(DocumentChangeActor.TenantAdmin);
        h.OldUsername.Should().Be("TI1012345678");
        h.NewUsername.Should().Be("1012345678");
        await _correo.Received(1).SendAsync(Arg.Is<EmailMessage>(m => m.To == "ana@gmail.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_mismo_documento_no_es_una_correccion()
    {
        var r = await Correccion().CorregirAsync(Persona, DocumentChangeActor.TenantAdmin, Coop,
            new CorregirDocumentoCommand(Guid.Empty, "TI", "1012345678", "1012345678", "Correction", null, null), default);

        r.Error.Code.Should().Be("Identity.Document.Unchanged");
    }

    [Fact]
    public async Task Un_documento_ajeno_abre_un_caso_y_no_cambia_nada()
    {
        _identidad.SetDocumentAsync(Persona, Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(false, ["Identity.DuplicateDocument"]));

        var r = await Correccion().CorregirAsync(Persona, DocumentChangeActor.PlatformMaster, null, Datos(), default);

        r.Error.Code.Should().Be("Identity.Document.Unavailable");
        (await _db.IdentityCases.SingleAsync()).Kind.Should().Be(IdentityCaseKind.DocumentTaken);
        (await _db.DocumentHistory.CountAsync()).Should().Be(0);
    }

    // ---------- Quién puede ----------

    private CorregirDocumentoCommandHandler HandlerDelAdministrador(Guid? vinculo)
    {
        _sesion.ActiveTenantPublicId.Returns(Coop);
        var cooperativa = TestDbContextFactory.Create();
        var usuario = new Domain.Entities.Security.User { Username = "ana@gmail.com", Email = "ana@gmail.com", PasswordHash = "x", CentralUserId = vinculo };
        cooperativa.Users.Add(usuario);
        cooperativa.SaveChanges();
        _publicIdDelUsuario = usuario.PublicId;
        return new CorregirDocumentoCommandHandler(_sesion, _identidad, _db, cooperativa, Correccion());
    }

    private Guid _publicIdDelUsuario;

    [Fact]
    public async Task El_administrador_corrige_a_quien_solo_esta_en_su_cooperativa()
    {
        var handler = HandlerDelAdministrador(Persona);

        var r = await handler.Handle(Datos() with { UserPublicId = _publicIdDelUsuario }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public async Task Con_otra_cooperativa_el_administrador_recibe_PlatformOnly()
    {
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Persona, Otra, false, Actor, Ahora));
        await _db.SaveChangesAsync();
        var handler = HandlerDelAdministrador(Persona);

        var r = await handler.Handle(Datos() with { UserPublicId = _publicIdDelUsuario }, default);

        r.Error.Code.Should().Be("Identity.Document.PlatformOnly");
        await _identidad.DidNotReceive().SetDocumentAsync(Arg.Any<Guid>(), Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_maestro_corrige_sin_limite_de_cooperativas()
    {
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Persona, Otra, false, Actor, Ahora));
        await _db.SaveChangesAsync();
        _sesion.IsGlobalMasterAdmin.Returns(true);
        var handler = new CorregirDocumentoPorMaestroCommandHandler(_sesion, Correccion());

        var r = await handler.Handle(new CorregirDocumentoPorMaestroCommand(Persona, Datos("Correction")), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        (await _db.DocumentHistory.SingleAsync()).ActorKind.Should().Be(DocumentChangeActor.PlatformMaster);
    }

    [Fact]
    public async Task Quien_no_es_maestro_no_usa_la_ruta_del_maestro()
    {
        var handler = new CorregirDocumentoPorMaestroCommandHandler(_sesion, Correccion());

        var r = await handler.Handle(new CorregirDocumentoPorMaestroCommand(Persona, Datos()), default);

        r.Error.Code.Should().Be("Saas.MasterOnly");
    }
}
