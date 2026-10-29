using FluentAssertions;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Invitations;

/// <summary>
/// Feature 017 (T051): la emisión de una invitación anclada al documento. El administrador ve el mismo
/// resultado haya o no cuenta, la invitación sale al correo de la identidad y nunca al digitado cuando
/// la persona ya existe, y el correo de otra persona se rechaza sin decir de quién es.
/// </summary>
public class InvitacionConDocumentoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly Tenant _coop;

    public InvitacionConDocumentoTests()
    {
        _coop = new Tenant { Name = "Coop A", SchemaName = "coop_a", IsActive = true };
        _db.Tenants.Add(_coop);
        _db.SaveChanges();
    }

    private EmisionDeInvitacion Emision()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        return new EmisionDeInvitacion(_db, _identidad, Substitute.For<ITenantDbContextFactory>(),
            new SecureTokenGenerator(), reloj, Options.Create(new IdentityEmailOptions()));
    }

    private static DatosDelInvitado Datos(string correo = "ana@gmail.com", string tipo = "CC", string numero = "1.234.567",
        string? confirmacion = null) =>
        new(correo, tipo, numero, confirmacion ?? numero, "Ana", null, "Pérez", null, "3001234567");

    private static CentralUser Identidad(Guid id, string correo, string? usuario, CentralUserStatus estado = CentralUserStatus.Active) => new()
    {
        Id = id, Email = correo, NormalizedEmail = correo.ToUpperInvariant(), Status = estado,
        DocumentType = usuario is null ? null : "CC", DocumentNumber = usuario, DocumentUsername = usuario,
        FirstName = "Ana", LastName = "Pérez",
    };

    [Fact]
    public async Task Documento_nuevo_crea_una_identidad_pendiente_y_sale_al_correo_digitado()
    {
        var nueva = Guid.NewGuid();
        _identidad.CreatePendingUserAsync(Arg.Any<NuevaIdentidadPendiente>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCentralUserResult(true, nueva, []));
        _identidad.FindByIdAsync(nueva, Arg.Any<CancellationToken>())
            .Returns(Identidad(nueva, "ana@gmail.com", "1234567", CentralUserStatus.Pending));

        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Invitacion.Email.Should().Be("ana@gmail.com");
        r.Value.Invitacion.CentralUserId.Should().Be(nueva);
        r.Value.Invitacion.DocumentNumber.Should().Be("1234567");
        r.Value.Invitacion.MaxDocumentAttempts.Should().Be(TenantIdentityPolicy.IntentosPorDefecto);
        await _identidad.Received(1).CreatePendingUserAsync(
            Arg.Is<NuevaIdentidadPendiente>(d => d.Documento.Usuario == "1234567" && d.Email == "ana@gmail.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Documento_existente_sale_al_correo_de_la_identidad_no_al_digitado()
    {
        var ana = Guid.NewGuid();
        _identidad.FindByDocumentUsernameAsync("1234567", Arg.Any<CancellationToken>())
            .Returns(Identidad(ana, "ana.personal@outlook.com", "1234567"));

        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(correo: "otro@gmail.com"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Invitacion.Email.Should().Be("ana.personal@outlook.com");
        r.Value.Invitacion.TypedEmail.Should().Be("otro@gmail.com");
        await _identidad.DidNotReceive().CreatePendingUserAsync(Arg.Any<NuevaIdentidadPendiente>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_correo_de_otra_persona_se_rechaza_sin_decir_de_quien_es()
    {
        _identidad.FindByEmailAsync("pedro@gmail.com", Arg.Any<CancellationToken>())
            .Returns(Identidad(Guid.NewGuid(), "pedro@gmail.com", "99887766"));

        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(correo: "pedro@gmail.com"), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Invitation.EmailTaken");
        r.Error.Message.Should().Be("Correo ya registrado.");
    }

    [Fact]
    public async Task Los_dos_numeros_deben_coincidir()
    {
        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(numero: "1234567", confirmacion: "1234568"), default);

        r.Error.Code.Should().Be("Identity.Document.Mismatch");
    }

    [Fact]
    public async Task Un_tipo_que_la_cooperativa_no_acepta_se_rechaza()
    {
        var politica = TenantIdentityPolicy.CreateForTenant(_coop.PublicId);
        politica.Cambiar(["CC"], null, 3);
        _db.TenantIdentityPolicies.Add(politica);
        await _db.SaveChangesAsync();

        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(tipo: "CE", numero: "987654"), default);

        r.Error.Code.Should().Be("Identity.Document.TypeNotAccepted");
    }

    [Fact]
    public async Task Quien_ya_es_miembro_activo_no_se_vuelve_a_invitar()
    {
        var ana = Guid.NewGuid();
        _identidad.FindByDocumentUsernameAsync("1234567", Arg.Any<CancellationToken>())
            .Returns(Identidad(ana, "ana@gmail.com", "1234567"));
        _db.TenantMemberships.Add(TenantMembership.CreateActive(ana, _coop.PublicId, false, Admin, Ahora));
        await _db.SaveChangesAsync();

        var r = await Emision().PrepararAsync(_coop, Admin, false, Datos(), default);

        r.Error.Code.Should().Be("Invitation.AlreadyMember");
    }

    [Fact]
    public async Task Una_invitacion_nueva_reemplaza_la_viva_de_la_misma_persona()
    {
        var ana = Guid.NewGuid();
        _identidad.FindByDocumentUsernameAsync("1234567", Arg.Any<CancellationToken>())
            .Returns(Identidad(ana, "ana@gmail.com", "1234567"));

        var primera = await Emision().PrepararAsync(_coop, Admin, false, Datos(), default);
        await _db.SaveChangesAsync();
        var segunda = await Emision().PrepararAsync(_coop, Admin, false, Datos(), default);
        await _db.SaveChangesAsync();

        segunda.IsSuccess.Should().BeTrue();
        (await _db.Invitations.SingleAsync(i => i.PublicId == primera.Value.Invitacion.PublicId))
            .Status.Should().Be(InvitationStatus.Superseded);
    }

    [Fact]
    public async Task Sin_documento_la_invitacion_sale_como_antes()
    {
        var r = await Emision().PrepararAsync(_coop, Admin, false, new DatosDelInvitado("ana@gmail.com"), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Invitacion.RequiresDocument.Should().BeFalse();
        r.Value.Invitacion.CentralUserId.Should().BeNull();
    }

    [Fact]
    public void Agotar_los_intentos_pasa_la_invitacion_a_revision()
    {
        var inv = Invitation.Create("ana@gmail.com", "ANA@GMAIL.COM", _coop.PublicId, Admin, false, [1, 2, 3], Ahora, Ahora.AddDays(7));
        inv.AnclarADocumento(Guid.NewGuid(), "CC", "1234567", "Ana", null, "Pérez", null, null, null, null, 3, null);

        inv.RegistrarIntentoFallido().Should().BeFalse();
        inv.RegistrarIntentoFallido().Should().BeFalse();
        inv.AttemptsLeft.Should().Be(1);
        inv.RegistrarIntentoFallido().Should().BeTrue();
        inv.Status.Should().Be(InvitationStatus.UnderReview);
        inv.IsValid(Ahora).Should().BeFalse("una invitación en revisión no activa a nadie");
    }

    [Fact]
    public void La_politica_por_defecto_acepta_todos_los_tipos_de_acceso() =>
        TenantIdentityPolicy.TiposAceptadosDe(null).Select(t => t.Codigo)
            .Should().BeEquivalentTo(TiposDeDocumento.DeAcceso.Select(t => t.Codigo));
}
