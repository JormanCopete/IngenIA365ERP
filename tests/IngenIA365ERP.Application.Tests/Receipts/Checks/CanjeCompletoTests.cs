using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Checks;

/// <summary>
/// Feature 015, R2, T0427 (FR-084, FR-087): el lote de canje <c>RCD_ClearingBatches</c> de la remesa del día deja los cheques en canje; el abono del extracto que paga los
/// cheques los marca pagados sin registrarlos uno por uno y queda identificado en Recaudo; un cheque devuelto sigue la devolución de FR-060 y el lote lo cuenta como
/// devuelto. Prueba escrita, sin correr.
/// </summary>
public class CanjeCompletoTests
{
    private readonly ChequesDePrueba _c = new();
    private readonly ICuentasBancariasPropias _cuentas = Substitute.For<ICuentasBancariasPropias>();
    private readonly IMovimientosBancariosParaRecaudo _movimientos = Substitute.For<IMovimientosBancariosParaRecaudo>();
    private readonly Guid _cuenta = Guid.NewGuid();

    public CanjeCompletoTests()
    {
        _cuentas.PorPublicIdAsync(_cuenta, Arg.Any<CancellationToken>())
            .Returns(new CuentaBancariaPropiaDto(_cuenta, "RECAUDO", "Banco", "1007", "Savings", "****1234", _c.K.C.E.OficinaA.PublicId, true, true));
    }

    private ReceivedCheck Cheque(string numero, decimal valor)
    {
        var e = _c.K.C.E;
        var c = new ReceivedCheck
        {
            DraweeBankId = _c.Banco.Id, CheckNumber = numero, AccountNumber = "4410001", PayerPersonId = e.PersonaCajera.Id, IssueDate = EscenarioDeRecaudo.Hoy,
            Amount = valor, SamePlace = true, PresentationDeadline = EscenarioDeRecaudo.Hoy.AddDays(15), BranchId = e.OficinaA.Id, Status = ReceivedCheckState.Received,
        };
        e.Db.ReceivedChecks.Add(c);
        e.Db.SaveChanges();
        return c;
    }

    private CreateClearingBatchCommandHandler Armar() =>
        new(_c.K.C.E.Db, _c.K.C.E.Alcance, _c.K.C.E.Actor, _c.K.C.Cerrojo, _c.K.C.Numerador, _cuentas, _c.Auditoria, _c.K.C.E.Reloj);

    private MatchClearingBatchStatementCommandHandler Cruzar() =>
        new(_c.K.C.E.Db, _c.K.C.E.Alcance, _c.K.C.E.Actor, _c.K.C.Cerrojo, _movimientos, _c.Auditoria, _c.K.C.E.Reloj);

    private LineaDeExtractoDto Abono(decimal valor) =>
        new(Guid.NewGuid(), _cuenta, EscenarioDeRecaudo.Hoy.AddDays(2), null, valor, 0m, "CANJE", null, null, null, false);

    [Fact]
    public async Task El_lote_deja_los_cheques_en_canje_y_el_abono_del_extracto_los_paga()
    {
        var a = Cheque("1", 100_000m);
        var b = Cheque("2", 200_000m);
        var c = Cheque("3", 50_000m);
        var lote = await Armar().Handle(new CreateClearingBatchCommand(_cuenta, [a.PublicId, b.PublicId, c.PublicId]) { OperationKey = Guid.NewGuid() }, default);
        lote.IsSuccess.Should().BeTrue(lote.IsFailure ? lote.Error.Code : null);
        (await _c.K.C.E.Db.ReceivedChecks.ToListAsync()).Should().OnlyContain(x => x.Status == ReceivedCheckState.InClearing && x.ClearingBatchId != null);

        var abono = Abono(300_000m);
        _movimientos.LineaAsync(abono.PublicId, Arg.Any<CancellationToken>()).Returns(abono);
        var r = await Cruzar().Handle(new MatchClearingBatchStatementCommand(lote.Value.PublicId, abono.PublicId, [a.PublicId, b.PublicId]) { OperationKey = Guid.NewGuid() },
            default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.PaidAmount.Should().Be(300_000m);
        r.Value.Status.Should().Be(ClearingBatchState.PartiallyPaid);
        await _movimientos.Received(1).MarcarIdentificacionAsync(abono.PublicId, 300_000m, Arg.Any<CancellationToken>());
        (await _c.K.C.E.Db.BankItemIdentifications.SingleAsync()).Outcome.Should().Be(BankItemOutcome.ClearingBatch);
    }

    [Fact]
    public async Task El_abono_que_no_coincide_con_los_cheques_no_los_paga()
    {
        var a = Cheque("4", 100_000m);
        var lote = await Armar().Handle(new CreateClearingBatchCommand(_cuenta, [a.PublicId]) { OperationKey = Guid.NewGuid() }, default);
        var abono = Abono(90_000m);
        _movimientos.LineaAsync(abono.PublicId, Arg.Any<CancellationToken>()).Returns(abono);

        var r = await Cruzar().Handle(new MatchClearingBatchStatementCommand(lote.Value.PublicId, abono.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Receipts.ClearingBatch.AmountMismatch");
    }

    [Fact]
    public async Task Un_cheque_no_va_en_dos_lotes()
    {
        var a = Cheque("5", 100_000m);
        (await Armar().Handle(new CreateClearingBatchCommand(_cuenta, [a.PublicId]) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();

        var otra = await Armar().Handle(new CreateClearingBatchCommand(_cuenta, [a.PublicId]) { OperationKey = Guid.NewGuid() }, default);

        otra.Error.Code.Should().Be("Receipts.ClearingBatch.CheckNotEligible");
    }

    [Fact]
    public void El_lote_recalcula_lo_pagado_y_lo_devuelto()
    {
        var lote = new ClearingBatch { TotalAmount = 300m };
        var pagado = new ReceivedCheck { Amount = 100m, Status = ReceivedCheckState.Paid };
        var devuelto = new ReceivedCheck { Amount = 200m, Status = ReceivedCheckState.Returned };

        CanjeDeCheques.Recalcular(lote, [pagado, devuelto]);

        (lote.PaidAmount, lote.ReturnedAmount, lote.Status).Should().Be((100m, 200m, ClearingBatchState.Closed));
    }
}
