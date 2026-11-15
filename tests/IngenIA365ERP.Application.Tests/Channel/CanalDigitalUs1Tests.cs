using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Concepts;
using IngenIA365ERP.Application.Channel.Destinations;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Channel.Settings;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US1 (T043, T048): configuración del canal, conceptos, destinos con credenciales en tránsito y habilitaciones con su
/// política. Sobre el contexto en memoria; el canal satélite es un doble.
/// </summary>
public class CanalDigitalUs1Tests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Cooperativa = Guid.Parse("c4f1a7d2-0000-4000-8000-000000000001");

    private sealed class Escenario
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
        public ICanalSatelite Canal { get; } = Substitute.For<ICanalSatelite>();
        public ICurrentTenantService Tenant { get; } = Substitute.For<ICurrentTenantService>();

        public Escenario()
        {
            Reloj.UtcNow.Returns(Ahora);
            Canal.Configurado.Returns(true);
            Canal.ReservarCodigoAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success("CFP"));
            Tenant.TenantId.Returns(Cooperativa.ToString());
        }

        public Person Persona(string documento, bool asociada)
        {
            var p = new Person { FirstName = "Ana", LastName = "Prueba", TaxId = documento, IdType = "C", Username = documento, Status = "A", CreatedBy = "test" };
            Db.People.Add(p);
            Db.SaveChanges();
            if (asociada)
            {
                Db.Associates.Add(new Associate { PersonId = p.Id, Status = "A", CreatedAt = Ahora });
                Db.SaveChanges();
            }
            return p;
        }

        public async Task<Result<ChannelSettingsDto>> ConfigurarAsync(EnablementPolicy politica = EnablementPolicy.AllActiveAssociates, bool noAsociados = false) =>
            await new UpdateChannelSettingsCommandHandler(Db, Reloj, Canal).Handle(
                new UpdateChannelSettingsCommand("Cooperativa Flota", null, "#1f3b57", null, "6025550000", "soporte@ejemplo.com", "L-V 8-17",
                    15, false, 5, politica, noAsociados, false, 3, SurplusPolicy.AdvanceElseCreditBalance), default);

        public async Task PublicarTextosAsync()
        {
            var h = new PublishChannelLegalTextCommandHandler(Db, Reloj);
            (await h.Handle(new PublishChannelLegalTextCommand(LegalTextKind.Terms, "Términos"), default)).IsSuccess.Should().BeTrue();
            (await h.Handle(new PublishChannelLegalTextCommand(LegalTextKind.DataPolicy, "Política"), default)).IsSuccess.Should().BeTrue();
        }

        public EnableChannelCommandHandler Encender() => new(Db, Reloj, Canal, Tenant, new PoliticaDeHabilitacion(Db));
    }

    // ------------------------------------------------------------------------------------------ configuración --

    [Fact]
    public async Task Cambiar_la_configuracion_crea_una_version_y_no_toca_la_anterior()
    {
        var e = new Escenario();
        (await e.ConfigurarAsync()).Value.Version.Should().Be(1);
        (await e.ConfigurarAsync(EnablementPolicy.Manual)).Value.Version.Should().Be(2);

        var versiones = await e.Db.ChannelSettings.OrderBy(c => c.Version).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].EnablementPolicy.Should().Be(EnablementPolicy.AllActiveAssociates);
        versiones[1].EnablementPolicy.Should().Be(EnablementPolicy.Manual);
    }

    [Theory]
    [InlineData(9, 5, 3, "#1F3B57")]
    [InlineData(31, 5, 3, "#1F3B57")]
    [InlineData(15, 4, 3, "#1F3B57")]
    [InlineData(15, 16, 3, "#1F3B57")]
    [InlineData(15, 5, 16, "#1F3B57")]
    [InlineData(15, 5, 3, "azul")]
    public void El_validador_respeta_los_limites(int sesionDePago, int inactividad, int aviso, string color)
    {
        var resultado = new UpdateChannelSettingsCommandValidator().Validate(new UpdateChannelSettingsCommand(
            "Coop", null, color, null, null, null, null, sesionDePago, false, inactividad, EnablementPolicy.Manual, false, false, aviso,
            SurplusPolicy.AdvanceElseCreditBalance));
        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task No_se_enciende_sin_nombre_ni_sin_terminos_y_politica()
    {
        var e = new Escenario();
        (await e.Encender().Handle(new EnableChannelCommand(), default)).Error.Code.Should().Be(EnableChannelCommandHandler.SinNombre);

        await e.ConfigurarAsync();
        (await e.Encender().Handle(new EnableChannelCommand(), default)).Error.Code.Should().Be(EnableChannelCommandHandler.SinTextos);
    }

    [Fact]
    public async Task Encender_reserva_el_codigo_una_sola_vez_y_materializa_la_politica()
    {
        var e = new Escenario();
        var asociada = e.Persona("1001", asociada: true);
        e.Persona("1002", asociada: false);
        await e.ConfigurarAsync();
        await e.PublicarTextosAsync();

        var encendido = await e.Encender().Handle(new EnableChannelCommand(), default);
        encendido.IsSuccess.Should().BeTrue();
        encendido.Value.IsEnabled.Should().BeTrue();
        encendido.Value.ChannelCode.Should().Be("CFP");

        var habilitaciones = await e.Db.ChannelEnablements.ToListAsync();
        habilitaciones.Should().ContainSingle().Which.PersonId.Should().Be(asociada.Id, "sólo el asociado activo entra por la política");
        habilitaciones[0].Source.Should().Be(EnablementSource.Policy);

        // Apagar y volver a encender no pide otro código.
        await new DisableChannelCommandHandler(e.Db, e.Reloj, e.Canal).Handle(new DisableChannelCommand(), default);
        (await e.Encender().Handle(new EnableChannelCommand(), default)).Value.ChannelCode.Should().Be("CFP");
        await e.Canal.Received(1).ReservarCodigoAsync(Cooperativa, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Cooperativa de Transportadores Flota", "CTF")]
    [InlineData("Ñandú", "NNA")]
    [InlineData("AB", "ABX")]
    public void El_codigo_sugerido_son_tres_letras(string nombre, string esperado) =>
        EnableChannelCommandHandler.CodigoSugerido(nombre).Should().Be(esperado);

    // ------------------------------------------------------------------------------------ destinos y credenciales --

    [Fact]
    public async Task El_destino_exige_un_convenio_de_pasarela_y_las_credenciales_no_se_guardan()
    {
        var e = new Escenario();
        var banco = new CollectionAgreement { Code = "BCO", Name = "Banco", Kind = CollectionAgreementKind.Bank, CreatedBy = "test" };
        var pasarela = new CollectionAgreement { Code = "PSE", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, CreatedBy = "test" };
        e.Db.CollectionAgreements.AddRange(banco, pasarela);
        await e.Db.SaveChangesAsync();
        var guardar = new SaveChannelDestinationCommandHandler(e.Db);

        (await guardar.Handle(new SaveChannelDestinationCommand(null, "Banco", banco.PublicId, true, false, new TimeOnly(20, 0), true), default))
            .Error.Code.Should().Be(Destinos.ConvenioNoEsPasarela);
        var destino = (await guardar.Handle(new SaveChannelDestinationCommand(null, "Pasarela", pasarela.PublicId, true, false, new TimeOnly(20, 0), true), default)).Value;

        e.Canal.GuardarCredencialesAsync(Cooperativa, destino.SitePublicId, Arg.Any<CredencialesDePasarela>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new VerificacionDeCredencialesDto(true, Ahora, "abcd", null)));
        var comando = new SetChannelGatewayCredentialsCommand(destino.PublicId, "https://pasarela.example", "login-abcd", "secreto-que-no-se-guarda");
        var resultado = await new SetChannelGatewayCredentialsCommandHandler(e.Db, e.Canal, e.Tenant).Handle(comando, default);

        resultado.Value.CredentialsStatus.Should().Be(CredentialsStatus.Active);
        resultado.Value.LoginLast4.Should().Be("abcd");
        System.Text.Json.JsonSerializer.Serialize(comando).Should().NotContain("secreto-que-no-se-guarda", "la auditoría serializa el comando");
        comando.ToString().Should().NotContain("secreto-que-no-se-guarda");
        await e.Canal.Received(1).GuardarCredencialesAsync(Cooperativa, destino.SitePublicId,
            Arg.Is<CredencialesDePasarela>(c => c.SecretKey == "secreto-que-no-se-guarda" && c.MediosDePago.SequenceEqual(new[] { "pse" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_fecha_de_verificacion_las_credenciales_quedan_pendientes()
    {
        var e = new Escenario();
        var pasarela = new CollectionAgreement { Code = "PSE", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, CreatedBy = "test" };
        e.Db.CollectionAgreements.Add(pasarela);
        await e.Db.SaveChangesAsync();
        var destino = (await new SaveChannelDestinationCommandHandler(e.Db).Handle(
            new SaveChannelDestinationCommand(null, "Pasarela", pasarela.PublicId, true, true, new TimeOnly(20, 0), true), default)).Value;
        e.Canal.GuardarCredencialesAsync(default, default, default!, default)
            .ReturnsForAnyArgs(Result.Success(new VerificacionDeCredencialesDto(false, null, "abcd", "La verificación llega con el adaptador.")));

        var resultado = await new SetChannelGatewayCredentialsCommandHandler(e.Db, e.Canal, e.Tenant)
            .Handle(new SetChannelGatewayCredentialsCommand(destino.PublicId, "https://p.example", "login-abcd", "s"), default);

        resultado.Value.CredentialsStatus.Should().Be(CredentialsStatus.PendingVerification);
    }

    // ------------------------------------------------------------------------------------------------ conceptos --

    [Fact]
    public async Task Los_conceptos_validan_la_linea_su_naturaleza_y_no_se_duplican()
    {
        var e = new Escenario();
        e.Db.CreditLines.Add(new CreditLine { Code = "CONSUMO", Name = "Consumo", Nature = LineNature.Placement, CreatedBy = "test" });
        e.Db.CreditLines.Add(new CreditLine { Code = "APORTES", Name = "Aportes", Nature = LineNature.Contribution, CreatedBy = "test" });
        await e.Db.SaveChangesAsync();
        var h = new SaveChannelConceptCommandHandler(e.Db);

        SaveChannelConceptCommand Credito(string linea) => new(null, ConceptKind.Loan, linea, null, true, false, null, true, true, true,
            ChannelPrepaymentChoice.ReduceTerm, true, false, false);

        (await h.Handle(Credito("APORTES"), default)).Error.Code.Should().Be(Conceptos.LineaInexistente, "APORTES no es de colocación");
        var creado = await h.Handle(Credito("consumo"), default);
        creado.IsSuccess.Should().BeTrue();
        creado.Value.LineCode.Should().Be("CONSUMO");
        creado.Value.LineName.Should().Be("Consumo");
        creado.Value.DestinationPublicId.Should().BeNull("sin destino es sólo consulta");
        (await h.Handle(Credito("CONSUMO"), default)).Error.Code.Should().Be(Conceptos.Duplicado);
    }

    [Fact]
    public void El_validador_de_conceptos_exige_minimo_y_prepago_solo_en_creditos()
    {
        var v = new SaveChannelConceptCommandValidator();
        v.Validate(new SaveChannelConceptCommand(null, ConceptKind.Contribution, null, null, false, true, null, false, false, true, null, false, false, false))
            .IsValid.Should().BeFalse("con pago parcial hace falta el mínimo");
        v.Validate(new SaveChannelConceptCommand(null, ConceptKind.Contribution, null, null, true, false, null, true, false, true, ChannelPrepaymentChoice.ReduceTerm, false, false, false))
            .IsValid.Should().BeFalse("el abono anticipado es sólo de créditos");
        v.Validate(new SaveChannelConceptCommand(null, ConceptKind.Loan, null, null, true, false, null, true, false, true, null, false, false, false))
            .IsValid.Should().BeFalse("con abono anticipado hay que elegir la opción por defecto");
    }

    // ------------------------------------------------------------------------------------------- habilitaciones --

    [Fact]
    public async Task Habilitar_e_inhabilitar_avanzan_el_cursor_y_exigen_motivo()
    {
        var e = new Escenario();
        await e.ConfigurarAsync(EnablementPolicy.Manual);
        var ana = e.Persona("2001", asociada: true);

        var habilitada = await new EnableChannelPersonCommandHandler(e.Db, e.Reloj).Handle(new EnableChannelPersonCommand(ana.PublicId), default);
        habilitada.Value.Status.Should().Be(EnablementStatus.Enabled);
        var cursor1 = (await e.Db.ChannelEnablements.SingleAsync()).ChangeSequence;

        new DisableChannelPersonCommandValidator().Validate(new DisableChannelPersonCommand(ana.PublicId, "")).IsValid.Should().BeFalse();
        var inhabilitada = await new DisableChannelPersonCommandHandler(e.Db, e.Reloj).Handle(new DisableChannelPersonCommand(ana.PublicId, "Solicitud de la persona"), default);
        inhabilitada.Value.Status.Should().Be(EnablementStatus.Disabled);
        inhabilitada.Value.DisabledReason.Should().Be("Solicitud de la persona");
        (await e.Db.ChannelEnablements.SingleAsync()).ChangeSequence.Should().BeGreaterThan(cursor1);

        var cambios = await new ListEnablementChangesQueryHandler(e.Db).Handle(new ListEnablementChangesQuery(0), default);
        cambios.Value.Items.Should().ContainSingle().Which.Status.Should().Be("Disabled");
        cambios.Value.Items[0].DocumentType.Should().Be("CC");
        (await new ListEnablementChangesQueryHandler(e.Db).Handle(new ListEnablementChangesQuery(cambios.Value.Next), default)).Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_permitir_no_asociados_solo_se_habilita_a_asociados_activos()
    {
        var e = new Escenario();
        await e.ConfigurarAsync(EnablementPolicy.Manual, noAsociados: false);
        var tercero = e.Persona("3001", asociada: false);
        (await new EnableChannelPersonCommandHandler(e.Db, e.Reloj).Handle(new EnableChannelPersonCommand(tercero.PublicId), default))
            .Error.Code.Should().Be(HabilitacionesDelCanal.NoEsAsociado);

        await e.ConfigurarAsync(EnablementPolicy.Manual, noAsociados: true);
        (await new EnableChannelPersonCommandHandler(e.Db, e.Reloj).Handle(new EnableChannelPersonCommand(tercero.PublicId), default))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_conciliacion_inhabilita_al_retirado_y_no_toca_las_manuales()
    {
        var e = new Escenario();
        var retirada = e.Persona("4001", asociada: true);
        var manual = e.Persona("4002", asociada: true);
        await e.ConfigurarAsync(EnablementPolicy.Both, noAsociados: true);
        await e.PublicarTextosAsync();
        await e.Encender().Handle(new EnableChannelCommand(), default);
        (await e.Db.ChannelEnablements.CountAsync()).Should().Be(2);

        // La segunda pasa a manual (la cooperativa decidió por ella) y las dos se retiran.
        var h = await e.Db.ChannelEnablements.SingleAsync(x => x.PersonId == manual.Id);
        h.Source = EnablementSource.Manual;
        foreach (var a in e.Db.Associates) a.Status = "R";
        await e.Db.SaveChangesAsync();

        var conciliacion = await new ReconcileChannelEnablementsCommandHandler(e.Db, e.Reloj, new PoliticaDeHabilitacion(e.Db))
            .Handle(new ReconcileChannelEnablementsCommand(), default);

        conciliacion.Value.Disabled.Should().Be(1);
        // Se lee sin seguimiento: así se comprueba lo guardado, no la entidad en memoria.
        (await e.Db.ChannelEnablements.AsNoTracking().SingleAsync(x => x.PersonId == retirada.Id)).DisabledReason.Should().Be(HabilitacionesDelCanal.MotivoDeRetiro);
        (await e.Db.ChannelEnablements.AsNoTracking().SingleAsync(x => x.PersonId == manual.Id)).Status.Should().Be(EnablementStatus.Enabled);

        // Repetirla no cambia nada.
        (await new ReconcileChannelEnablementsCommandHandler(e.Db, e.Reloj, new PoliticaDeHabilitacion(e.Db))
            .Handle(new ReconcileChannelEnablementsCommand(), default)).Value.Should().Be(new ConciliacionDeHabilitacionesDto(0, 0));
    }

    [Fact]
    public void El_cursor_es_estrictamente_creciente_aunque_el_reloj_no_avance()
    {
        var a = HabilitacionesDelCanal.SiguienteSecuencia(Ahora);
        var b = HabilitacionesDelCanal.SiguienteSecuencia(Ahora);
        b.Should().BeGreaterThan(a);
    }
}
