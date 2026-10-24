using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Checks;

/// <summary>
/// Feature 015, US15, T0214 (FR-060, FR-085, FR-188): la devolución con causal del catálogo reversa en una sola operación sólo las porciones pagadas con el cheque
/// (obligaciones antes que cuentas, clave <c>:reverso</c>); el efectivo del mismo recibo sigue aplicado; el medio queda <c>WrittenOff</c> sin saldo a favor; lo que el cheque
/// respaldaba y ya se entregó queda como cuenta por cobrar al pagador; hecho <c>ChequeRecibidoDevuelto</c>, alerta <c>Recaudo.ChequeDevuelto</c> y lista de giradores;
/// <c>Receipts.Check.AlreadyReturned</c>. El cargo por devolución sigue apagado. Prueba escrita, sin correr.
/// </summary>
public class DevolucionDeChequeTests
{
    private readonly ChequesDePrueba _c = new();

    private Task<IngenIA365ERP.Application.Common.Models.Result<ReceivedCheckDto>> DevolverAsync(Guid cheque, string causa = "01") =>
        _c.Devolver().Handle(new ReturnReceivedCheckCommand(cheque, causa) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Reversa_solo_las_porciones_del_cheque_obligaciones_antes_que_cuentas_y_el_efectivo_sigue()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m), _c.K.CuotaDeAportes(50_000m), _c.K.Cuota(_c.K.CreditoB, 30_000m)],
            [_c.K.Efectivo(30_000m), _c.Medio(150_000m)]);

        var r = await DevolverAsync(cheque.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(ReceivedCheckState.Returned);
        r.Value.ReturnCauseCode.Should().Be("01");
        var medios = await _c.K.C.E.Db.ReceiptMeans.OrderBy(m => m.Ordinal).ToListAsync();
        var delCheque = medios.Single(m => m.ReceivedCheckId == cheque.Id);
        delCheque.Status.Should().Be(ReceiptMeansState.WrittenOff);
        medios.Single(m => m.ReceivedCheckId is null).Status.Should().Be(ReceiptMeansState.Active);
        var porciones = await _c.K.C.E.Db.ReceiptApplicationPortions.ToListAsync();
        porciones.Where(p => p.ReceiptMeansId == delCheque.Id).Should().OnlyContain(p => p.Status == PortionState.Reversed && p.ReversalKey!.EndsWith(":reverso"));
        porciones.Where(p => p.ReceiptMeansId != delCheque.Id).Should().OnlyContain(p => p.Status == PortionState.Applied);
        _c.Orden.Should().NotBeEmpty();
        _c.Orden.TakeWhile(o => o.StartsWith("obligacion")).Count().Should().BeGreaterThan(0);
        _c.Orden.SkipWhile(o => o.StartsWith("obligacion")).Should().OnlyContain(o => o.StartsWith("cuenta"));
        (await _c.K.C.E.Db.PayerCreditBalances.AnyAsync()).Should().BeFalse();
        (await _c.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == ChequeRecibidoDevueltoV1.Type)).Should().BeTrue();
        await _c.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoChequeDevuelto), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lo_ya_entregado_queda_como_cuenta_por_cobrar_al_pagador()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);
        // La porción del cheque ya la reversó el módulo y el dinero se devolvió: lo que respaldaba ya no está en ninguna porción.
        foreach (var p in _c.K.C.E.Db.ReceiptApplicationPortions) p.Status = PortionState.Reversed;
        await _c.K.C.E.Db.SaveChangesAsync();

        var r = await DevolverAsync(cheque.PublicId);

        r.Value.PayerReceivableNumber.Should().NotBeNull();
        var cxc = await _c.K.C.E.Db.PayerReceivables.SingleAsync();
        cxc.Amount.Should().Be(100_000m);
        cxc.PersonId.Should().Be(_c.K.Asociado.Id);
    }

    [Fact]
    public async Task La_causal_tiene_que_estar_en_el_catalogo_y_no_se_devuelve_dos_veces()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);

        (await DevolverAsync(cheque.PublicId, "99")).Error.Code.Should().Be(ErroresDeRecaudo.CausalDeDevolucionDesconocida("99").Code);
        (await DevolverAsync(cheque.PublicId)).IsSuccess.Should().BeTrue();
        (await DevolverAsync(cheque.PublicId)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequeYaDevuelto);
    }

    [Fact]
    public async Task El_girador_con_devoluciones_aparece_en_la_lista_que_advierte_al_cajero()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);
        await DevolverAsync(cheque.PublicId);

        var lista = await new ListDrawersWithReturnsQueryHandler(_c.K.C.E.Db).Handle(new ListDrawersWithReturnsQuery(_c.K.Asociado.PublicId), default);

        lista.Value.Should().ContainSingle().Which.Returned.Should().Be(1);
    }

    [Fact]
    public async Task Una_causal_que_no_cuenta_no_entra_a_la_lista()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);
        await DevolverAsync(cheque.PublicId, "05");

        var lista = await new ListDrawersWithReturnsQueryHandler(_c.K.C.E.Db).Handle(new ListDrawersWithReturnsQuery(), default);

        lista.Value.Should().BeEmpty();
    }
}
