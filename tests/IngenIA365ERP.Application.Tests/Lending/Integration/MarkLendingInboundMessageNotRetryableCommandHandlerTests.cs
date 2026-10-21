using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0573 (C95, C102; api.md §16.2; data-model §21.1): marcar no reprocesable un recibo <c>Rejected</c> llena <c>NotRetryable*</c> y emite
/// en la misma transacción <c>ObligacionRechazada</c> <c>NoConvertible</c> con <c>retryable = false</c> y la clave de la última evaluación; Comercial pasa
/// a verla <c>NotIncorporated</c>; un recibo que no está <c>Rejected</c> o que ya se marcó responde <c>Lending.InboundMessage.NotMarkable</c>. Y las
/// consultas de los recibos (lista y detalle).
/// </summary>
public sealed class MarkLendingInboundMessageNotRetryableCommandHandlerTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private MarkLendingInboundMessageNotRetryableCommandHandler Marcar() =>
        new(_v.Db, new MensajesEntrantes(_v.Db), _v.Emision, _v.Anteriores(), _v.G.Alcance, _v.G.Auditoria, _v.G.Actor, _v.G.Reloj);

    private async Task<Guid> NoConvertibleAsync()
    {
        var venta = _v.NuevaVenta();
        var id = await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, periodicidad: "Biweekly"));
        (await _v.ConsumirAsync(id)).Should().BeOfType<ResultadoDeConsumo.Rejected>();
        _v.G.ActuarComo(_v.O.Analista);
        return id;
    }

    [Fact]
    public async Task Marcar_un_rechazado_lo_cierra_y_emite_ObligacionRechazada_NoConvertible_no_reprocesable()
    {
        var mensaje = await NoConvertibleAsync();

        var r = await Marcar().Handle(new MarkLendingInboundMessageNotRetryableCommand(mensaje, "Se anuló la venta en Comercial") { OperationKey = Guid.NewGuid() },
            default);
        _v.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        (recibo.Status, recibo.NotRetryableAt is not null, recibo.NotRetryableByUserId, recibo.NotRetryableReason).Should()
            .Be((InboundMessageStatus.Rejected, true, (int?)_v.O.Analista.Id, "Se anuló la venta en Comercial"));
        var rechazada = (await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Single();
        var ultima = await _v.Db.LendingInboundMessageEvaluations.AsNoTracking().SingleAsync(e => e.Id == recibo.LastEvaluationId);
        rechazada.OriginEventKey.Should().Be(ClavesDeEvento.ConfirmacionPor(ultima.PublicId), "la clave de la última evaluación (C95)");
        r.Value.Response.MessagePublicId.Should().Be(rechazada.PublicId);
        var c = JsonDocument.Parse(rechazada.PayloadJson).RootElement;
        (c.GetProperty("motive").GetString(), c.GetProperty("code").GetString(), c.GetProperty("retryable").GetBoolean())
            .Should().Be(("NoConvertible", ErroresDeVentasACredito.CodigoSinEquivalencia, false));
        (await _v.Consultas().EstadoDeValidacionAsync(mensaje, default)).Status.Should().Be(EstadosDeValidacion.NotIncorporated,
            "no hay crédito: como una venta no incorporada (C102)");
    }

    [Fact]
    public async Task Marcar_dos_veces_o_un_procesado_no_es_marcable()
    {
        var mensaje = await NoConvertibleAsync();
        (await Marcar().Handle(new MarkLendingInboundMessageNotRetryableCommand(mensaje, "Primera") { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        _v.Db.ChangeTracker.Clear();
        var venta = _v.NuevaVenta();
        var (procesado, _) = await _v.VenderAsync(venta);

        var otraVez = await Marcar().Handle(new MarkLendingInboundMessageNotRetryableCommand(mensaje, "Segunda") { OperationKey = Guid.NewGuid() }, default);
        var deUnProcesado = await Marcar().Handle(new MarkLendingInboundMessageNotRetryableCommand(procesado, "No") { OperationKey = Guid.NewGuid() }, default);

        otraVez.Error.Code.Should().Be(ErroresDeVentasACredito.CodigoNoMarcable);
        deUnProcesado.Error.Code.Should().Be(ErroresDeVentasACredito.CodigoNoMarcable);
    }

    [Fact]
    public async Task Un_recibo_inexistente_es_NotFound()
    {
        var r = await Marcar().Handle(new MarkLendingInboundMessageNotRetryableCommand(Guid.NewGuid(), "x") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeVentasACredito.CodigoReciboInexistente);
    }

    [Fact]
    public async Task El_validador_exige_el_motivo() =>
        (await new MarkLendingInboundMessageNotRetryableCommandValidator().ValidateAsync(new MarkLendingInboundMessageNotRetryableCommand(Guid.NewGuid(), " ")))
        .IsValid.Should().BeFalse();

    // ---------------------------------------------------------------------------------------------------------- consultas --

    [Fact]
    public async Task La_lista_y_el_detalle_muestran_las_evaluaciones_y_la_respuesta()
    {
        var venta = _v.NuevaVenta();
        var (mensaje, _) = await _v.VenderAsync(venta);
        await NoConvertibleAsync();

        var lista = await new ListLendingInboundMessagesQueryHandler(_v.Db, _v.G.Alcance).Handle(new ListLendingInboundMessagesQuery(), default);
        var detalle = await new GetLendingInboundMessageQueryHandler(_v.Db, new MensajesEntrantes(_v.Db), _v.G.Alcance, _v.G.Auditoria)
            .Handle(new GetLendingInboundMessageQuery(mensaje), default);

        lista.Value.Items.Should().HaveCount(2);
        var procesado = lista.Value.Items.Single(i => i.MessagePublicId == mensaje);
        (procesado.Status, procesado.ValidationStatus, procesado.Response!.Type).Should().Be(("Processed", EstadosDeValidacion.Validated, ObligacionCreadaV1.Type));
        procesado.Evaluations.Should().ContainSingle();
        lista.Value.Items.Single(i => i.MessagePublicId != mensaje).ValidationStatus.Should().Be(EstadosDeValidacion.Pending);
        detalle.Value.PayloadJson.Should().Contain(venta.Pago.ToString());
        (await new ListLendingInboundMessagesQueryHandler(_v.Db, _v.G.Alcance).Handle(new ListLendingInboundMessagesQuery(Status: InboundMessageStatus.Rejected),
            default)).Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Fuera_del_alcance_de_la_oficina_del_credito_no_se_ve()
    {
        var venta = _v.NuevaVenta();
        var (mensaje, _) = await _v.VenderAsync(venta);
        _v.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;

        var lista = await new ListLendingInboundMessagesQueryHandler(_v.Db, _v.G.Alcance).Handle(new ListLendingInboundMessagesQuery(), default);
        var detalle = await new GetLendingInboundMessageQueryHandler(_v.Db, new MensajesEntrantes(_v.Db), _v.G.Alcance, _v.G.Auditoria)
            .Handle(new GetLendingInboundMessageQuery(mensaje), default);

        lista.Value.Items.Should().BeEmpty();
        detalle.Error.Code.Should().Be(ErroresDeVentasACredito.CodigoReciboInexistente);
    }
}
