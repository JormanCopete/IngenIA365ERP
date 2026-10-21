using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Tareas;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0271 (US17; T75; contracts/adaptadores-y-archivos.md §4.6; C57): la tarea <c>cartera.mensajes</c> toma hasta 200 vencidos
/// en orden de <c>RequestedFor</c>, los reevalúa con el motor, cancela el de cobranza de un crédito al día, lee el destino del maestro
/// <b>al despachar</b> y lo copia en el evento, y registra cada intento por <see cref="RegisterDebtorMessageAttemptCommand"/> (idempotente
/// por mensaje e intento): <c>Ok</c> → <c>Sent</c>, <c>Ok</c> de <c>SIMULADO</c> → <c>Simulated</c>, <c>ChannelUnavailable</c> →
/// <c>Deferred(ChannelUnavailable)</c> y tras <c>Mensajes.Reintentos</c> <c>Failed</c>, <c>Rejected</c>/<c>InvalidData</c> → <c>Failed</c>
/// con la alerta <c>Cartera.MensajeAlDeudorFallido</c>. <c>cartera.entregas</c> registra <c>Delivered</c>/<c>Failed</c> de los enviados de
/// las últimas 72 horas cuyo canal consulta la entrega. Las dos salen si Cartera no está activada y corren con los servicios del ámbito de
/// la cooperativa que les arma el ejecutor.
/// </summary>
public sealed class TareasDeMensajesAlDeudorTests : IDisposable
{
    private readonly MensajesDePrueba _e = new();
    private readonly CanalDePrueba _sms;

    public TareasDeMensajesAlDeudorTests()
    {
        _e.Autorizar(_e.Ana, MessageChannel.Sms);
        _e.Autorizar(_e.Ana, MessageChannel.Email);
        _e.Autorizar(_e.Beto, MessageChannel.Sms);
        _sms = _e.Canal("PROVEEDOR-SMS", [MessageChannel.Sms], consultaEntrega: true);
    }

    public void Dispose() => _e.Dispose();

    private Task CorrerMensajesAsync() => new TareaDeMensajesAlDeudor().EjecutarAsync(_e.Servicios(), default);

    private Task CorrerEntregasAsync() => new TareaDeEntregasAlDeudor().EjecutarAsync(_e.Servicios(), default);

    private async Task<DebtorMessage> ConProveedorAsync(PedidoDeMensajeAlDeudor pedido)
    {
        _e.ConfigurarCanal(ExternalChannelKind.Sms, "PROVEEDOR-SMS");
        return (await _e.EncolarYGuardarAsync(pedido)).Mensaje;
    }

    // ------------------------------------------------------------------------------------------------------------- toma --

    [Fact]
    public async Task Toma_hasta_el_tope_de_vencidos_en_orden_de_RequestedFor()
    {
        var tarde = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1") with { ProgramadoPara = _e.G.UtcNow.AddMinutes(-5) });
        var temprano = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-2") with { ProgramadoPara = _e.G.UtcNow.AddMinutes(-50) });
        var medio = await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-3") with { ProgramadoPara = _e.G.UtcNow.AddMinutes(-20) });
        await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-4") with { ProgramadoPara = _e.G.UtcNow.AddHours(2) });

        var vencidos = await _e.Cola().VencidosAsync(2, default);

        TareaDeMensajesAlDeudor.TamanoDeTanda.Should().Be(200);
        vencidos.Should().Equal(temprano.Mensaje.Id, medio.Mensaje.Id);
        (await _e.Cola().VencidosAsync(200, default)).Should().Equal(temprano.Mensaje.Id, medio.Mensaje.Id, tarde.Mensaje.Id);
    }

    [Fact]
    public async Task Reevalua_con_el_motor_y_difiere_lo_que_ya_no_cabe_ese_dia()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        await _e.YaEnviadoAsync(_e.Ana, MessageChannel.Email, DebtorMessagePurpose.Informative, new DateTime(2026, 12, 1, 9, 30, 0), "previo-1");

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Deferred);
        ultimo.Motivos().Should().Equal(ContactWindowReason.DailyLimitReached);
        ultimo.DueAt.Should().Be(MensajesDePrueba.Utc(new DateTime(2026, 12, 2, 7, 0, 0)));
        _sms.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancela_el_de_cobranza_de_un_credito_que_se_puso_al_dia()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Mora(_e.Ana, _e.Credito));
        _e.Posicion(_e.Credito, diasDeMora: 0);
        await _e.Db.SaveChangesAsync();

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Cancelled);
        ultimo.ReasonText.Should().Be("El crédito se puso al día antes del envío.");
        ultimo.ActorKind.Should().Be(ActorKind.Process);
        _sms.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_mora_el_de_cobranza_sale()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Mora(_e.Ana, _e.Credito));
        _e.Posicion(_e.Credito, diasDeMora: 12);
        await _e.Db.SaveChangesAsync();

        await CorrerMensajesAsync();

        (await _e.UltimoAsync(m)).Kind.Should().Be(DebtorMessageEventKind.Sent);
    }

    // ---------------------------------------------------------------------------------------------------------- envío --

    [Fact]
    public async Task Lee_el_destino_del_maestro_al_despachar_y_lo_copia_en_el_evento_Sent()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        _e.Ana.Mobile = "3157778882";
        await _e.Db.SaveChangesAsync();

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Sent);
        ultimo.DestinationAddress.Should().Be("3157778882", "el cambio de contacto antes del envío se respeta (C57)");
        ultimo.ProviderReference.Should().Be($"REF-{m.PublicId:N}");
        ultimo.ExternalOutcome.Should().Be(ExternalOutcome.Ok);
        ultimo.AttemptNumber.Should().Be(1);
        _sms.Enviados.Should().ContainSingle(x => x.Destino == "3157778882" && x.MensajePublicId == m.PublicId && x.Cuerpo == m.RenderedBody);
        (await _e.Db.DebtorMessages.AsNoTracking().SingleAsync()).ChannelCode.Should().Be("PROVEEDOR-SMS", "el mensaje no cambia tras nacer");
    }

    [Fact]
    public async Task Ok_del_simulado_fuera_de_produccion_es_Simulated_con_el_destino()
    {
        var m = (await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms))).Mensaje;

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Simulated);
        ultimo.DestinationAddress.Should().Be("3001234564");
        _e.Simulado.Enviados.Should().ContainSingle();
    }

    [Fact]
    public async Task En_produccion_el_simulado_no_sale_y_queda_Simulated()
    {
        _e.Opciones.AmbienteDePruebas = false;
        var m = (await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms))).Mensaje;

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Simulated);
        ultimo.ReasonText.Should().Contain("simulado");
        _e.Simulado.Enviados.Should().BeEmpty("la guardia RealDelivery le quita el efecto: no se llama al canal");
    }

    [Fact]
    public async Task ChannelUnavailable_difiere_y_tras_los_reintentos_falla_con_alerta()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        _sms.Respuestas.Enqueue(ExternalOutcome.ChannelUnavailable);
        _sms.Respuestas.Enqueue(ExternalOutcome.ChannelUnavailable);
        _sms.Respuestas.Enqueue(ExternalOutcome.ChannelUnavailable);

        await CorrerMensajesAsync();
        var primero = await _e.UltimoAsync(m);
        _e.G.UtcNow = primero.DueAt!.Value;
        await CorrerMensajesAsync();
        var segundo = await _e.UltimoAsync(m);
        _e.G.UtcNow = segundo.DueAt!.Value;
        await CorrerMensajesAsync();

        primero.Kind.Should().Be(DebtorMessageEventKind.Deferred);
        primero.Motivos().Should().Equal(ContactWindowReason.ChannelUnavailable);
        primero.DueAt.Should().BeAfter(MensajesDePrueba.Utc(new DateTime(2026, 12, 1, 10, 0, 0)));
        segundo.Kind.Should().Be(DebtorMessageEventKind.Deferred);
        segundo.AttemptNumber.Should().Be(2);
        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Failed, "Mensajes.Reintentos = 3");
        ultimo.AttemptNumber.Should().Be(3);
        await _e.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.MensajeAlDeudorFallido), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ExternalOutcome.Rejected)]
    [InlineData(ExternalOutcome.InvalidData)]
    public async Task Rechazado_o_dato_invalido_falla_con_alerta_al_jefe_de_cartera(ExternalOutcome respuesta)
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        _sms.Respuestas.Enqueue(respuesta);

        await CorrerMensajesAsync();

        var ultimo = await _e.UltimoAsync(m);
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Failed);
        ultimo.ExternalOutcome.Should().Be(respuesta);
        await _e.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a =>
                a.TypeCode == TiposDeAlerta.MensajeAlDeudorFallido && a.EntityPublicId == m.PublicId
                && a.DedupKey == $"{TiposDeAlerta.MensajeAlDeudorFallido}:{m.PublicId:N}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_registro_del_intento_es_idempotente_por_mensaje_e_intento()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        var comando = new RegisterDebtorMessageAttemptCommand(m.PublicId, 1, PasoDeDespacho.Envio)
        {
            Outcome = ExternalOutcome.Ok, Destino = "3001234564", ReferenciaExterna = "REF-1",
        };

        var primero = await _e.Registro().Handle(comando, default);
        var segundo = await _e.Registro().Handle(comando, default);

        primero.Value.Should().Be(DebtorMessageEventKind.Sent);
        segundo.Value.Should().Be(DebtorMessageEventKind.Sent);
        (await _e.EventosAsync(m)).Should().HaveCount(2, "el encolado y un solo envío");
    }

    // ------------------------------------------------------------------------------------------------------- entregas --

    [Fact]
    public async Task Entregas_registra_Delivered_o_Failed_de_los_enviados_de_las_ultimas_72_horas()
    {
        var fallido = (await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Beto, _e.Credito, "op-2", MessageChannel.Sms))).Mensaje;
        var entregado = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, "op-1", MessageChannel.Sms));
        await CorrerMensajesAsync();
        fallido.ChannelCode.Should().Be(CodigosDeCanalExterno.Simulado);
        (await _e.UltimoAsync(fallido)).Kind.Should().Be(DebtorMessageEventKind.Simulated);
        var viejo = await _e.YaEnviadoAsync(_e.Beto, MessageChannel.Sms, DebtorMessagePurpose.Informative, new DateTime(2026, 11, 27, 9, 0, 0), "viejo-1");

        _e.EnLocal(new DateTime(2026, 12, 1, 11, 0, 0));
        _sms.Entrega = DeliveryState.Delivered;
        _e.Simulado.Entrega = DeliveryState.Failed;
        await CorrerEntregasAsync();

        (await _e.UltimoAsync(entregado)).Kind.Should().Be(DebtorMessageEventKind.Delivered);
        (await _e.UltimoAsync(fallido)).Kind.Should().Be(DebtorMessageEventKind.Failed, "fuera de producción el simulado también se consulta");
        (await _e.UltimoAsync(viejo)).Kind.Should().Be(DebtorMessageEventKind.Sent, "pasó de las 72 horas");
        _e.Simulado.Consultados.Should().ContainSingle();
    }

    [Fact]
    public async Task Entregas_no_consulta_el_correo_que_no_informa_la_entrega()
    {
        var m = (await _e.EncolarYGuardarAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito))).Mensaje;
        await CorrerMensajesAsync();

        await CorrerEntregasAsync();

        (await _e.UltimoAsync(m)).Kind.Should().Be(DebtorMessageEventKind.Sent, "el correo no tiene consulta: su Sent es el último evento");
        _e.Correo.Consultados.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------- activación y ámbito --

    [Fact]
    public async Task Las_dos_salen_si_Cartera_no_esta_activada()
    {
        var m = await ConProveedorAsync(MensajesDePrueba.Abono(_e.Ana, _e.Credito, canal: MessageChannel.Sms));
        var setup = await _e.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        setup.ActivatedAt = null;
        await _e.Db.SaveChangesAsync();

        await CorrerMensajesAsync();
        await CorrerEntregasAsync();

        (await _e.EventosAsync(m)).Should().ContainSingle();
        _sms.Enviados.Should().BeEmpty();
    }

    [Fact]
    public void Son_tareas_programadas_por_cooperativa_con_su_cadencia()
    {
        ITareaProgramada mensajes = new TareaDeMensajesAlDeudor();
        ITareaProgramada entregas = new TareaDeEntregasAlDeudor();
        var ahora = new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5));

        mensajes.Nombre.Should().Be("cartera.mensajes");
        entregas.Nombre.Should().Be("cartera.entregas");
        mensajes.DebeCorrer(ahora, null).Should().BeTrue();
        mensajes.DebeCorrer(ahora, ahora.AddSeconds(-30)).Should().BeFalse();
        mensajes.DebeCorrer(ahora, ahora.AddMinutes(-1)).Should().BeTrue("cada minuto");
        entregas.DebeCorrer(ahora, ahora.AddMinutes(-5)).Should().BeFalse();
        entregas.DebeCorrer(ahora, ahora.AddMinutes(-15)).Should().BeTrue();
    }
}
