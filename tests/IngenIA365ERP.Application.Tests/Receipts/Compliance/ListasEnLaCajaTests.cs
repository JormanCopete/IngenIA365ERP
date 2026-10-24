using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, US14, T0234 (FR-098, FR-189, FR-203): pagador, titular y quien efectúa contra las listas en el contexto del recaudo; la vinculante deja la operación
/// <c>Blocked</c> sin medios ni confirmación; la no vinculante con <c>Sigue</c> confirma con alerta; con <c>Retiene</c> la confirmación pide retener, el efectivo queda en
/// custodia de la sesión (<c>DineroRetenidoEnCustodia</c>) y el oficial —otra persona— lo libera con soporte (<c>CustodiaLiberada</c>) o lo devuelve por orden de pago.
/// Prueba escrita, sin correr.
/// </summary>
public class ListasEnLaCajaTests
{
    private readonly CajaConContratos _k = new();

    private AuditoriaDeRecaudo Auditoria => new(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    private async Task<CashOperationDto> OperacionAsync(ScreeningOutcomeR listas, decimal efectivo = 100_000m)
    {
        _k.C.E.Db.SaveChanges();
        _k.Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(listas);
        await _k.AbrirAsync();
        var op = (await new CreateCashOperationCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Reloj, _k.Listas)
            .Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        if (listas == ScreeningOutcomeR.BindingMatch) return op;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, efectivo)]), default);
        return (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(efectivo)]), default)).Value;
    }

    private WithholdCashOperationCommandHandler Retener() =>
        new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Numerador, _k.C.E.Parametros, _k.Listas, _k.C.Emision, Auditoria, _k.C.E.Reloj);

    [Fact]
    public async Task La_vinculante_bloquea_la_operacion_desde_que_nace_y_no_se_le_registran_medios()
    {
        var op = await OperacionAsync(ScreeningOutcomeR.BindingMatch);

        op.Status.Should().Be(CashOperationState.Blocked);
        var medios = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(100_000m)]), default);
        medios.Error.Code.Should().Be(ErroresDeRecaudo.ListaVinculante().Code);
    }

    [Fact]
    public async Task La_no_vinculante_con_sigue_confirma()
    {
        var op = await OperacionAsync(ScreeningOutcomeR.NonBindingMatch);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
    }

    [Fact]
    public async Task Con_retiene_el_efectivo_queda_en_custodia_y_el_oficial_lo_libera()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftListasNoVinculantesEfecto, CumplimientoDeCaja.EfectoRetiene);
        var op = await OperacionAsync(ScreeningOutcomeR.NonBindingMatch);

        var pide = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        pide.Error.Code.Should().Be(ErroresDeRecaudo.CodigoRetencionRequerida);
        var retenida = await Retener().Handle(new WithholdCashOperationCommand(op.PublicId) { OperationKey = Guid.NewGuid() }, default);
        retenida.IsSuccess.Should().BeTrue(retenida.IsFailure ? retenida.Error.Code : null);
        retenida.Value.Status.Should().Be(CashOperationState.Withheld);
        (await _k.C.E.Db.CashMovements.SingleAsync(m => m.Kind == CashMovementKindR.Custody)).Amount.Should().Be(100_000m);
        (await _k.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == DineroRetenidoEnCustodiaV1.Type)).Should().BeTrue();

        var liberar = new ReleaseWithheldOperationCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Numerador, _k.C.E.Segregacion, _k.C.Emision,
            Auditoria, _k.C.E.Reloj);
        var mismo = await liberar.Handle(new ReleaseWithheldOperationCommand(op.PublicId, Guid.NewGuid(), "Verificado") { OperationKey = Guid.NewGuid() }, default);
        mismo.Error.Code.Should().Be(ErroresDeRecaudo.LiberacionPorQuienRetuvo().Code);
        _k.C.E.Como(_k.C.E.Supervisor);
        var liberada = await liberar.Handle(new ReleaseWithheldOperationCommand(op.PublicId, Guid.NewGuid(), "Verificado") { OperationKey = Guid.NewGuid() }, default);
        liberada.Value.Status.Should().Be(CashOperationState.Preparing);
        (await _k.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == CustodiaLiberadaV1.Type)).Should().BeTrue();

        _k.C.E.Como(_k.C.E.Cajera);
        var confirmada = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? confirmada.Error.Code : null);
    }

    [Fact]
    public async Task El_oficial_puede_devolver_el_dinero_retenido_por_orden_de_pago()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftListasNoVinculantesEfecto, CumplimientoDeCaja.EfectoRetiene);
        var op = await OperacionAsync(ScreeningOutcomeR.NonBindingMatch);
        await Retener().Handle(new WithholdCashOperationCommand(op.PublicId) { OperationKey = Guid.NewGuid() }, default);
        var ordenes = Substitute.For<IOrdenesDePagoDeTesoreria>();
        ordenes.EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>()).Returns(new OrdenDePagoEmitidaDto(Guid.NewGuid(), "OP-1", "Ready"));

        var r = await new RefundWithheldMoneyByOrderCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Numerador, ordenes, _k.C.Emision, Auditoria,
            _k.C.E.Reloj).Handle(new RefundWithheldMoneyByOrderCommand(op.PublicId, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        r.Value.Status.Should().Be(CashOperationState.Abandoned);
        await ordenes.Received(1).EmitirAsync(Arg.Is<SolicitudDeOrdenDePago>(s => s.OrderKind == RefundWithheldMoneyByOrderCommandHandler.ClaseDeOrden && s.NetAmount == 100_000m),
            Arg.Any<CancellationToken>());
    }
}
