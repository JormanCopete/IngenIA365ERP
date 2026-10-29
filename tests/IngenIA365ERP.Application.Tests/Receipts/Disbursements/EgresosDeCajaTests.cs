using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Disbursements;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Disbursements;

/// <summary>
/// Feature 015, R1, T0367 (FR-051 a FR-056, FR-101, FR-191): el egreso de caja.
/// <list type="bullet">
/// <item>el retiro va por <c>SimularAsync</c> → <c>RetirarAsync(MeansClass = Cash, Signers, Receiver)</c> con la clave <c>REC:{egreso:N}:{cuenta:N}</c> y se entrega en la
/// misma operación; el efectivo esperado de la sesión baja; el GMF lo liquidó Captaciones y el egreso lo muestra sin causarlo otra vez;</item>
/// <item>sin efectivo suficiente se rechaza <b>antes</b> de pedir la autorización; el tope diario de la caja también;</item>
/// <item>sobre la atribución, Captaciones deja la solicitud y la caja responde 428 con ella; repetir con la aprobación entrega;</item>
/// <item>la cancelación con entrega entrega lo que simuló Captaciones (E-02 d);</item>
/// <item>el saldo a favor de Cartera se resuelve con <c>ResolverSaldoAFavorAsync</c> por el recaudador; el de un pagador baja con <c>RefundedByCash</c>;</item>
/// <item>los egresos varios están apagados por defecto; encendidos, piden el concepto de egreso, su tope y el aprobador presente;</item>
/// <item>quien recibe se cruza con las listas: una coincidencia vinculante no entrega.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class EgresosDeCajaTests
{
    private readonly EgresosDePrueba _p = new();

    private Task<Result<CashDisbursementDto>> RegistrarAsync(RegisterCashDisbursementCommand c) => _p.Registrar().Handle(c, default);

    [Fact]
    public async Task El_retiro_va_por_el_contrato_con_su_clave_y_baja_el_efectivo_de_la_sesion()
    {
        await _p.K.AbrirAsync();
        var clave = Guid.NewGuid();

        var r = await RegistrarAsync(_p.Retiro(120_000m, clave));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Kind.Should().Be(CashDisbursementKind.SavingsWithdrawal);
        r.Value.GmfAmount.Should().Be(_p.GmfDeCaptaciones);
        r.Value.GmfByOrigin.Should().BeTrue("el GMF del retiro lo liquida Captaciones");
        await _p.K.Cuentas.Received(1).RetirarAsync(Arg.Is<SolicitudDeRetiro>(s => s.MeansClass == ClaseDeMedioDeRecaudo.Cash
            && s.IdempotencyKey == EgresosDeCaja.Clave(clave, _p.Ahorros) && s.SignerPersonPublicIds.Contains(_p.K.Asociado.PublicId)
            && s.ReceiverPersonPublicId == _p.K.Asociado.PublicId), Arg.Any<CancellationToken>());
        (await _p.EfectivoEsperadoAsync()).Should().Be(500_000m - 120_000m);
        (await _p.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == EgresoDeCajaEntregadoV1.Type)).Should().Be(1);
        (await _p.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == GmfDeEgresoCausadoV1.Type)).Should().Be(0, "nunca lo que el origen ya liquidó");

        var otra = await RegistrarAsync(_p.Retiro(120_000m, clave));
        otra.Value.PublicId.Should().Be(r.Value.PublicId, "repetir con la misma clave no entrega dos veces");
        await _p.K.Cuentas.Received(1).RetirarAsync(Arg.Any<SolicitudDeRetiro>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_efectivo_suficiente_se_rechaza_antes_de_pedir_la_autorizacion()
    {
        await _p.K.AbrirAsync();

        var r = await RegistrarAsync(_p.Retiro(600_000m));

        r.Error.Code.Should().Be(ErroresDeEgreso.CodigoEfectivoInsuficiente);
        await _p.K.Cuentas.DidNotReceive().RetirarAsync(Arg.Any<SolicitudDeRetiro>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_tope_diario_de_la_caja_limita_lo_entregado_en_el_dia()
    {
        _p.K.C.C1.DailyCashOutCap = 150_000m;
        await _p.K.C.E.Db.SaveChangesAsync();
        await _p.K.AbrirAsync();

        (await RegistrarAsync(_p.Retiro(100_000m))).IsSuccess.Should().BeTrue();
        var r = await RegistrarAsync(_p.Retiro(60_000m));

        r.Error.Code.Should().Be(ErroresDeEgreso.CodigoTopeDiario);
    }

    [Fact]
    public async Task Sobre_la_atribucion_responde_428_con_la_solicitud_de_Captaciones_y_repetir_entrega()
    {
        await _p.K.AbrirAsync();
        var solicitud = Guid.NewGuid();
        _p.AprobacionDeCaptaciones = solicitud;
        var clave = Guid.NewGuid();

        var pedida = await RegistrarAsync(_p.Retiro(200_000m, clave));
        pedida.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPresenciaRequerida);
        ((ErrorConDatos)pedida.Error).Data.ToString().Should().Contain(solicitud.ToString());
        (await _p.K.C.E.Db.CashDisbursements.CountAsync()).Should().Be(0);

        var aprobada = await RegistrarAsync(_p.Retiro(200_000m, clave) with { ApprovalRequestPublicId = solicitud });
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : null);
    }

    [Fact]
    public async Task La_cancelacion_con_entrega_entrega_lo_que_simulo_Captaciones()
    {
        await _p.K.AbrirAsync();
        var pedido = new RegisterCashDisbursementCommand(_p.K.Sesion.PublicId, CashDisbursementKind.AccountClosingDelivery, _p.K.Asociado.PublicId, 250_000m, _p.Ahorros,
            SignerPersonPublicIds: [_p.K.Asociado.PublicId]) { OperationKey = Guid.NewGuid() };

        (await RegistrarAsync(pedido)).Error.Code.Should().Be(ErroresDeEgreso.CodigoValorDeCancelacion);

        var r = await RegistrarAsync(pedido with { Amount = 300_000m, OperationKey = Guid.NewGuid() });
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await _p.K.Cuentas.Received(1).RetirarAsync(Arg.Is<SolicitudDeRetiro>(s => s.CloseAccount && s.Amount == 300_000m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_saldo_a_favor_de_Cartera_lo_resuelve_el_recaudador_y_causa_el_GMF_de_Recaudo()
    {
        await _p.K.AbrirAsync();

        var r = await RegistrarAsync(new RegisterCashDisbursementCommand(_p.K.Sesion.PublicId, CashDisbursementKind.LendingCreditBalanceRefund, _p.K.Asociado.PublicId,
            100_000m, _p.Credito) { OperationKey = Guid.NewGuid() });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await _p.K.Obligaciones.Received(1).ResolverSaldoAFavorAsync(Arg.Is<SolicitudDeResolucion>(s => s.Form == FormaDeResolucion.RefundThroughCollector
            && s.Concept == ConceptoDeObligacion.CreditBalance && s.Amount == 100_000m), Arg.Any<CancellationToken>());
        r.Value.GmfByOrigin.Should().BeFalse();
        r.Value.GmfAmount.Should().Be(400m, "la devolución dispone de una cuenta contable a favor de un tercero: 100.000 × 0,004 por Gmf.* de RCD");
        (await _p.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == GmfDeEgresoCausadoV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task El_saldo_a_favor_de_un_pagador_baja_con_la_devolucion_en_efectivo()
    {
        await _p.K.AbrirAsync();
        var saldo = SaldosAFavor.Reconocer(_p.K.C.E.Db, _p.K.Asociado.Id, PayerCreditBalanceOrigin.VoidAfterSessionClose, Guid.NewGuid(), "AN-1", _p.K.C.E.OficinaA.Id,
            80_000m, EscenarioDeRecaudo.Hoy, null, DateTime.UtcNow);
        await _p.K.C.E.Db.SaveChangesAsync();

        var r = await RegistrarAsync(new RegisterCashDisbursementCommand(_p.K.Sesion.PublicId, CashDisbursementKind.PayerCreditBalanceRefund, _p.K.Asociado.PublicId,
            80_000m, PayerCreditBalancePublicId: saldo.PublicId) { OperationKey = Guid.NewGuid() });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var leido = await _p.K.C.E.Db.PayerCreditBalances.AsNoTracking().FirstAsync(s => s.Id == saldo.Id);
        leido.Balance.Should().Be(0m);
        leido.Status.Should().Be(PayerCreditBalanceState.Resolved);
        (await _p.K.C.E.Db.PayerCreditBalanceMovements.CountAsync(m => m.Kind == PayerCreditBalanceMovementKind.RefundedByCash)).Should().Be(1);
    }

    [Fact]
    public async Task Los_egresos_varios_estan_apagados_por_defecto_y_encendidos_piden_concepto_tope_y_aprobador()
    {
        await _p.K.AbrirAsync();
        var concepto = new MiscConcept { Code = "PAPEL", Name = "Papelería", Direction = MiscConceptDirection.Expense, MaxAmount = 50_000m };
        _p.K.C.E.Db.MiscConcepts.Add(concepto);
        await _p.K.C.E.Db.SaveChangesAsync();
        RegisterCashDisbursementCommand Vario(decimal valor) => new(_p.K.Sesion.PublicId, CashDisbursementKind.MiscExpense, _p.K.Asociado.PublicId, valor,
            MiscConceptPublicId: concepto.PublicId, Reason: "Resmas") { OperationKey = Guid.NewGuid() };

        (await RegistrarAsync(Vario(20_000m))).Error.Code.Should().Be(ErroresDeEgreso.CodigoEgresosVariosApagados);

        _p.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaEgresosVarios, "true");
        (await RegistrarAsync(Vario(60_000m))).Error.Code.Should().Be(ErroresDeEgreso.CodigoSobreElTopeDelConcepto);
        var r = await RegistrarAsync(Vario(20_000m));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await _p.K.C.Motor.Received().SolicitarAsync(Arg.Is<Application.Common.Approvals.SolicitudDeAprobacion>(s => s.Subject == ApprovalSubjects.CashDisbursementApproval),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quien_recibe_se_cruza_con_las_listas_y_una_coincidencia_vinculante_no_entrega()
    {
        await _p.K.AbrirAsync();
        var tercero = new Domain.Entities.Core.Person { FirstName = "Tito", LastName = "Tercero", TaxId = "3001" };
        _p.K.C.E.Db.People.Add(tercero);
        await _p.K.C.E.Db.SaveChangesAsync();
        _p.K.Listas.EvaluarAsync(Arg.Is<IReadOnlyCollection<int>>(x => x.Contains(tercero.Id)), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ScreeningOutcomeR.BindingMatch);

        var r = await RegistrarAsync(_p.Retiro(50_000m) with { ReceivedByPersonPublicId = tercero.PublicId });

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoListaVinculante);
        await _p.K.Cuentas.DidNotReceive().RetirarAsync(Arg.Any<SolicitudDeRetiro>(), Arg.Any<CancellationToken>());
    }
}
