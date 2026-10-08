using FluentAssertions;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0270 (US17; T74; contracts/adaptadores-y-archivos.md §4.1–§4.4; data-model §26.1–§26.2; FR-122, FR-142, C57):
/// <see cref="ColaDeMensajesAlDeudor"/> es el único escritor de <c>LND_DebtorMessages</c> y <c>LND_DebtorMessageEvents</c>. El mensaje nace
/// en la unidad de trabajo de quien lo motiva y sin guardar; el motor decide al programar <c>Queued</c>, <c>Deferred</c> (con el instante
/// siguiente y sus motivos) o <c>Excluded</c>; la clave <c>{origen}:{entidad}:{propósito}:{fecha}</c> es única y repetirla devuelve el
/// mismo; sella la plantilla vigente con su versión, el adaptador y el cuerpo renderizado una vez; sólo va al deudor o a un codeudor
/// obligado —nunca a una referencia—; antes de la activación no encola ni programa antes del registro de la activación; si la causa se
/// reversa, lo que no salió se cancela en la misma transacción; <c>COR_Notifications</c> no se usa.
/// </summary>
public sealed class ColaDeMensajesAlDeudorTests : IDisposable
{
    private readonly MensajesDePrueba _e = new();

    public ColaDeMensajesAlDeudorTests()
    {
        _e.Autorizar(_e.Ana, MessageChannel.Email);
        _e.Autorizar(_e.Ana, MessageChannel.Sms);
        _e.Autorizar(_e.Beto, MessageChannel.Email);
    }

    public void Dispose() => _e.Dispose();

    [Fact]
    public async Task Nace_en_la_unidad_de_trabajo_de_quien_lo_motiva_sin_guardar_y_sin_COR_Notifications()
    {
        var r = await _e.Cola().EncolarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        (await _e.Db.DebtorMessages.AsNoTracking().CountAsync()).Should().Be(0, "la cola nunca guarda: guarda quien lo motiva");
        _e.Db.ChangeTracker.Entries<DebtorMessage>().Should().ContainSingle(x => x.State == EntityState.Added);
        _e.Db.ChangeTracker.Entries<DebtorMessageEvent>().Should().ContainSingle(x => x.State == EntityState.Added);

        await _e.Db.SaveChangesAsync();

        var guardado = await _e.Db.DebtorMessages.AsNoTracking().SingleAsync();
        var evento = await _e.Db.DebtorMessageEvents.AsNoTracking().SingleAsync();
        evento.DebtorMessageId.Should().Be(guardado.Id, "el primer evento se enlaza en el mismo guardado");
        evento.Sequence.Should().Be(1);
        (await _e.Db.Notifications.CountAsync()).Should().Be(0, "COR_Notifications es de usuarios del ERP, no de deudores (T74)");
    }

    [Fact]
    public async Task En_horario_nace_Queued_desde_el_instante_pedido()
    {
        var r = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito));

        r.Evento.Kind.Should().Be(DebtorMessageEventKind.Queued);
        r.Evento.DueAt.Should().Be(_e.G.UtcNow);
        r.Evento.ActorKind.Should().Be(ActorKind.Person);
        r.Mensaje.RecipientRole.Should().Be(ContactRole.Debtor);
    }

    [Fact]
    public async Task Programado_fuera_de_horario_nace_Deferred_con_el_instante_siguiente_y_sus_motivos()
    {
        var pedido = MensajesDePrueba.Abono(_e.Ana, _e.Credito) with
        {
            ProgramadoPara = MensajesDePrueba.Utc(new DateTime(2026, 12, 1, 20, 30, 0)),
        };

        var r = await _e.EncolarYGuardarAsync(pedido);

        r.Evento.Kind.Should().Be(DebtorMessageEventKind.Deferred);
        r.Evento.DueAt.Should().Be(MensajesDePrueba.Utc(new DateTime(2026, 12, 2, 7, 0, 0)), "abre el miércoles a las 7:00");
        r.Evento.Motivos().Should().Equal(ContactWindowReason.OutsideHours);
        r.Mensaje.ScheduledFor.Should().Be(pedido.ProgramadoPara);
    }

    [Fact]
    public async Task El_festivo_nacional_difiere_al_dia_habil_siguiente()
    {
        _e.Festivos.Add(new DateOnly(2026, 12, 8));
        var pedido = MensajesDePrueba.Abono(_e.Ana, _e.Credito) with
        {
            ProgramadoPara = MensajesDePrueba.Utc(new DateTime(2026, 12, 8, 9, 0, 0)),
        };

        var r = await _e.EncolarYGuardarAsync(pedido);

        r.Evento.Kind.Should().Be(DebtorMessageEventKind.Deferred);
        r.Evento.Motivos().Should().Equal(ContactWindowReason.Holiday);
        r.Evento.DueAt.Should().Be(MensajesDePrueba.Utc(new DateTime(2026, 12, 9, 7, 0, 0)));
    }

    [Fact]
    public async Task Sin_autorizacion_del_canal_nace_Excluded_con_su_motivo()
    {
        var r = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Beto, _e.Credito, canal: MessageChannel.Sms));

        r.Evento.Kind.Should().Be(DebtorMessageEventKind.Excluded);
        r.Evento.Motivos().Should().Equal(ContactWindowReason.ChannelNotAuthorized);
        r.Mensaje.AuthorizationConsentPublicId.Should().BeNull();
    }

    [Fact]
    public async Task La_clave_es_origen_entidad_proposito_fecha_y_repetirla_devuelve_el_mismo()
    {
        var cola = _e.Cola();
        var primero = await cola.EncolarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1"), default);
        var repetido = await cola.EncolarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1"), default);
        await _e.Db.SaveChangesAsync();
        var otroDia = await _e.Cola().EncolarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1") with
        {
            ProgramadoPara = MensajesDePrueba.Utc(new DateTime(2026, 12, 2, 9, 0, 0)),
        }, default);
        var repetidoGuardado = await _e.Cola().EncolarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1"), default);

        primero.Value.Mensaje.IdempotencyKey.Should().Be("PaymentApplied:op-1:Informative:2026-12-01");
        repetido.Value.Repetido.Should().BeTrue();
        repetido.Value.Mensaje.Should().BeSameAs(primero.Value.Mensaje, "en la misma unidad de trabajo, el mismo");
        repetidoGuardado.Value.Repetido.Should().BeTrue();
        repetidoGuardado.Value.Mensaje.PublicId.Should().Be(primero.Value.Mensaje.PublicId, "ya guardado, también el mismo");
        otroDia.Value.Repetido.Should().BeFalse();
        otroDia.Value.Mensaje.IdempotencyKey.Should().Be("PaymentApplied:op-1:Informative:2026-12-02");
    }

    [Fact]
    public async Task Sella_la_plantilla_vigente_con_su_version_el_adaptador_y_el_cuerpo_renderizado_una_vez()
    {
        var vieja = _e.Db.NotificationTemplates.Single(t => t.Code == "ABONOCAP" && t.Channel == "Email");
        vieja.ValidTo = new DateOnly(2026, 11, 30);
        await _e.Db.SaveChangesAsync();
        var vigente = _e.Plantilla("ABONOCAP", DebtorMessagePurpose.Informative, MessageChannel.Email, "Abono aplicado a {{NumeroDelCredito}}",
            "Querida {{NombreDelTitular}}: aplicamos {{ValorAplicado}}.", version: 2, desde: new DateOnly(2026, 12, 1));

        var r = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito));

        r.Mensaje.TemplateId.Should().Be(vigente.Id);
        r.Mensaje.TemplateVersion.Should().Be(2);
        r.Mensaje.ChannelCode.Should().Be(CodigosDeCanalExterno.Correo);
        r.Mensaje.Subject.Should().Be("Abono aplicado a CR-0001");
        r.Mensaje.RenderedBody.Should().Be("Querida Ana Deudora: aplicamos $ 100.000.");
        r.Mensaje.CuerpoIntacto().Should().BeTrue();

        vigente.BodyTemplate = "Otro texto {{NombreDelTitular}}";
        await _e.Db.SaveChangesAsync();
        (await _e.Db.DebtorMessages.AsNoTracking().SingleAsync()).RenderedBody.Should().Be("Querida Ana Deudora: aplicamos $ 100.000.",
            "el cuerpo se sella al nacer y nunca se vuelve a renderizar");
    }

    [Fact]
    public async Task El_SMS_sella_el_canal_configurado_por_la_cooperativa_o_el_simulado()
    {
        var sinConfigurar = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1", MessageChannel.Sms));
        _e.Canal("PROVEEDOR-SMS", [MessageChannel.Sms], consultaEntrega: true);
        _e.ConfigurarCanal(ExternalChannelKind.Sms, "PROVEEDOR-SMS");
        var configurado = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-2", MessageChannel.Sms));

        sinConfigurar.Mensaje.ChannelCode.Should().Be(CodigosDeCanalExterno.Simulado);
        configurado.Mensaje.ChannelCode.Should().Be("PROVEEDOR-SMS");
        configurado.Mensaje.AuthorizationConsentPublicId.Should().NotBeNull("sella la autorización de canal usada");
    }

    [Fact]
    public async Task Va_al_codeudor_obligado_pero_nunca_a_quien_no_esta_obligado()
    {
        var codeudor = await _e.Cola().EncolarAsync(MensajesDePrueba.Abono(_e.Beto, _e.Credito, "op-1"), default);
        _e.Autorizar(_e.Carla, MessageChannel.Email);
        var referencia = await _e.Cola().EncolarAsync(MensajesDePrueba.Abono(_e.Carla, _e.Credito, "op-2"), default);

        codeudor.Value.Mensaje.RecipientRole.Should().Be(ContactRole.CoDebtor);
        referencia.IsFailure.Should().BeTrue();
        referencia.Error.Code.Should().Be("Lending.DebtorMessage.RecipientNotObligated");
        _e.Db.ChangeTracker.Entries<DebtorMessage>().Should().ContainSingle("a la referencia no le nace nada");
    }

    [Fact]
    public async Task Antes_de_la_activacion_no_encola()
    {
        using var sinActivar = new MensajesDePrueba(activada: false);
        sinActivar.Autorizar(sinActivar.Ana, MessageChannel.Email);

        var r = await sinActivar.Cola().EncolarAsync(MensajesDePrueba.Abono(sinActivar.Ana, sinActivar.Credito), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.NotActivated");
        sinActivar.Db.ChangeTracker.Entries<DebtorMessage>().Should().BeEmpty();
    }

    [Fact]
    public async Task No_programa_con_fecha_anterior_al_registro_de_la_activacion()
    {
        var registro = _e.G.UtcNow.AddHours(-1);
        _e.Activar(registro);
        var pedido = MensajesDePrueba.Abono(_e.Ana, _e.Credito) with { ProgramadoPara = _e.G.UtcNow.AddDays(-5) };

        var r = await _e.EncolarYGuardarAsync(pedido);

        r.Mensaje.ScheduledFor.Should().Be(registro, "la cola sólo programa desde la activación (FR-142)");
        r.Mensaje.IdempotencyKey.Should().EndWith(":2026-12-01");
        r.Evento.DueAt.Should().BeOnOrAfter(_e.G.UtcNow);
    }

    [Fact]
    public async Task Si_la_causa_se_reversa_el_que_no_salio_se_cancela_en_la_misma_transaccion()
    {
        var origen = Guid.NewGuid();
        var pendiente = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1") with { OriginPublicId = origen });
        var salio = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Beto, _e.Credito, "op-1-beto") with { OriginPublicId = origen });
        var cola = _e.Cola();
        await cola.RegistrarAsync(salio.Mensaje, new EventoDeMensaje(DebtorMessageEventKind.Sent, _e.G.UtcNow, ActorKind.Process)
        {
            DestinationAddress = "beto@correo.test", AttemptNumber = 1,
        }, default);
        await _e.Db.SaveChangesAsync();

        var canceladas = await _e.Cola().CancelarPorCausaAsync("PaymentApplied", origen, "Se reversó el abono.", ActorKind.Person, 7, default);

        canceladas.Should().Be(1);
        (await _e.UltimoAsync(pendiente.Mensaje)).Kind.Should().Be(DebtorMessageEventKind.Queued, "todavía no se guardó: es la transacción del reverso");
        await _e.Db.SaveChangesAsync();
        var ultimo = await _e.UltimoAsync(pendiente.Mensaje);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Cancelled);
        ultimo.ReasonText.Should().Be("Se reversó el abono.");
        (await _e.UltimoAsync(salio.Mensaje)).Kind.Should().Be(DebtorMessageEventKind.Sent, "lo que ya salió no se toca");
    }

    [Fact]
    public async Task Un_cobro_con_autorizacion_sin_evidencia_nace_excluido()
    {
        _e.Autorizar(_e.Beto, MessageChannel.Sms, conEvidencia: false);

        var r = await _e.EncolarYGuardarAsync(MensajesDePrueba.Mora(_e.Beto, _e.Credito));

        r.Evento.Kind.Should().Be(DebtorMessageEventKind.Excluded);
        r.Evento.Motivos().Should().Equal(ContactWindowReason.AuthorizationWithoutEvidence);
        r.Evento.ActorKind.Should().Be(ActorKind.Process);
    }
}
