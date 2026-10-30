using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// Feature 015, US7, T0203 (R-16, R-17; FR-078 a FR-081, FR-171): el giro desde una línea del extracto se reparte en el orden del archivo; el menor deja pendientes las que
/// no cubre y el mayor exige dejar el excedente como saldo de la pagaduría; cada línea se aplica en su unidad con <c>ExternalPaymasterDeduction</c>, la línea del archivo,
/// la fecha valor del descuento y la clave <c>REC:{giro:N}:{línea:N}</c>; el rechazo del contrato deja la línea como saldo de la pagaduría con la alerta
/// <c>Recaudo.NoAplicadoDePagaduria</c>; resolver ese saldo exige soporte. Prueba escrita, sin correr.
/// </summary>
public class GiroDePagaduriaTests
{
    private readonly PagaduriasDePrueba _p = new();

    private Task<IngenIA365ERP.Application.Common.Models.Result<PaymasterRemittanceDto>> RegistrarAsync(PayrollDeductionFile f, decimal abono, decimal? valor = null,
        bool excedente = false) =>
        _p.RegistrarGiro().Handle(new RegisterPaymasterRemittanceCommand(_p.Pagaduria.PublicId, _p.B.Abono(abono).PublicId, [f.PublicId], valor,
            SurplusAsCreditBalance: excedente) { OperationKey = Guid.NewGuid() }, default);

    private Task<IngenIA365ERP.Application.Common.Models.Result<PaymasterRemittanceDto>> AplicarAsync(Guid giro) =>
        _p.AplicarGiro().Handle(new ApplyPaymasterRemittanceCommand(giro) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task El_giro_menor_cubre_en_el_orden_del_archivo_y_deja_pendientes_las_demas()
    {
        var f = await _p.RespondidoAsync(3);

        var r = await RegistrarAsync(f, 250_000m);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Items.Select(l => l.Amount).Should().Equal(100_000m, 100_000m, 50_000m);
        r.Value.Surplus.Should().Be(0m);
        var lineas = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.Where(l => l.PayrollDeductionFileId == f.Id).OrderBy(l => l.Id).ToListAsync();
        lineas.Select(l => l.RemittedAmount).Should().Equal(100_000m, 100_000m, 50_000m);
        (await _p.B.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == GiroDePagaduriaRecibidoV1.Type)).Should().BeTrue();
        (await _p.B.K.C.E.Db.BankItemIdentifications.SingleAsync()).Outcome.Should().Be(BankItemOutcome.PaymasterRemittance);
    }

    [Fact]
    public async Task El_giro_mayor_exige_dejar_el_excedente_como_saldo_de_la_pagaduria()
    {
        var f = await _p.RespondidoAsync(1);

        var sin = await RegistrarAsync(f, 130_000m);
        sin.Error.Code.Should().Be(ErroresDeRecaudo.CodigoGiroExcedePendiente);

        var con = await RegistrarAsync(f, 130_000m, excedente: true);
        con.IsSuccess.Should().BeTrue(con.IsFailure ? con.Error.Code : null);
        con.Value.Surplus.Should().Be(30_000m);
        var saldo = await _p.B.K.C.E.Db.PayerCreditBalances.SingleAsync();
        saldo.Kind.Should().Be(PayerCreditBalanceKind.Paymaster);
        saldo.Origin.Should().Be(PayerCreditBalanceOrigin.PaymasterSurplus);
        saldo.PersonId.Should().Be(_p.Pagaduria.Id);
        saldo.Balance.Should().Be(30_000m);
    }

    [Fact]
    public async Task Cada_linea_se_aplica_con_el_descuento_de_la_pagaduria_su_fecha_y_su_clave()
    {
        var fecha = EscenarioDeRecaudo.Hoy.AddDays(-4);
        var f = await _p.RespondidoAsync(2, fecha: fecha);
        var giro = (await RegistrarAsync(f, 200_000m)).Value;

        var r = await AplicarAsync(giro.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Applied.Should().Be(2);
        r.Value.Status.Should().Be(PaymasterRemittanceState.Applied);
        var lineas = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.Where(l => l.PayrollDeductionFileId == f.Id).ToListAsync();
        foreach (var l in lineas)
            await _p.B.K.Obligaciones.Received().AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Form == FormaDeAplicacion.NonInteractive
                && s.Payments.Single().MeansClass == ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction
                && s.Payments.Single().DeductionFileLinePublicId == l.DeductionFileLinePublicId
                && s.Payments.Single().ValueDate == fecha
                && s.Payments.Single().IdempotencyKey == $"REC:{giro.PublicId:N}:{l.DeductionFileLinePublicId:N}"), Arg.Any<CancellationToken>());
        var recibos = await _p.B.K.C.E.Db.IssuedReceipts.ToListAsync();
        recibos.Should().HaveCount(2).And.OnlyContain(x => x.Channel == ReceiptChannel.Paymaster && x.PayerPersonId == _p.Pagaduria.Id);
        (await _p.B.K.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ReciboDePagaduriaEmitidoV1.Type)).Should().Be(2);

        var otra = await AplicarAsync(giro.PublicId);
        otra.Error.Code.Should().Be(ErroresDeRecaudo.GiroYaAplicado().Code);
    }

    [Fact]
    public async Task El_rechazo_del_contrato_deja_la_linea_como_saldo_de_la_pagaduria_y_alerta()
    {
        var f = await _p.RespondidoAsync(2);
        var giro = (await RegistrarAsync(f, 200_000m)).Value;
        _p.B.RechazarCredito(_p.B.K.CreditoB, "Lending.Payment.PaymasterReferenceInvalid");

        var r = await AplicarAsync(giro.PublicId);

        r.Value.Applied.Should().Be(1);
        r.Value.Rejected.Should().Be(1);
        r.Value.Status.Should().Be(PaymasterRemittanceState.PartiallyApplied);
        var rechazada = r.Value.Items.Single(l => l.Outcome == PaymasterRemittanceLineOutcome.Rejected);
        rechazada.RejectionCode.Should().Be("Lending.Payment.PaymasterReferenceInvalid");
        rechazada.CreditBalanceNumber.Should().Be(giro.Number);
        var saldo = await _p.B.K.C.E.Db.PayerCreditBalances.SingleAsync();
        saldo.Origin.Should().Be(PayerCreditBalanceOrigin.PaymasterUnapplied);
        saldo.Kind.Should().Be(PayerCreditBalanceKind.Paymaster);
        await _p.B.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoNoAplicadoDePagaduria), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolver_el_saldo_de_la_pagaduria_exige_soporte()
    {
        var f = await _p.RespondidoAsync(1);
        await RegistrarAsync(f, 130_000m, excedente: true);
        var saldo = await _p.B.K.C.E.Db.PayerCreditBalances.SingleAsync();

        var r = await new ResolvePaymasterBalanceCommandHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance, _p.B.K.C.E.Actor, _p.B.Auditoria, Substitute.For<MediatR.ISender>())
            .Handle(new ResolvePaymasterBalanceCommand(saldo.PublicId, "Refund", 30_000m, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.SoporteDePagaduriaRequerido().Code);
    }

    [Fact]
    public async Task Un_saldo_de_un_pagador_no_se_resuelve_como_de_pagaduria()
    {
        var p = _p.B.K.C.E.Db;
        var saldo = Application.Receipts.CreditBalances.SaldosAFavor.Reconocer(p, _p.B.K.Asociado.Id, PayerCreditBalanceOrigin.NonInteractiveSurplus, Guid.NewGuid(), "RB-1",
            _p.B.K.C.E.OficinaA.Id, 1_000m, EscenarioDeRecaudo.Hoy, null, DateTime.UtcNow);
        await p.SaveChangesAsync();

        var r = await new ResolvePaymasterBalanceCommandHandler(p, _p.B.K.C.E.Alcance, _p.B.K.C.E.Actor, _p.B.Auditoria, Substitute.For<MediatR.ISender>())
            .Handle(new ResolvePaymasterBalanceCommand(saldo.PublicId, "Refund", 1_000m, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.SaldoNoEsDePagaduria().Code);
    }
}
