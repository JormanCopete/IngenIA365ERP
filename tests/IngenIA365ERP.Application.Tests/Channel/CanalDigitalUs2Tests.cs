using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.ContactUpdates;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Compliance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US2 (T059, T060): el contacto de activación que lee el canal y las novedades de contacto que reporta. Sobre el contexto en
/// memoria.
/// </summary>
public class CanalDigitalUs2Tests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
        public IAutorizacionDeDatos Autorizaciones { get; } = Substitute.For<IAutorizacionDeDatos>();
        public ICurrentUserService Usuario { get; } = Substitute.For<ICurrentUserService>();

        public AuditoriaDelCanal Auditoria { get; } =
            new(new ServiceCollection().BuildServiceProvider(), NullLogger<AuditoriaDelCanal>.Instance);

        public Escenario()
        {
            Reloj.UtcNow.Returns(Ahora);
            Usuario.UserName.Returns("funcionaria@coop.test");
        }

        public Person Persona(string documento, bool habilitada, string? correo = "ana.prueba@correo.com", string? celular = "300 123 4567")
        {
            var p = new Person
            {
                FirstName = "Ana", OtherNames = "María", LastName = "Prueba", SecondLastName = "Gómez", TaxId = documento, IdType = "C",
                Username = documento, Status = "A", CreatedBy = "test", Email = correo, Mobile = celular,
            };
            Db.People.Add(p);
            Db.SaveChanges();
            var h = new ChannelEnablement { PersonId = p.Id };
            HabilitacionesDelCanal.Habilitar(h, Ahora, EnablementSource.Manual);
            if (!habilitada) HabilitacionesDelCanal.Inhabilitar(h, Ahora, "Prueba");
            Db.ChannelEnablements.Add(h);
            Db.SaveChanges();
            return p;
        }

        public GetActivationContactQueryHandler Contacto() => new(Db, Autorizaciones, Auditoria);

        public RegisterContactUpdateCommandHandler Registrar() => new(Db);
    }

    [Theory]
    [InlineData("ana.prueba@correo.com", "a•••@c•••.com")]
    [InlineData("x@dominio.com.co", "x•••@d•••.co")]
    public void El_correo_se_enmascara_con_la_primera_letra_y_la_terminacion(string correo, string esperado) =>
        ContactoDeActivacion.EnmascararCorreo(correo).Should().Be(esperado);

    [Theory]
    [InlineData("300 123 4567", "3001234567")]
    [InlineData("+57 310-555-0000", "3105550000")]
    [InlineData("6025550000", null)]
    [InlineData("12345", null)]
    [InlineData(null, null)]
    public void Solo_un_celular_colombiano_recibe_codigos(string? celular, string? esperado) =>
        ContactoDeActivacion.CelularValido(celular).Should().Be(esperado);

    [Theory]
    [InlineData("sin-arroba", null)]
    [InlineData("a b@c.com", null)]
    [InlineData("a@sinpunto", null)]
    [InlineData(" ana@correo.com ", "ana@correo.com")]
    public void Solo_un_correo_bien_formado_recibe_codigos(string correo, string? esperado) =>
        ContactoDeActivacion.CorreoValido(correo).Should().Be(esperado);

    [Fact]
    public async Task El_contacto_de_activacion_trae_mascara_y_entrega_y_las_autorizaciones()
    {
        var e = new Escenario();
        var p = e.Persona("1010101010", habilitada: true);
        e.Autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.ContactChannel, "Email", Arg.Any<CancellationToken>())
            .Returns(new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, Ahora, "Branch"));

        var r = await e.Contacto().Handle(new GetActivationContactQuery(p.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.FirstNames.Should().Be("Ana María");
        r.Value.LastNames.Should().Be("Prueba Gómez");
        r.Value.Email.Should().Be(new ContactoEnmascaradoDto("a•••@c•••.com", true));
        r.Value.Mobile.Should().Be(new ContactoEnmascaradoDto("•••••• 4567", true));
        r.Value.Delivery.Should().Be(new EntregaDeContactoDto("ana.prueba@correo.com", "3001234567"));
        r.Value.ContactConsent.Should().Be(new ConsentimientoDeContactoDto(true, null));
    }

    [Fact]
    public async Task Sin_datos_de_contacto_validos_no_hay_a_donde_enviar()
    {
        var e = new Escenario();
        var p = e.Persona("2020202020", habilitada: true, correo: "no-es-correo", celular: "6025550000");

        var r = await e.Contacto().Handle(new GetActivationContactQuery(p.PublicId), default);

        r.Value.Email.Available.Should().BeFalse();
        r.Value.Mobile.Available.Should().BeFalse();
        r.Value.Delivery.Should().Be(new EntregaDeContactoDto(null, null));
    }

    [Fact]
    public async Task Inhabilitada_o_inexistente_responden_lo_mismo()
    {
        var e = new Escenario();
        var p = e.Persona("3030303030", habilitada: false);

        var inhabilitada = await e.Contacto().Handle(new GetActivationContactQuery(p.PublicId), default);
        var inexistente = await e.Contacto().Handle(new GetActivationContactQuery(Guid.NewGuid()), default);

        inhabilitada.Error.Should().Be(PersonaDelCanal.ErrorNoHabilitada);
        inexistente.Error.Should().Be(PersonaDelCanal.ErrorNoHabilitada);
    }

    [Fact]
    public async Task La_novedad_es_idempotente_por_persona_campo_y_fecha_y_no_toca_la_persona()
    {
        var e = new Escenario();
        var p = e.Persona("4040404040", habilitada: true);
        var reportada = Ahora.AddMinutes(-3);

        var primera = await e.Registrar().Handle(new RegisterContactUpdateCommand(p.PublicId, ContactField.Mobile, "+57 311 222 3344", reportada), default);
        var reintento = await e.Registrar().Handle(new RegisterContactUpdateCommand(p.PublicId, ContactField.Mobile, "3112223344", reportada), default);

        primera.Value.Created.Should().BeTrue();
        reintento.Value.Should().Be(primera.Value with { Created = false });
        e.Db.ChannelContactUpdates.Should().ContainSingle().Which.NewValue.Should().Be("3112223344");
        e.Db.People.Single(x => x.Id == p.Id).Mobile.Should().Be("300 123 4567");
    }

    [Fact]
    public async Task Un_valor_invalido_no_se_registra()
    {
        var e = new Escenario();
        var p = e.Persona("5050505050", habilitada: true);

        var r = await e.Registrar().Handle(new RegisterContactUpdateCommand(p.PublicId, ContactField.Email, "sin-arroba", Ahora), default);

        r.Error.Code.Should().Be(NovedadesDeContacto.ValorInvalido);
        e.Db.ChannelContactUpdates.Should().BeEmpty();
    }

    [Fact]
    public async Task Al_listar_el_valor_va_enmascarado_y_atender_es_idempotente()
    {
        var e = new Escenario();
        var p = e.Persona("6060606060", habilitada: true);
        var reg = await e.Registrar().Handle(new RegisterContactUpdateCommand(p.PublicId, ContactField.Email, "nuevo@correo.com", Ahora), default);

        var pendientes = await new ListContactUpdatesQueryHandler(e.Db).Handle(new ListContactUpdatesQuery(true), default);
        pendientes.Value.Items.Should().ContainSingle().Which.NewValueMasked.Should().Be("n•••@c•••.com");

        var atender = new AcknowledgeContactUpdateCommandHandler(e.Db, e.Reloj, e.Usuario);
        var a1 = await atender.Handle(new AcknowledgeContactUpdateCommand(reg.Value.PublicId), default);
        e.Reloj.UtcNow.Returns(Ahora.AddHours(1));
        var a2 = await atender.Handle(new AcknowledgeContactUpdateCommand(reg.Value.PublicId), default);

        a1.Value.AcknowledgedAt.Should().Be(Ahora);
        a2.Value.AcknowledgedAt.Should().Be(Ahora);
        a2.Value.AcknowledgedBy.Should().Be("funcionaria@coop.test");
        (await new ListContactUpdatesQueryHandler(e.Db).Handle(new ListContactUpdatesQuery(true), default)).Value.Items.Should().BeEmpty();

        var detalle = await new GetContactUpdateQueryHandler(e.Db, e.Auditoria).Handle(new GetContactUpdateQuery(reg.Value.PublicId), default);
        detalle.Value.NewValue.Should().Be("nuevo@correo.com");
        detalle.Value.CurrentValue.Should().Be("ana.prueba@correo.com");
    }
}
