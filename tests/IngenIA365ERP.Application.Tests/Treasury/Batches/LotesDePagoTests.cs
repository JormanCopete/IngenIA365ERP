using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Treasury;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Batches;

/// <summary>
/// Feature 015, US10, T0130 (FR-110, FR-111, FR-113, FR-116, FR-213, FR-214): el lote de pago. La cuenta de origen es de pagos, vinculada, del banco del formato y no
/// del fondo sin uso; la vista previa usa el formato vigente del banco o el genérico; el GMF se estima; enviarlo pide la aprobación por monto y quien preparó no
/// aprueba; un rechazo lo devuelve a borrador; una orden va en un solo lote; el portal del banco no tiene formato; el pago a tercero entra por archivo o portal.
/// Prueba escrita, sin correr.
/// </summary>
public class LotesDePagoTests
{
    private readonly EscenarioDeTesoreria _t = new();

    public LotesDePagoTests()
    {
        var generico = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos().AplicarA(generico, bankId: null);
        _t.E.Db.BankFileFormats.Add(generico);
        _t.E.Db.SaveChanges();
    }

    private CreatePaymentBatchCommandHandler Crear() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Contabilidad, _t.Cerrojo, _t.Numerador, _t.Validador, _t.Fondo,
        _t.Parametros, _t.E.Reloj);

    private CreatePaymentBatchCommand Pedido(Guid cuenta, PaymentBatchForm forma, params PaymentOrder[] ordenes) =>
        new(cuenta, forma, Receipts.EscenarioDeRecaudo.Hoy, ordenes.Select(o => o.PublicId).ToList()) { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Arma_el_lote_con_el_formato_generico_el_total_y_la_cantidad()
    {
        var a = _t.Orden(1_500_000m);
        var b = _t.Orden(500_000m);
        var r = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, a, b), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.FormatCode.Should().Be(FormatosGenericosDeRecaudoYTesoreria.Pagos);
        r.Value.Total.Should().Be(2_000_000m);
        r.Value.OrderCount.Should().Be(2);
        r.Value.Status.Should().Be(PaymentBatchState.Draft);
        a.Status.Should().Be(PaymentOrderState.InBatch);
    }

    [Fact]
    public async Task Desde_el_fondo_sin_uso_aprobado_no_hay_lote()
    {
        var r = await Crear().Handle(Pedido(_t.FondoDeLiquidez.PublicId, PaymentBatchForm.File, _t.Orden()), default);
        r.Error.Code.Should().Be("Treasury.Batch.SourceIsLiquidityFund");
    }

    [Fact]
    public async Task Sin_vinculo_contable_no_hay_lote()
    {
        _t.Contabilidad.VinculoAsync(_t.Pagos.PublicId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IngenIA365ERP.Application.Common.Integration.Accounting.VinculoDto?>(null));
        (await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, _t.Orden()), default)).Error.Code.Should().Be("Treasury.BankAccount.NotLinked");
    }

    [Fact]
    public async Task Un_formato_de_otro_banco_no_sirve()
    {
        var otro = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos().AplicarA(otro, bankId: _t.BancoSinCodigo.Id);
        otro.Code = "PAGOS-OTRO";
        _t.E.Db.BankFileFormats.Add(otro);
        _t.E.Db.SaveChanges();
        var r = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, _t.Orden()) with { BankFileFormatPublicId = otro.PublicId }, default);
        r.Error.Code.Should().Be("Treasury.Batch.FormatMismatch");
    }

    [Fact]
    public async Task Una_orden_en_otro_lote_vigente_queda_pendiente()
    {
        var orden = _t.Orden();
        (await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, orden), default)).IsSuccess.Should().BeTrue();
        orden.Status = PaymentOrderState.Ready;
        var otra = _t.Orden();
        var r = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, orden, otra), default);
        r.Value.Pending.Should().ContainSingle(p => p.OrderPublicId == orden.PublicId && p.Reason == "AlreadyInBatch");
    }

    [Fact]
    public async Task El_portal_del_banco_no_lleva_formato_y_admite_pago_a_tercero()
    {
        var r = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.BankPortal, _t.Orden(forma: PaymentForm.ThirdPartyPayment)), default);
        r.Value.FormatCode.Should().BeNull();
        r.Value.OrderCount.Should().Be(1);
    }

    [Fact]
    public async Task Enviar_pide_la_aprobacion_por_monto_y_aprobar_va_por_el_motor()
    {
        var creado = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, _t.Orden(3_000_000m)), default);
        var enviado = await new SubmitPaymentBatchCommandHandler(_t.E.Db, _t.Alcance, _t.Cerrojo, new AprobacionDiferidaEnTesoreria(_t.E.Db, _t.Motor, _t.E.Actor),
            _t.Fondo, _t.E.Reloj).Handle(new SubmitPaymentBatchCommand(creado.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        enviado.Value.Status.Should().Be(PaymentBatchState.PendingApproval);
        await _t.Motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.Subject == ApprovalSubjects.PaymentBatchApproval && s.Amount == 3_000_000m
            && s.PermisoLimitado == LotesDePago.PermisoDeAprobar && s.Participantes.Contains(_t.E.Cajera.Id)), Arg.Any<CancellationToken>());

        _t.Motor.DecidirAsync(Arg.Any<DecisionDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DecisionResultDto>(ErroresDeTesoreria.LoteAutoaprobado()));
        var aprobado = await new ApprovePaymentBatchCommandHandler(_t.E.Db, _t.Alcance, _t.Cerrojo, _t.Motor, new GuardasDeAprobacionDelLote(_t.E.Db, _t.Listas, _t.E.Alertas))
            .Handle(new ApprovePaymentBatchCommand(creado.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        aprobado.Error.Code.Should().Be("Treasury.Batch.SelfApproval");
    }

    [Fact]
    public async Task La_aprobacion_completa_deja_el_lote_y_sus_ordenes_aprobados_y_la_devolucion_en_borrador()
    {
        var orden = _t.Orden();
        var creado = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, orden), default);
        var lote = _t.E.Db.PaymentBatches.Single();
        lote.Status = PaymentBatchState.PendingApproval;
        _t.E.Db.SaveChanges();
        var aprobable = new AprobableLoteDePago(_t.E.Db, _t.E.Reloj);
        (await aprobable.AlAprobarAsync(new ApprovalRequest { SourcePublicId = creado.Value.PublicId }, default)).IsSuccess.Should().BeTrue();
        lote.Status.Should().Be(PaymentBatchState.Approved);
        orden.Status.Should().Be(PaymentOrderState.Approved);
        await aprobable.AlDevolverAsync(new ApprovalRequest { SourcePublicId = creado.Value.PublicId }, "corregir", default);
        lote.Status.Should().Be(PaymentBatchState.Draft);
    }

    [Fact]
    public async Task Un_beneficiario_en_lista_vinculante_impide_aprobar()
    {
        var creado = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, _t.Orden()), default);
        _t.Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Application.Treasury.Orders.ResultadoDeListasDeTesoreria([_t.Beneficiaria.Id], []));
        var r = await new GuardasDeAprobacionDelLote(_t.E.Db, _t.Listas, _t.E.Alertas).AntesDeAprobarAsync(_t.E.Db.PaymentBatches.Single(b => b.PublicId == creado.Value.PublicId), default);
        r.Error.Code.Should().Be("Treasury.Compliance.BindingListMatch");
    }

    [Fact]
    public async Task La_vista_previa_escribe_las_lineas_con_el_formato()
    {
        var creado = await Crear().Handle(Pedido(_t.Pagos.PublicId, PaymentBatchForm.File, _t.Orden(1_234_567m)), default);
        var r = await new PreviewPaymentBatchQueryHandler(_t.E.Db, _t.Alcance, _t.E.Reloj).Handle(new PreviewPaymentBatchQuery(creado.Value.PublicId), default);
        r.Value.Lines.Should().ContainSingle(l => l.Amount == 1_234_567m && l.Text != null && l.Text.Contains("1234567.00"));
    }
}
