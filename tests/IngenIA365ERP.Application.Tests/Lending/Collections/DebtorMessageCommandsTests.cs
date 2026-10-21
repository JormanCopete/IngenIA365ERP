using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0272 (US17; T74, T75; contracts/api.md §14.2; adaptadores-y-archivos.md §4.5–§4.6): el envío manual
/// <see cref="SendDebtorMessageCommand"/> inmediato fuera de ventana se rechaza (<c>Lending.DebtorMessage.OutsideLegalWindow</c> con
/// <c>data.nextAllowedAt</c>; canal semanal, <c>.WeeklyChannelUsed</c>; <c>.ChannelNotAuthorized</c>; <c>.RecipientNotObligated</c>;
/// <c>Lending.Authorization.WithoutEvidence</c>) y el programado fuera de ventana nace <c>Deferred</c> con el instante nuevo;
/// <see cref="CancelDebtorMessageCommand"/> sólo antes de salir y con motivo; <see cref="ListDebtorMessagesQuery"/> y
/// <see cref="GetDebtorMessageQuery"/> filtran por el alcance de Cartera; <see cref="GetContactWindowQuery"/> no escribe.
/// </summary>
public sealed class DebtorMessageCommandsTests : IDisposable
{
    private readonly MensajesDePrueba _e = new();

    public DebtorMessageCommandsTests()
    {
        _e.Autorizar(_e.Ana, MessageChannel.Sms);
        _e.Autorizar(_e.Ana, MessageChannel.Email);
        _e.G.ActuarComo(_e.G.Usuario(null, "gestor@coop.test"));
    }

    public void Dispose() => _e.Dispose();

    private SendDebtorMessageCommandHandler Enviar() => new(_e.Db, _e.Cola(), _e.G.Actor, _e.G.Alcance, _e.G.Auditoria);

    private CancelDebtorMessageCommandHandler Cancelar() => new(_e.Db, _e.Cola(), _e.G.Actor, _e.G.Alcance, _e.G.Auditoria, _e.G.Reloj);

    private SendDebtorMessageCommand Mora(MessageChannel canal = MessageChannel.Sms, DateTimeOffset? programado = null,
        IngenIA365ERP.Domain.Entities.Core.Person? para = null) =>
        new(_e.Credito.PublicId, (para ?? _e.Ana).PublicId, canal, DebtorMessagePurpose.Collection, "MORAADMIN", programado,
            new Dictionary<string, string?> { ["ValorVencido"] = "$ 150.000" })
        {
            OperationKey = Guid.NewGuid(),
        };

    private static JsonElement Datos(IngenIA365ERP.Application.Common.Models.Error error) => GobiernoDePrueba.Datos(error);

    // ------------------------------------------------------------------------------------------------------ envío manual --

    [Fact]
    public async Task Inmediato_en_horario_queda_en_cola_y_lo_firma_quien_lo_pide()
    {
        var r = await Enviar().Handle(Mora(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(DebtorMessageEventKind.Queued);
        r.Value.Template!.Code.Should().Be("MORAADMIN");
        r.Value.Recipient.PersonPublicId.Should().Be(_e.Ana.PublicId);
        var m = await _e.Db.DebtorMessages.AsNoTracking().SingleAsync();
        m.IsManualImmediate.Should().BeTrue();
        m.OriginKind.Should().Be("Manual");
        m.RequestedByKind.Should().Be(ActorKind.Person);
        m.RequestedByUserId.Should().NotBeNull();
    }

    [Fact]
    public async Task Inmediato_un_domingo_responde_OutsideLegalWindow_con_el_instante_permitido()
    {
        _e.EnLocal(new DateTime(2026, 12, 6, 10, 0, 0));

        var r = await Enviar().Handle(Mora(), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.DebtorMessage.OutsideLegalWindow");
        Datos(r.Error).GetProperty("nextAllowedAt").GetDateTimeOffset()
            .Should().Be(new DateTimeOffset(2026, 12, 7, 7, 0, 0, TimeSpan.FromHours(-5)));
        r.Error.Message.Should().Contain("07/12/2026");
        (await _e.Db.DebtorMessages.CountAsync()).Should().Be(0, "el inmediato fuera de ventana no nace");
    }

    [Fact]
    public async Task Inmediato_por_otro_canal_tras_un_contacto_de_cobranza_en_la_semana_responde_WeeklyChannelUsed()
    {
        await _e.YaEnviadoAsync(_e.Ana, MessageChannel.Email, DebtorMessagePurpose.Collection, new DateTime(2026, 11, 30, 9, 0, 0), "previo-1");

        var r = await Enviar().Handle(Mora(MessageChannel.Sms), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.WeeklyChannelUsed");
        var datos = Datos(r.Error);
        datos.GetProperty("channelUsed").GetString().Should().Be("Email");
        datos.GetProperty("weekStart").GetString().Should().Be("2026-11-30");
        datos.GetProperty("nextAllowedAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 12, 7, 7, 0, 0, TimeSpan.FromHours(-5)));
    }

    [Fact]
    public async Task Inmediato_por_un_canal_que_no_autorizo_responde_ChannelNotAuthorized()
    {
        var r = await Enviar().Handle(Mora(MessageChannel.WhatsApp), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.ChannelNotAuthorized");
        r.Error.Message.Should().Contain("Ana Deudora").And.Contain("WhatsApp");
    }

    [Fact]
    public async Task A_quien_no_es_deudor_ni_codeudor_responde_RecipientNotObligated()
    {
        _e.Autorizar(_e.Carla, MessageChannel.Sms);

        var r = await Enviar().Handle(Mora(para: _e.Carla), default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.RecipientNotObligated");
    }

    [Fact]
    public async Task Cobranza_con_autorizacion_sin_evidencia_responde_Authorization_WithoutEvidence()
    {
        _e.Autorizar(_e.Beto, MessageChannel.Sms, conEvidencia: false);

        var r = await Enviar().Handle(Mora(para: _e.Beto), default);

        r.Error.Code.Should().Be("Lending.Authorization.WithoutEvidence");
    }

    [Fact]
    public async Task Programado_fuera_de_ventana_nace_Deferred_con_el_instante_nuevo()
    {
        var domingo = new DateTimeOffset(2026, 12, 6, 9, 0, 0, TimeSpan.FromHours(-5));

        var r = await Enviar().Handle(Mora(programado: domingo), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(DebtorMessageEventKind.Deferred);
        r.Value.LastEvent.NextAttemptAt.Should().Be(MensajesDePrueba.Utc(new DateTime(2026, 12, 7, 7, 0, 0)));
        r.Value.LastEvent.Reasons.Should().Equal("Sunday");
        r.Value.ScheduledFor.Should().Be(domingo.UtcDateTime);
    }

    [Fact]
    public async Task Fuera_del_alcance_el_credito_no_existe()
    {
        _e.G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { MensajesDePrueba.Sur }, TodasLasLineas = true };

        var r = await Enviar().Handle(Mora(), default);

        r.Error.Code.Should().Be("Lending.Loan.NotFound");
    }

    [Fact]
    public void Los_comandos_llevan_clave_y_la_cancelacion_motivo()
    {
        typeof(IOperacionIdempotente).IsAssignableFrom(typeof(SendDebtorMessageCommand)).Should().BeTrue();
        typeof(IOperacionIdempotente).IsAssignableFrom(typeof(CancelDebtorMessageCommand)).Should().BeTrue();
        typeof(IConMotivo).IsAssignableFrom(typeof(CancelDebtorMessageCommand)).Should().BeTrue();
        new CancelDebtorMessageCommandValidator().Validate(new CancelDebtorMessageCommand(Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
    }

    // -------------------------------------------------------------------------------------------------------- cancelar --

    [Fact]
    public async Task Cancelar_antes_de_salir_deja_el_evento_Cancelled_con_su_motivo()
    {
        var enviado = await Enviar().Handle(Mora(), default);

        var r = await Cancelar().Handle(new CancelDebtorMessageCommand(enviado.Value.PublicId, "El deudor pagó en la oficina.") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var m = await _e.Db.DebtorMessages.AsNoTracking().SingleAsync();
        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Cancelled);
        ultimo.ReasonText.Should().Be("El deudor pagó en la oficina.");
        ultimo.ActorKind.Should().Be(ActorKind.Person);
    }

    [Fact]
    public async Task Cancelar_lo_que_ya_salio_responde_AlreadySent()
    {
        var salio = await _e.YaEnviadoAsync(_e.Ana, MessageChannel.Sms, DebtorMessagePurpose.Collection, new DateTime(2026, 12, 1, 8, 0, 0), "previo-1");

        var r = await Cancelar().Handle(new CancelDebtorMessageCommand(salio.PublicId, "Ya no hace falta") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.DebtorMessage.AlreadySent");
        (await _e.UltimoAsync(salio)).Kind.Should().Be(DebtorMessageEventKind.Sent);
    }

    // -------------------------------------------------------------------------------------------------------- consultas --

    [Fact]
    public async Task Listar_y_consultar_respetan_el_alcance_de_Cartera()
    {
        var delSur = _e.NuevoCredito("CR-0002", MensajesDePrueba.Sur, _e.Beto);
        _e.Autorizar(_e.Beto, MessageChannel.Sms);
        var norte = await Enviar().Handle(Mora(), default);
        var sur = await Enviar().Handle(new SendDebtorMessageCommand(delSur.PublicId, _e.Beto.PublicId, MessageChannel.Sms,
            DebtorMessagePurpose.Collection, "MORAADMIN", null, new Dictionary<string, string?> { ["ValorVencido"] = "$ 1" }) { OperationKey = Guid.NewGuid() }, default);
        sur.IsSuccess.Should().BeTrue(sur.IsFailure ? sur.Error.Message : null);

        _e.G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { MensajesDePrueba.Norte }, TodasLasLineas = true };
        var lista = await new ListDebtorMessagesQueryHandler(_e.Db, _e.G.Alcance).Handle(new ListDebtorMessagesQuery(), default);
        var propio = await new GetDebtorMessageQueryHandler(_e.Db, _e.G.Alcance).Handle(new GetDebtorMessageQuery(norte.Value.PublicId), default);
        var ajeno = await new GetDebtorMessageQueryHandler(_e.Db, _e.G.Alcance).Handle(new GetDebtorMessageQuery(sur.Value.PublicId), default);

        lista.Value.TotalCount.Should().Be(1, "lo de otra oficina no aparece ni cuenta");
        lista.Value.Items.Single().PublicId.Should().Be(norte.Value.PublicId);
        propio.Value.Body.Should().Contain("CR-0001");
        propio.Value.Events.Should().ContainSingle(e => e.Kind == DebtorMessageEventKind.Queued);
        ajeno.Error.Code.Should().Be("Lending.DebtorMessage.NotFound");
    }

    [Fact]
    public async Task Listar_filtra_por_estado_y_proposito()
    {
        await Enviar().Handle(Mora(), default);
        await Enviar().Handle(Mora(programado: new DateTimeOffset(2026, 12, 6, 9, 0, 0, TimeSpan.FromHours(-5))), default);

        var diferidos = await new ListDebtorMessagesQueryHandler(_e.Db, _e.G.Alcance)
            .Handle(new ListDebtorMessagesQuery(Status: DebtorMessageEventKind.Deferred), default);
        var informativos = await new ListDebtorMessagesQueryHandler(_e.Db, _e.G.Alcance)
            .Handle(new ListDebtorMessagesQuery(Purpose: DebtorMessagePurpose.Informative), default);

        diferidos.Value.TotalCount.Should().Be(1);
        informativos.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task La_ventana_de_contacto_responde_lo_que_dice_el_motor_y_no_escribe()
    {
        _e.EnLocal(new DateTime(2026, 12, 6, 10, 0, 0));
        var handler = new GetContactWindowQueryHandler(_e.Db, _e.Cola(), _e.G.Alcance, _e.G.Reloj);

        var r = await handler.Handle(new GetContactWindowQuery(_e.Ana.PublicId, MessageChannel.Sms, DebtorMessagePurpose.Collection,
            LoanPublicId: _e.Credito.PublicId), default);

        r.Value.Decision.Should().Be("Diferido");
        r.Value.NextAllowedAt.Should().Be(new DateTimeOffset(2026, 12, 7, 7, 0, 0, TimeSpan.FromHours(-5)));
        r.Value.Reasons.Should().Equal("Sunday");
        _e.Db.ChangeTracker.Entries<DebtorMessage>().Should().BeEmpty();
        _e.Db.ChangeTracker.Entries<DebtorMessageEvent>().Should().BeEmpty();
    }
}
