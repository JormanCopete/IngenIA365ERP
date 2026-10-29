using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.Disbursements;
using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Egresos;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Disbursements;

/// <summary>
/// Feature 015, R1, T0368 (FR-061, FR-099, FR-205):
/// <list type="bullet">
/// <item>la anulación reversa el retiro por su contrato (con la clave del egreso) y el dinero vuelve como entrada de la sesión abierta de la misma caja; la sesión que
/// lo entregó no cambia; pide otra persona presente; anular dos veces no reversa dos veces;</item>
/// <item>el efectivo entregado es una fila <c>Out</c> del registro de efectivo y su anulación otra con signo contrario (cuenta para topes, umbrales, alertas y el
/// reporte);</item>
/// <item>el GMF de Recaudo (parámetros <c>Gmf.*</c> de <c>RCD</c>): las devoluciones de saldos a favor se causan, el egreso vario sólo con <c>Todos</c>, nada con
/// <c>Ninguno</c>, y nunca lo que el origen ya liquidó.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class AnulacionDeEgresoYGmfTests
{
    private readonly EgresosDePrueba _p = new();

    [Fact]
    public async Task La_anulacion_reversa_por_el_contrato_y_el_dinero_vuelve_a_la_sesion_abierta()
    {
        await _p.K.AbrirAsync();
        var egreso = (await _p.Registrar().Handle(_p.Retiro(120_000m), default)).Value;
        (await _p.EfectivoEsperadoAsync()).Should().Be(380_000m);

        var r = await _p.Anular().Handle(new VoidCashDisbursementCommand(egreso.PublicId, _p.K.Sesion.PublicId, "El asociado devolvió el dinero")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(CashDisbursementStatus.Voided);
        r.Value.VoidingNumber.Should().NotBeNullOrEmpty();
        var guardado = await _p.K.C.E.Db.CashDisbursements.AsNoTracking().FirstAsync(e => e.PublicId == egreso.PublicId);
        await _p.K.Cuentas.Received(1).ReversarAsync(Arg.Is<SolicitudDeReversoDeCuenta>(s => s.IdempotencyKey == guardado.IdempotencyKey), Arg.Any<CancellationToken>());
        (await _p.EfectivoEsperadoAsync()).Should().Be(500_000m, "el egreso sigue como salida y su vuelta es una entrada");
        (await _p.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == EgresoDeCajaAnuladoV1.Type)).Should().Be(1);
        await _p.K.C.Motor.Received().SolicitarAsync(Arg.Is<Application.Common.Approvals.SolicitudDeAprobacion>(s =>
            s.Subject == Domain.Approvals.ApprovalSubjects.CashDisbursementApproval), Arg.Any<CancellationToken>());

        var otra = await _p.Anular().Handle(new VoidCashDisbursementCommand(egreso.PublicId, _p.K.Sesion.PublicId, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        otra.IsSuccess.Should().BeTrue();
        await _p.K.Cuentas.Received(1).ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_dinero_vuelve_solo_a_una_sesion_de_la_misma_caja()
    {
        await _p.K.AbrirAsync();
        var egreso = (await _p.Registrar().Handle(_p.Retiro(50_000m), default)).Value;
        var otraSesion = await _p.K.C.AbrirAsync(_p.K.C.C2, _p.K.C.E.OtroCajero);

        var r = await _p.Anular().Handle(new VoidCashDisbursementCommand(egreso.PublicId, otraSesion.PublicId, "Devuelto") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeEgreso.CodigoNoEsDeLaCaja);
    }

    [Fact]
    public async Task El_efectivo_entregado_y_su_anulacion_quedan_en_el_registro_de_efectivo()
    {
        await _p.K.AbrirAsync();
        var egreso = (await _p.Registrar().Handle(_p.Retiro(70_000m), default)).Value;
        await _p.Anular().Handle(new VoidCashDisbursementCommand(egreso.PublicId, _p.K.Sesion.PublicId, "Devuelto") { OperationKey = Guid.NewGuid() }, default);

        var filas = await _p.K.C.E.Db.ReceiptsCashTransactions.AsNoTracking().Where(t => t.Direction == CashTransactionDirection.Out).ToListAsync();
        filas.Should().HaveCount(2);
        filas.Should().ContainSingle(t => t.DocumentKind == CashTransactionDocumentKind.CashDisbursement && t.CashAmount == 70_000m);
        filas.Should().ContainSingle(t => t.DocumentKind == CashTransactionDocumentKind.Voiding && t.CashAmount == -70_000m);
    }

    [Theory]
    [InlineData(CashDisbursementKind.LendingCreditBalanceRefund, "DevolucionesYPartidas", true)]
    [InlineData(CashDisbursementKind.PayerCreditBalanceRefund, "DevolucionesYPartidas", true)]
    [InlineData(CashDisbursementKind.MiscExpense, "DevolucionesYPartidas", false)]
    [InlineData(CashDisbursementKind.MiscExpense, "Todos", true)]
    [InlineData(CashDisbursementKind.PayerCreditBalanceRefund, "Ninguno", false)]
    [InlineData(CashDisbursementKind.SavingsWithdrawal, "Todos", false)]
    [InlineData(CashDisbursementKind.AccountClosingDelivery, "Todos", false)]
    public void El_GMF_de_Recaudo_sigue_sus_hechos_generadores_y_nunca_lo_que_liquido_el_origen(CashDisbursementKind clase, string hechos, bool causa)
    {
        var gmf = ReglasDeEgresoDeCaja.Gmf(clase, 250_000m, 0.004m, hechos, RedondeoDeGmf.AlPeso);

        gmf.Causado.Should().Be(causa);
        gmf.Valor.Should().Be(causa ? 1_000m : 0m);
        if (clase is CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery) gmf.Motivo.Should().Be("OrigenLoLiquido");
    }

    [Fact]
    public void Una_tarifa_cero_no_causa_y_el_tope_diario_sin_valor_no_limita()
    {
        ReglasDeEgresoDeCaja.Gmf(CashDisbursementKind.PayerCreditBalanceRefund, 250_000m, 0m, "Todos", RedondeoDeGmf.AlPeso).Causado.Should().BeFalse();
        ReglasDeEgresoDeCaja.SuperaElTopeDiario(null, 9_000_000m, 1_000_000m).Should().BeFalse();
        ReglasDeEgresoDeCaja.SuperaElTopeDiario(1_000_000m, 900_000m, 100_001m).Should().BeTrue();
        ReglasDeEgresoDeCaja.AlcanzaElEfectivo(100_000m, 100_000m).Should().BeTrue();
        ReglasDeEgresoDeCaja.AlcanzaElEfectivo(100_000m, 100_001m).Should().BeFalse();
    }

    [Fact]
    public void El_codigo_de_pendiente_de_aprobacion_del_contrato_es_el_de_Captaciones()
    {
        CodigosDelContratoDeCuentas.PendienteDeAprobacion.Should().Be(Application.Lending.Deposits.Operations.ErroresDeMovimientos.CodigoPendienteDeAprobacion);
    }
}
