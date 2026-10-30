using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Application.Treasury.Tareas;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Results;

/// <summary>
/// Feature 015, US11, T0153 (R-18, R-19; FR-118 a FR-121, FR-123, FR-213, FR-215): <c>ResultadoDelGiro</c> v1 sin cambios, a nombre de la persona que lo registra,
/// clave <c>Confirmation:{orden:N}</c> y <c>Amount = NetAmount</c>; el mismo resultado dos veces no cambia nada; el registro manual exige soporte; «girada» nunca al
/// marcar enviado; el rechazo técnico devuelve las órdenes sin resultado; <c>PagoDeOrdenGirado</c> con la fecha del débito y sin hecho en un rechazo; las órdenes de
/// Recaudo se informan por su puerto; la vigilancia sólo alerta. Prueba escrita, sin correr.
/// </summary>
public class ResultadoDelGiroTests
{
    private readonly EscenarioDeResultados _r = new();

    private EscenarioDeTesoreria T => _r.T;

    private static readonly DateOnly Debito = new(2026, 11, 21);

    [Fact]
    public async Task Girada_emite_el_resultado_v1_a_nombre_de_la_persona_con_el_neto()
    {
        var orden = T.Orden(800_000m);
        _r.LoteEnviado(orden);

        var r = await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, "REF-1", null, null, _r.Soporte(orden.PublicId))
            { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        orden.Status.Should().Be(PaymentOrderState.Disbursed);
        var mensaje = await T.E.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == ResultadoDelGiroV1.Type);
        mensaje.OriginEventKey.Should().Be(ClavesDeEvento.ConfirmacionPor(orden.OriginOrderPublicId));
        mensaje.PayloadJson.Should().Contain("\"outcome\":\"Disbursed\"").And.Contain("800000");
        r.Value.RecordedByName.Should().Be(T.E.Supervisor.Username);
        r.Value.EmittedMessagePublicId.Should().Be(mensaje.PublicId);
    }

    [Fact]
    public async Task El_mismo_resultado_dos_veces_no_cambia_nada()
    {
        var orden = T.Orden();
        _r.LoteEnviado(orden);
        var soporte = _r.Soporte(orden.PublicId);
        await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, null, null, null, soporte) { OperationKey = Guid.NewGuid() }, default);

        var otra = await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, null, null, null, soporte) { OperationKey = Guid.NewGuid() }, default);

        otra.IsSuccess.Should().BeTrue();
        otra.Value.Repeated.Should().BeTrue();
        T.E.Db.DisbursementResults.Count().Should().Be(1);
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(1);
    }

    [Fact]
    public async Task El_registro_manual_exige_soporte()
    {
        var orden = T.Orden();
        _r.LoteEnviado(orden);

        var r = await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, null, null, null, null) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Treasury.Result.SupportRequired");
        T.E.Db.DisbursementResults.Should().BeEmpty();
    }

    [Fact]
    public async Task Antes_de_enviar_no_hay_resultado_y_rechazada_despues_de_girada_tampoco()
    {
        var lista = T.Orden(estado: PaymentOrderState.Approved);
        (await _r.Registrar().Handle(new RegisterDisbursementResultCommand(lista.PublicId, "Disbursed", Debito, null, null, null, _r.Soporte(lista.PublicId))
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Treasury.Result.NotSent");

        var orden = T.Orden();
        _r.LoteEnviado(orden);
        var soporte = _r.Soporte(orden.PublicId);
        await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, null, null, null, soporte) { OperationKey = Guid.NewGuid() }, default);
        (await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Rejected", Debito, null, "Cuenta embargada", null, soporte)
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Treasury.Result.InvalidTransition");
    }

    [Fact]
    public async Task Girada_emite_el_pago_girado_con_la_fecha_del_debito_y_el_rechazo_no()
    {
        var girada = T.Orden(500_000m);
        var rechazada = T.Orden(300_000m);
        var (lote, _) = _r.LoteEnviado(girada, rechazada);

        await _r.Registrar().Handle(new RegisterDisbursementResultCommand(girada.PublicId, "Disbursed", Debito, "REF", null, null, _r.Soporte(girada.PublicId))
            { OperationKey = Guid.NewGuid() }, default);
        lote.Status.Should().Be(PaymentBatchState.PartiallyConfirmed);
        await _r.Registrar().Handle(new RegisterDisbursementResultCommand(rechazada.PublicId, "Rejected", Debito, null, "Cuenta inexistente", null,
            _r.Soporte(rechazada.PublicId)) { OperationKey = Guid.NewGuid() }, default);

        var pagos = T.E.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == PagoDeOrdenGiradoV1.Type).ToList();
        pagos.Should().ContainSingle();
        pagos[0].OriginPublicId.Should().Be(girada.PublicId);
        pagos[0].PayloadJson.Should().Contain("2026-11-21");
        lote.Status.Should().Be(PaymentBatchState.Confirmed);
        rechazada.Status.Should().Be(PaymentOrderState.Rejected);
    }

    [Fact]
    public async Task Marcar_enviado_no_deja_nada_girado()
    {
        var orden = T.Orden(estado: PaymentOrderState.Approved);
        var (lote, archivo) = _r.LoteEnviado(orden);
        T.E.Db.Entry(archivo).Property(a => a.SentAt).CurrentValue = null;
        lote.Status = PaymentBatchState.FileGenerated;
        orden.Status = PaymentOrderState.Approved;
        T.E.Db.SaveChanges();

        var r = await new MarkPaymentFileSentCommandHandler(T.E.Db, T.Alcance, T.E.Actor, T.Cerrojo, T.E.Reloj)
            .Handle(new MarkPaymentFileSentCommand(archivo.PublicId, "Carga", "Autoriza") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        orden.Status.Should().Be(PaymentOrderState.Sent);
        T.E.Db.DisbursementResults.Should().BeEmpty();
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(0);
    }

    [Fact]
    public async Task El_rechazo_tecnico_del_archivo_devuelve_las_ordenes_sin_resultado()
    {
        var orden = T.Orden();
        var (lote, archivo) = _r.LoteEnviado(orden);

        var r = await new MarkPaymentFileRejectedByBankCommandHandler(T.E.Db, T.Alcance, T.Cerrojo, _r.Auditoria, T.E.Reloj)
            .Handle(new MarkPaymentFileRejectedByBankCommand(archivo.PublicId, "Estructura inválida") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.RejectedByBankAt.Should().NotBeNull();
        lote.Status.Should().Be(PaymentBatchState.RejectedByBank);
        orden.Status.Should().Be(PaymentOrderState.Ready);
        T.E.Db.PaymentBatchItems.Single().IsLive.Should().BeFalse();
        T.E.Db.DisbursementResults.Should().BeEmpty();
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(0);
    }

    [Fact]
    public async Task Las_ordenes_de_Recaudo_se_informan_por_su_puerto_despues_de_sus_cerrojos()
    {
        var orden = T.Orden(origen: PaymentOrderOrigin.Receipts, clase: "PayerCreditBalanceRefund");
        _r.LoteEnviado(orden);

        await _r.Registrar().Handle(new RegisterDisbursementResultCommand(orden.PublicId, "Disbursed", Debito, null, null, null, _r.Soporte(orden.PublicId))
            { OperationKey = Guid.NewGuid() }, default);

        Received.InOrder(() =>
        {
            T.Recaudo.TomarCerrojosAsync(Arg.Is<IReadOnlyList<Guid>>(l => l.Contains(orden.PublicId)), Arg.Any<CancellationToken>());
            T.Recaudo.InformarAsync(orden.PublicId, "Disbursed", Debito, null, Arg.Any<CancellationToken>());
        });
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(0);
    }

    [Fact]
    public async Task Registrar_en_bloque_es_todo_o_nada()
    {
        var a = T.Orden();
        var b = T.Orden(estado: PaymentOrderState.Ready);
        _r.LoteEnviado(a);
        var soporte = _r.Soporte(a.PublicId);
        var handler = new RegisterDisbursementResultsBulkCommandHandler(T.E.Db, T.Alcance, T.E.Actor, T.Cerrojo, T.Segregacion, T.Aprobacion, T.Resultados, _r.Auditoria);

        var r = await handler.Handle(new RegisterDisbursementResultsBulkCommand(
        [
            new(a.PublicId, "Disbursed", Debito, null, null, null),
            new(b.PublicId, "Disbursed", Debito, null, null, null),
        ], soporte) { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue("la segunda no está enviada");
    }

    [Fact]
    public async Task La_vigilancia_alerta_la_orden_sin_resultado_sin_registrar_nada()
    {
        T.E.Parametro(ParametrosDeRecaudoYTesoreria.GirosDiasSinResultado, "3", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        var orden = T.Orden();
        _r.LoteEnviado(orden);

        var n = await new VigilanciaDeTesoreria(T.E.Db, T.Parametros, _r.Calendario, T.E.Alertas, T.E.Reloj).OrdenesSinResultadoAsync(default);

        n.Should().Be(1);
        await T.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaOrdenSinResultado), Arg.Any<CancellationToken>());
        T.E.Db.DisbursementResults.Should().BeEmpty();
        orden.Status.Should().Be(PaymentOrderState.Sent);
    }

    [Fact]
    public async Task La_anulacion_del_origen_en_la_bandeja_emite_anulada_a_nombre_de_quien_anulo()
    {
        var orden = T.Orden(estado: PaymentOrderState.Ready);
        orden.VoidReason = "Desistió";

        await T.Resultados.AnuladaPorElOrigenAsync(orden, new() { CentralUserId = Guid.NewGuid(), Name = "Persona de Cartera" }, default);
        await T.E.Db.SaveChangesAsync();

        orden.Status.Should().Be(PaymentOrderState.Voided);
        var r = T.E.Db.DisbursementResults.Single();
        r.Outcome.Should().Be(DisbursementOutcome.Voided);
        r.RecordedByName.Should().Be("Persona de Cartera");
        r.RecordedByUserId.Should().BeNull();
        T.E.Db.IntegrationMessages.Count(m => m.Type == ResultadoDelGiroV1.Type).Should().Be(1);
    }
}
