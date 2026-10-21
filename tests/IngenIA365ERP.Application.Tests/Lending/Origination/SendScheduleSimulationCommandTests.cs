using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Collections;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0367 (C87, T74, T80; api.md §4.1): el envío de la simulación. Sólo por correo y con propósito <c>Informative</c>, a la persona
/// simulada —que todavía no es deudora— con autorización de tratamiento general y correo registrado; el PDF va como adjunto de módulo; el
/// mensaje nace por la cola con su ventana legal (<c>OutsideLegalWindow</c> con <c>nextAllowedAt</c>) y su autorización de canal
/// (<c>ChannelNotAuthorized</c>). No registra nada del crédito: ni solicitud, ni cupo, ni número.
/// </summary>
public sealed class SendScheduleSimulationCommandTests : IAsyncLifetime
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private OriginacionDePrueba _o = null!;
    private readonly IRenderizadorDeSimulacion _renderizador = Substitute.For<IRenderizadorDeSimulacion>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICalendarioHabil _calendario = Substitute.For<ICalendarioHabil>();
    private readonly ICanalesDeMensajeria _canales = Substitute.For<ICanalesDeMensajeria>();
    private readonly List<UploadAttachmentCommand> _subidas = [];
    private readonly Guid _adjunto = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: true);
        var g = _o.G;
        g.Reloj.AhoraLocal.Returns(_ => new DateTimeOffset(DateTime.SpecifyKind(g.UtcNow, DateTimeKind.Utc)).ToOffset(Colombia));
        g.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7m, "Ley 2300 de 2023");
        g.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19m, "Ley 2300 de 2023");
        g.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, 8m, "Ley 2300 de 2023");
        g.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinSabado, 15m, "Ley 2300 de 2023");
        g.Limite(CatalogoDeLimitesLegales.CobranzaContactosDia, 1m, "Ley 2300 de 2023");
        _calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil([], ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        var correo = new CanalDePrueba(CodigosDeCanalExterno.Correo, [MessageChannel.Email], consultaEntrega: false);
        _canales.Resolver(Arg.Any<string>()).Returns(correo);
        _renderizador.Render(Arg.Any<SimulacionParaImprimir>()).Returns([0x25, 0x50, 0x44, 0x46]);
        _sender.Send(Arg.Do<UploadAttachmentCommand>(_subidas.Add), Arg.Any<CancellationToken>()).Returns(Result.Success(_adjunto));

        _o.Db.NotificationTemplates.Add(new NotificationTemplate
        {
            TemplateName = "SIMULACION v1", Channel = nameof(MessageChannel.Email), Subject = "Simulación de su crédito",
            BodyTemplate = "Señor(a) {{NombreDelTitular}}: adjuntamos la simulación. No constituye aprobación.", IsActive = true, Code = "SIMULACION",
            Module = "Lending", Purpose = nameof(DebtorMessagePurpose.Informative), Version = 1, ValidFrom = new DateOnly(2020, 1, 1),
            Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(DebtorMessagePurpose.Informative), IsSeeded = true, CreatedBy = "semilla",
        });
        await _o.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private ColaDeMensajesAlDeudor Cola() =>
        new(_o.Db, _o.G.Reloj, _o.Parametros(), _calendario, _o.Autorizaciones, _canales, Options.Create(_o.Opciones));

    private SendScheduleSimulationCommandHandler Handler() =>
        new(_o.Db, _o.Simulacion(), _renderizador, Cola(), _o.Autorizaciones, _sender, _o.G.Actor, _o.G.Reloj);

    private SendScheduleSimulationCommand Envio(Person? a = null) =>
        new(new ScheduleSimulationInput(_o.Linea.PublicId, 12_000_000m, 36), (a ?? _o.Ana).PublicId) { OperationKey = Guid.NewGuid() };

    private void EnLocal(DateTime local) => _o.G.UtcNow = DateTime.SpecifyKind(local - Colombia, DateTimeKind.Utc);

    [Fact]
    public async Task Envia_por_correo_un_informativo_con_el_pdf_y_no_registra_nada_del_credito()
    {
        var r = await Handler().Handle(Envio(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(nameof(DebtorMessageEventKind.Queued));
        var mensaje = await _o.Db.DebtorMessages.AsNoTracking().SingleAsync();
        mensaje.PublicId.Should().Be(r.Value.DebtorMessagePublicId);
        mensaje.PersonId.Should().Be(_o.Ana.Id);
        mensaje.LoanId.Should().BeNull();
        mensaje.Purpose.Should().Be(DebtorMessagePurpose.Informative);
        mensaje.Channel.Should().Be(MessageChannel.Email);
        mensaje.OriginKind.Should().Be(ColaDeMensajesAlDeudor.OrigenSimulacion);
        mensaje.AttachmentPublicId.Should().Be(_adjunto);
        mensaje.IsManualImmediate.Should().BeTrue();

        var subida = _subidas.Should().ContainSingle().Subject;
        subida.OwnerEntityType.Should().Be(AdjuntosDeModulo.SimulacionEnviada);
        subida.OwnerEntityPublicId.Should().Be(_o.Ana.PublicId);
        subida.ContentType.Should().Be("application/pdf");
        AdjuntosDeModulo.De(AdjuntosDeModulo.SimulacionEnviada)!.Borrable.Should().BeFalse();

        (await _o.Db.CreditApplications.CountAsync()).Should().Be(0);
        (await _o.Db.Loans.CountAsync()).Should().Be(0);
        (await _o.Db.LoanNumberReservations.CountAsync()).Should().Be(0);
        (await _o.Db.LendingNumberSequences.CountAsync()).Should().Be(0, "el envío no consume ningún número");
    }

    [Fact]
    public async Task Sin_autorizacion_de_tratamiento_es_Authorization_Missing()
    {
        _o.QuitarAutorizacion(_o.Ana, DataAuthorizationPurpose.GeneralTreatment);

        var r = await Handler().Handle(Envio(), default);

        r.Error.Code.Should().Be("Lending.Authorization.Missing");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("purpose").GetString().Should().Be("GeneralTreatment");
        datos.GetProperty("personPublicId").GetGuid().Should().Be(_o.Ana.PublicId);
        _subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_correo_registrado_no_se_envia()
    {
        var r = await Handler().Handle(Envio(_o.Beto), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.DestinationMissing");
        _subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_autorizacion_del_canal_correo_es_ChannelNotAuthorized()
    {
        _o.QuitarAutorizacion(_o.Ana, DataAuthorizationPurpose.ContactChannel);

        var r = await Handler().Handle(Envio(), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.ChannelNotAuthorized");
        _subidas.Should().BeEmpty("lo que no puede salir no deja un PDF sin mensaje");
    }

    [Fact]
    public async Task Un_domingo_responde_OutsideLegalWindow_con_el_proximo_instante_permitido()
    {
        EnLocal(new DateTime(2026, 12, 6, 10, 0, 0));

        var r = await Handler().Handle(Envio(), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.OutsideLegalWindow");
        OriginacionDePrueba.Datos(r.Error).GetProperty("nextAllowedAt").GetDateTimeOffset()
            .Should().Be(new DateTimeOffset(2026, 12, 7, 7, 0, 0, Colombia));
        _subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Antes_de_activar_Cartera_no_se_envia()
    {
        var setup = await _o.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        setup.ActivatedAt = null;
        await _o.Db.SaveChangesAsync();

        var r = await Handler().Handle(Envio(), default);

        r.Error.Code.Should().Be("Lending.NotActivated");
    }

    [Fact]
    public void El_comando_lleva_clave_y_la_semilla_trae_la_plantilla_de_correo()
    {
        typeof(IngenIA365ERP.Application.Common.Behaviors.IOperacionIdempotente).IsAssignableFrom(typeof(SendScheduleSimulationCommand)).Should().BeTrue();
        IngenIA365ERP.Persistence.Seeding.Parametric.LendingMessageTemplatesSeeder.Leer()
            .Should().ContainSingle(p => p.Code == SendScheduleSimulationCommandHandler.Plantilla)
            .Which.Channel.Should().Be(MessageChannel.Email);
    }
}
