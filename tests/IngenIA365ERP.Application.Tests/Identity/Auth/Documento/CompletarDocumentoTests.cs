using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Identity.Auth.Documento;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth.Documento;

/// <summary>
/// Feature 017 (T065, US5): la persona que ya tenía cuenta completa su documento, y la exigencia por fecha
/// de cada cooperativa. Lo que más importa de la guardia es lo que NO hace: quien ya tiene documento y la
/// cooperativa sin fecha (el defecto) nunca se bloquean, así que el despliegue no toca a nadie.
/// </summary>
public class CompletarDocumentoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 11, 15);
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid Coop = Guid.NewGuid();

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly ITenantMembershipReader _membresias = Substitute.For<ITenantMembershipReader>();
    private readonly ICurrentCentralUserContext _sesion = Substitute.For<ICurrentCentralUserContext>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public CompletarDocumentoTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(DateOnly.FromDateTime(Ahora.AddHours(-5)));
        _sesion.IsAuthenticated.Returns(true);
        _sesion.CentralUserId.Returns(Persona);
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Persona, Email = "ana@gmail.com" });
        _membresias.GetActiveMembershipsAsync(Persona, Arg.Any<CancellationToken>())
            .Returns([new ActiveMembershipInfo(Coop, "Coop A", false, false)]);
    }

    // ---------- La guardia ----------

    [Fact]
    public void Con_documento_nunca_se_bloquea_aunque_la_fecha_haya_pasado() =>
        GuardiaDeDocumento.Evaluar(true, [Hoy.AddDays(-30)], Hoy).Should().Be(GuardiaDeDocumento.Veredicto.Admite);

    [Fact]
    public void Una_cooperativa_sin_fecha_nunca_bloquea() =>
        GuardiaDeDocumento.Evaluar(false, [null, null], Hoy).Should().Be(GuardiaDeDocumento.Veredicto.Admite);

    [Fact]
    public void Antes_de_la_fecha_admite_y_desde_la_fecha_exige()
    {
        GuardiaDeDocumento.Evaluar(false, [Hoy.AddDays(1)], Hoy).Should().Be(GuardiaDeDocumento.Veredicto.Admite);
        GuardiaDeDocumento.Evaluar(false, [Hoy], Hoy).Should().Be(GuardiaDeDocumento.Veredicto.FaltaDocumento);
        GuardiaDeDocumento.Evaluar(false, [null, Hoy.AddDays(-1)], Hoy).Should().Be(GuardiaDeDocumento.Veredicto.FaltaDocumento);
    }

    [Fact]
    public async Task La_exigencia_lee_la_fecha_de_la_politica_y_no_consulta_si_ya_tiene_documento()
    {
        var politica = TenantIdentityPolicy.CreateForTenant(Coop);
        politica.Cambiar(["CC", "CE"], Hoy.AddDays(-1), 3);
        _db.TenantIdentityPolicies.Add(politica);
        await _db.SaveChangesAsync();
        var exigencia = new ExigenciaDeDocumento(_db, _reloj);

        (await exigencia.EvaluarAsync(new CentralUser { Id = Persona }, [Coop], default))
            .Should().Be(GuardiaDeDocumento.Veredicto.FaltaDocumento);
        (await exigencia.EvaluarAsync(new CentralUser { Id = Persona, DocumentUsername = "1234567" }, [Coop], default))
            .Should().Be(GuardiaDeDocumento.Veredicto.Admite);
        (await exigencia.EvaluarAsync(new CentralUser { Id = Persona }, [Guid.NewGuid()], default))
            .Should().Be(GuardiaDeDocumento.Veredicto.Admite, "otra cooperativa sin política no exige nada");
    }

    // ---------- Registrar ----------

    private RegistrarDocumentoCommandHandler Handler() => new(
        _sesion, _identidad, _membresias, _db,
        new PropagadorDeIdentidad(_db, Substitute.For<ITenantDbContextFactory>(), NullLogger<PropagadorDeIdentidad>.Instance),
        new AuditoriaDeIdentidad(Substitute.For<IAuditAppendOnlyWriter>(), NullLogger<AuditoriaDeIdentidad>.Instance),
        _reloj, NullLogger<RegistrarDocumentoCommandHandler>.Instance);

    [Fact]
    public async Task Registrar_un_documento_libre_deja_el_usuario_y_el_historial()
    {
        _identidad.SetDocumentAsync(Persona, Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, []));

        var r = await Handler().Handle(new RegistrarDocumentoCommand("CC", "1.234.567", "1234567"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.DocumentUsername.Should().Be("1234567");
        var h = await _db.DocumentHistory.SingleAsync();
        h.Reason.Should().Be(DocumentChangeReason.Registration);
        h.ActorKind.Should().Be(DocumentChangeActor.Person);
        h.NewUsername.Should().Be("1234567");
    }

    [Fact]
    public async Task Un_documento_tomado_no_dice_de_quien_es_y_abre_un_caso()
    {
        var duena = Guid.NewGuid();
        _identidad.SetDocumentAsync(Persona, Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(false, ["Identity.DuplicateDocument"]));
        _identidad.FindByDocumentUsernameAsync("1234567", Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = duena, Email = "otra@gmail.com" });

        var r = await Handler().Handle(new RegistrarDocumentoCommand("CC", "1234567", "1234567"), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Identity.Document.Unavailable");
        r.Error.Message.Should().NotContain("otra@gmail.com");
        var caso = await _db.IdentityCases.SingleAsync();
        caso.Kind.Should().Be(IdentityCaseKind.DocumentTaken);
        caso.OtherCentralUserId.Should().Be(duena);
        (await _db.DocumentHistory.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Las_dos_digitaciones_tienen_que_coincidir()
    {
        var r = await Handler().Handle(new RegistrarDocumentoCommand("CC", "1234567", "1234568"), default);

        r.Error.Code.Should().Be("Identity.Document.Mismatch");
        await _identidad.DidNotReceive().SetDocumentAsync(Arg.Any<Guid>(), Arg.Any<DocumentoValidado>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_tipo_tiene_que_aceptarlo_cada_cooperativa_activa()
    {
        var politica = TenantIdentityPolicy.CreateForTenant(Coop);
        politica.Cambiar(["CC"], null, 3);
        _db.TenantIdentityPolicies.Add(politica);
        await _db.SaveChangesAsync();

        var r = await Handler().Handle(new RegistrarDocumentoCommand("CE", "987654", "987654"), default);

        r.Error.Code.Should().Be("Identity.Document.TypeNotAccepted");
    }

    [Fact]
    public async Task Quien_ya_tiene_documento_no_lo_cambia_por_aqui()
    {
        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Persona, Email = "ana@gmail.com", DocumentUsername = "1234567" });

        var r = await Handler().Handle(new RegistrarDocumentoCommand("CC", "7654321", "7654321"), default);

        r.Error.Code.Should().Be("Identity.Document.AlreadyRegistered");
    }

    [Fact]
    public async Task Sirve_con_el_desafio_de_documento_y_no_con_otro_desafio()
    {
        _sesion.Purpose.Returns(CentralJwtPurposes.TenantSelect);

        var r = await Handler().Handle(new RegistrarDocumentoCommand("CC", "1234567", "1234567"), default);

        r.Error.Code.Should().Be("Identity.WrongTokenPurpose");
    }
}
