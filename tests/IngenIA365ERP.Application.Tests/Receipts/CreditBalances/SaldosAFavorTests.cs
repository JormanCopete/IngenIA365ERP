using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.CreditBalances;

/// <summary>
/// Feature 015, US5, T0172 (FR-053, FR-054; interfaces.md §2): el saldo a favor de un pagador se devuelve por orden de pago a Tesorería en la misma transacción que su
/// baja (clase <c>PayerCreditBalanceRefund</c>), con el hecho <c>SaldoAFavorDePagadorResuelto</c>; «girada» lo cierra y «rechazada» lo reabre; el de Cartera se
/// resuelve con <c>RefundThroughCollector</c> y sale con su orden y el hecho <c>SaldoAFavorDeCarteraDevuelto</c>; aplicarlo con un recibo nuevo del mismo pagador.
/// Prueba escrita, sin correr.
/// </summary>
public class SaldosAFavorTests
{
    private readonly CajaConContratos _k = new();
    private readonly IOrdenesDePagoDeTesoreria _ordenes = Substitute.For<IOrdenesDePagoDeTesoreria>();
    private readonly Guid _orden = Guid.NewGuid();

    public SaldosAFavorTests()
    {
        _ordenes.EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>()).Returns(new OrdenDePagoEmitidaDto(_orden, "OP-000001", "Ready"));
        _k.C.E.Como(_k.C.E.Supervisor);
    }

    private AuditoriaDeRecaudo Auditoria => new(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    private PayerCreditBalance Saldo(decimal valor = 80_000m)
    {
        var s = SaldosAFavor.Reconocer(_k.C.E.Db, _k.Asociado.Id, PayerCreditBalanceOrigin.VoidAfterSessionClose, Guid.NewGuid(), "AN-1", _k.C.E.OficinaA.Id, valor,
            EscenarioDeRecaudo.Hoy.AddDays(-40), null, DateTime.UtcNow);
        _k.C.E.Db.SaveChanges();
        SaldosAFavor.Reconocido(_k.C.E.Db, s, null, DateTime.UtcNow);
        _k.C.E.Db.SaveChanges();
        return s;
    }

    private RefundPayerCreditBalanceByOrderCommandHandler Devolver() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _ordenes, _k.C.Emision, Auditoria, _k.C.E.Reloj);

    private ResultadoDeOrdenesDeRecaudo Resultado() => new(_k.C.E.Db, _k.C.Cerrojo, Auditoria, _k.C.E.Reloj);

    [Fact]
    public async Task Se_devuelve_por_orden_de_pago_en_la_misma_transaccion_con_su_hecho()
    {
        var saldo = Saldo();

        var r = await Devolver().Handle(new RefundPayerCreditBalanceByOrderCommand(saldo.PublicId, 80_000m, null) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Balance.Should().Be(0m);
        r.Value.AgeDays.Should().Be(40);
        await _ordenes.Received(1).EmitirAsync(Arg.Is<SolicitudDeOrdenDePago>(s => s.OrderKind == ClasesDeOrdenDeRecaudo.DevolucionDeSaldoDePagador && s.NetAmount == 80_000m),
            Arg.Any<CancellationToken>());
        _k.C.E.Db.IntegrationMessages.Count(m => m.Type == SaldoAFavorDePagadorResueltoV1.Type).Should().Be(1);
        saldo.Status.Should().Be(PayerCreditBalanceState.Open, "se cierra cuando Tesorería informa «girada»");
    }

    [Fact]
    public async Task Girada_lo_cierra_y_rechazada_lo_reabre()
    {
        var saldo = Saldo();
        await Devolver().Handle(new RefundPayerCreditBalanceByOrderCommand(saldo.PublicId, 80_000m, null) { OperationKey = Guid.NewGuid() }, default);

        await Resultado().InformarAsync(_orden, "Rejected", EscenarioDeRecaudo.Hoy, "Cuenta inexistente", default);
        await _k.C.E.Db.SaveChangesAsync();
        saldo.Balance.Should().Be(80_000m);
        saldo.Status.Should().Be(PayerCreditBalanceState.Open);
        _k.C.E.Db.PayerCreditBalanceMovements.Should().Contain(m => m.Kind == PayerCreditBalanceMovementKind.RefundRejected);

        var otra = Guid.NewGuid();
        _ordenes.EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>()).Returns(new OrdenDePagoEmitidaDto(otra, "OP-000002", "Ready"));
        await Devolver().Handle(new RefundPayerCreditBalanceByOrderCommand(saldo.PublicId, 80_000m, null) { OperationKey = Guid.NewGuid() }, default);
        await Resultado().InformarAsync(otra, "Disbursed", EscenarioDeRecaudo.Hoy, null, default);
        await _k.C.E.Db.SaveChangesAsync();
        saldo.Status.Should().Be(PayerCreditBalanceState.Resolved);
    }

    [Fact]
    public async Task No_se_devuelve_mas_que_el_saldo()
    {
        var saldo = Saldo(10_000m);
        (await Devolver().Handle(new RefundPayerCreditBalanceByOrderCommand(saldo.PublicId, 20_000m, null) { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Receipts.CreditBalance.InsufficientBalance");
    }

    [Fact]
    public async Task El_saldo_de_Cartera_se_resuelve_por_el_recaudador_y_sale_con_su_orden()
    {
        _k.Obligaciones.ResolverSaldoAFavorAsync(Arg.Any<SolicitudDeResolucion>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<ComprobanteDeResolucionDto>(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeResolucionDto(Guid.NewGuid(), "SAF-1",
                _k.CreditoA, ConceptoDeObligacion.CreditBalance, FormaDeResolucion.RefundThroughCollector, 30_000m, null, CajaConContratos.Saldos())));
        var handler = new RefundLendingCreditBalanceByOrderCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.Obligaciones, _ordenes, _k.C.Emision,
            Auditoria, _k.C.E.Reloj);

        var r = await handler.Handle(new RefundLendingCreditBalanceByOrderCommand(_k.CreditoA, 30_000m, _k.Asociado.PublicId, _k.C.E.OficinaA.PublicId, null)
            { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        await _k.Obligaciones.Received(1).ResolverSaldoAFavorAsync(Arg.Is<SolicitudDeResolucion>(s => s.Form == FormaDeResolucion.RefundThroughCollector),
            Arg.Any<CancellationToken>());
        await _ordenes.Received(1).EmitirAsync(Arg.Is<SolicitudDeOrdenDePago>(s => s.OrderKind == ClasesDeOrdenDeRecaudo.DevolucionDeSaldoDeCartera), Arg.Any<CancellationToken>());
        _k.C.E.Db.IntegrationMessages.Count(m => m.Type == SaldoAFavorDeCarteraDevueltoV1.Type).Should().Be(1);
        _k.C.E.Db.PayerCreditBalances.Single().Origin.Should().Be(PayerCreditBalanceOrigin.LendingRefund);
    }
}
