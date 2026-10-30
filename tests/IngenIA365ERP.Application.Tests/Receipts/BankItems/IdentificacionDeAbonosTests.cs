using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, US6, T0187 (R-17; FR-043, FR-068, FR-071, FR-074, FR-171, FR-198): el abono identificado genera un recibo de canal banco aplicado en forma no interactiva
/// con la fecha valor del movimiento y la modalidad del canal; se divide entre personas y productos con suma exacta; cada línea es su unidad con su clave y se reprocesa
/// sin duplicar; el excedente de la modalidad queda a favor del pagador; la consignación a una cuenta va por <c>ConsignarAsync</c>; la identificación tardía usa la fecha
/// más antigua que admite el contrato, el recibo lo dice y alerta al módulo. Prueba escrita, sin correr.
/// </summary>
public class IdentificacionDeAbonosTests
{
    private readonly BancosDePrueba _b = new();

    private Task<IngenIA365ERP.Application.Common.Models.Result<IdentificacionDeAbonoDto>> Identificar(Guid linea, IReadOnlyList<ParteDeAbono> partes, bool resto = false,
        Guid? corrida = null) =>
        _b.Identificar().Handle(new IdentifyBankItemCommand(linea, partes, RemainderAsUnidentified: resto, RunPublicId: corrida) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task El_recibo_de_canal_banco_lleva_la_fecha_valor_del_movimiento_y_la_modalidad_del_canal()
    {
        var abono = _b.Abono(150_000m, diasAtras: 2);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(150_000m)]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.Channel.Should().Be(ReceiptChannel.Bank);
        recibo.ValueDate.Should().Be(abono.MovementDate);
        recibo.BankStatementLinePublicId.Should().Be(abono.PublicId);
        recibo.OwnBankAccountPublicId.Should().Be(_b.Cuenta.PublicId);
        await _b.K.Obligaciones.Received().AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Form == FormaDeAplicacion.NonInteractive
            && s.Payments.Single().Modality == ModalidadDePago.CycleInstallment && s.Payments.Single().ValueDate == abono.MovementDate), Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.Id == abono.Id)).RecaudoStatus.Should().Be(StatementLineRecaudoStatus.Identified);
        (await _b.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == ReciboDeBancoEmitidoV1.Type)).Should().BeTrue();
    }

    [Fact]
    public async Task Se_divide_entre_productos_con_suma_exacta()
    {
        var abono = _b.Abono(200_000m);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(120_000m), _b.CuotaB(80_000m)]);

        r.IsSuccess.Should().BeTrue();
        (await _b.K.C.E.Db.ReceiptApplications.CountAsync()).Should().Be(2);
        var mal = await Identificar(_b.Abono(100_000m).PublicId, [_b.CuotaA(60_000m)]);
        mal.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDivisionNoCuadra);
        var demas = await Identificar(_b.Abono(100_000m).PublicId, [_b.CuotaA(160_000m)]);
        demas.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAbonoSobreIdentificado);
        var otra = await Identificar(abono.PublicId, [_b.CuotaA(1m)]);
        otra.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAbonoYaIdentificado);
    }

    [Fact]
    public async Task El_resto_puede_quedar_como_partida()
    {
        var abono = _b.Abono(100_000m);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(70_000m)], resto: true);

        r.IsSuccess.Should().BeTrue();
        var partida = await _b.K.C.E.Db.UnidentifiedItems.SingleAsync();
        partida.Amount.Should().Be(30_000m);
        (await _b.K.C.E.Db.BankItemIdentifications.CountAsync(i => i.BankStatementLinePublicId == abono.PublicId)).Should().Be(2);
    }

    [Fact]
    public async Task Una_linea_que_el_contrato_rechaza_queda_en_la_corrida_y_se_reprocesa_con_la_misma_clave()
    {
        _b.RechazarCredito(_b.K.CreditoA);
        var abono = _b.Abono(90_000m);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(90_000m)]);

        r.IsSuccess.Should().BeTrue("un rechazo del contrato no es un error de la operación (FR-171)");
        var linea = r.Value.Run.Items.Single();
        linea.Outcome.Should().Be(ProcessingLineOutcome.Rejected);
        (await _b.K.C.E.Db.IssuedReceipts.AnyAsync()).Should().BeFalse("no queda recibo ni número consumido");
        (await _b.K.C.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.Id == abono.Id)).RecaudoStatus.Should().Be(StatementLineRecaudoStatus.Pending);

        _b.K.Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RespuestaDeAplicacionDto(ci.Arg<SolicitudDeAplicacion>().Payments.Select(CajaConContratos.Aplicado).ToList(), []));
        var otra = await _b.Reprocesar().Handle(new RetryProcessingRunLineCommand(r.Value.Run.PublicId, linea.PublicId) { OperationKey = Guid.NewGuid() }, default);

        otra.IsSuccess.Should().BeTrue();
        otra.Value.Items.Single().Outcome.Should().Be(ProcessingLineOutcome.Applied);
        otra.Value.Items.Single().IdempotencyKey.Should().Be(linea.IdempotencyKey, "el reproceso repite la clave");
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_excedente_de_la_modalidad_queda_a_favor_del_pagador()
    {
        _b.K.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "", CajaConContratos.Simulacion(s, null, []) with
            {
                AppliedAmount = 80_000m, Surplus = s.Amount - 80_000m,
            });
        });
        var abono = _b.Abono(100_000m);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(100_000m)]);

        r.IsSuccess.Should().BeTrue();
        var saldo = await _b.K.C.E.Db.PayerCreditBalances.SingleAsync();
        saldo.Amount.Should().Be(20_000m);
        saldo.Origin.Should().Be(PayerCreditBalanceOrigin.NonInteractiveSurplus);
        (await _b.K.C.E.Db.IssuedReceipts.SingleAsync()).TotalApplied.Should().Be(80_000m);
    }

    [Fact]
    public async Task La_transferencia_a_una_cuenta_va_por_ConsignarAsync()
    {
        var abono = _b.Abono(50_000m);

        var r = await Identificar(abono.PublicId, [_b.Aportes(50_000m)]);

        r.IsSuccess.Should().BeTrue();
        await _b.K.Cuentas.Received().ConsignarAsync(Arg.Is<SolicitudDeConsignacion>(s => s.Form == FormaDeAplicacion.NonInteractive
            && s.Collector.Code == ClavesDeRecaudo.Codigo && s.Deposits.Single().ValueDate == abono.MovementDate), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_identificacion_tardia_usa_la_fecha_mas_antigua_que_admite_el_contrato_y_alerta_al_modulo()
    {
        var minima = EscenarioDeRecaudo.Hoy.AddDays(-5);
        _b.K.Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeAplicacion>();
            return new RespuestaDeAplicacionDto(s.Payments.Select(p => p.ValueDate < minima
                ? new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Rejected, "Lending.Payment.ValueDateOutOfRange", "Fuera de rango.", null,
                    new Dictionary<string, object?> { ["minDate"] = minima })
                : CajaConContratos.Aplicado(p)).ToList(), []);
        });
        var abono = _b.Abono(60_000m, diasAtras: 20);

        var r = await Identificar(abono.PublicId, [_b.CuotaA(60_000m)]);

        r.IsSuccess.Should().BeTrue();
        r.Value.LateValueDate.Should().BeTrue();
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.ValueDate.Should().Be(minima);
        recibo.OriginalValueDate.Should().Be(abono.MovementDate, "el recibo dice la fecha del movimiento");
        await _b.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<Application.Common.Alerts.AlertaALevantar>(a => a.TypeCode == "Recaudo.FechaValorTardia"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_posible_duplicado_se_decide_antes_de_identificarlo()
    {
        var abono = _b.Abono(40_000m, posibleDuplicado: true);

        (await Identificar(abono.PublicId, [_b.CuotaA(40_000m)])).Error.Code.Should().Be("Receipts.BankItem.DuplicatePending");
        var decision = await _b.Duplicado().Handle(new DecideBankItemDuplicateCommand(abono.PublicId, true) { OperationKey = Guid.NewGuid() }, default);

        decision.IsSuccess.Should().BeTrue();
        (await _b.K.C.E.Db.TreasuryBankStatementLines.SingleAsync(l => l.Id == abono.Id)).DuplicateDecision.Should().Be(StatementLineDuplicateDecision.Duplicate);
        (await _b.K.C.E.Db.BankItemIdentifications.SingleAsync()).Outcome.Should().Be(BankItemOutcome.PossibleDuplicateDismissed);
    }

    [Fact]
    public async Task Repetir_la_linea_con_su_clave_no_duplica_el_recibo()
    {
        var clave = AbonosDeRecaudo.Clave(Guid.NewGuid(), 1);
        var linea = new LineaNoInteractiva(clave, ReceiptChannel.Bank, _b.K.Asociado.PublicId, EscenarioDeRecaudo.Hoy, 30_000m, _b.K.C.E.OficinaA.Id,
            [new ParteNoInteractiva(ApplicationTarget.Loan, _b.K.CreditoA, _b.K.Asociado.PublicId, 30_000m)], ClaseDeMedioDeRecaudo.Transfer);

        var primera = await _b.Aplicacion.AplicarAsync(linea, default);
        var segunda = await _b.Aplicacion.AplicarAsync(linea, default);

        primera.Outcome.Should().Be(ProcessingLineOutcome.Applied);
        segunda.Outcome.Should().Be(ProcessingLineOutcome.AlreadyApplied);
        segunda.Receipt!.PublicId.Should().Be(primera.Receipt!.PublicId);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Fuera_del_alcance_de_oficinas_el_abono_no_existe()
    {
        var abono = _b.Abono(10_000m);
        _b.K.C.E.Alcance.SoloOficinas(_b.K.C.E.OficinaB.Id);

        (await Identificar(abono.PublicId, [_b.CuotaA(10_000m)])).Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoEncontrado);
    }
}
