using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Tareas;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Checkbooks;

/// <summary>
/// Feature 015, US19, T0222 (R-19; FR-113, FR-122, FR-134 a FR-136, FR-219): la chequera con rango y custodio; la numeración desde el siguiente número bajo el cerrojo de la
/// chequera; agotada pide la siguiente; el cheque en blanco dañado explica su hueco; <c>ChequeGiradoEmitido</c> nace al emitir; la entrega con constancia registra «girada»;
/// anulado antes de entregar emite <c>ChequeGiradoAnulado</c> sin resultado; no cobrado a los días del parámetro alerta y, anulado, informa «cheque no cobrado»; cruzado y
/// para el primer beneficiario por defecto. Prueba escrita, sin correr.
/// </summary>
public class ChequerasYChequesGiradosTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private async Task<CheckbookDto> ChequeraAsync(long desde = 100, long hasta = 102)
    {
        var r = await new RegisterCheckbookCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, Auditoria, _t.E.Reloj)
            .Handle(new RegisterCheckbookCommand(_t.Pagos.PublicId, desde, hasta) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return r.Value;
    }

    /// <summary>Un lote aprobado de cheques con las órdenes dadas.</summary>
    private PaymentBatch LoteDeCheques(params PaymentOrder[] ordenes)
    {
        var lote = new PaymentBatch
        {
            Number = $"LP-{_t.E.Db.PaymentBatches.Count() + 1:0000}", BankAccountId = _t.Pagos.Id, Form = PaymentBatchForm.Checks, PreparedByUserId = _t.E.Cajera.Id,
            PayDate = EscenarioDeRecaudo.Hoy, Status = PaymentBatchState.Approved, OrderCount = ordenes.Length, Total = ordenes.Sum(o => o.NetAmount), InputsHash = "x",
        };
        _t.E.Db.PaymentBatches.Add(lote);
        _t.E.Db.SaveChanges();
        foreach (var o in ordenes)
        {
            o.Status = PaymentOrderState.Approved;
            _t.E.Db.PaymentBatchItems.Add(new PaymentBatchItem { PaymentBatchId = lote.Id, PaymentOrderId = o.Id, Amount = o.NetAmount });
        }
        _t.E.Db.SaveChanges();
        return lote;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<IReadOnlyList<IssuedCheckDto>>> EmitirAsync(PaymentBatch lote, CheckbookDto chequera) =>
        new IssueChecksForBatchCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Emision, Auditoria, _t.E.Reloj)
            .Handle(new IssueChecksForBatchCommand(lote.PublicId, chequera.PublicId) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task La_chequera_numera_desde_su_siguiente_numero_y_un_rango_cruzado_no_se_registra()
    {
        var chequera = await ChequeraAsync();

        chequera.NextNumber.Should().Be(100);
        chequera.Remaining.Should().Be(3);
        var cruzada = await new RegisterCheckbookCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, Auditoria, _t.E.Reloj)
            .Handle(new RegisterCheckbookCommand(_t.Pagos.PublicId, 101, 150) { OperationKey = Guid.NewGuid() }, default);
        cruzada.Error.Code.Should().Be(ErroresDeTesoreria.CodigoChequeraSeCruza);
    }

    [Fact]
    public async Task Emitir_toma_el_numero_siguiente_nace_el_hecho_y_por_defecto_va_cruzado_al_primer_beneficiario()
    {
        var chequera = await ChequeraAsync();
        var orden = _t.Orden(500_000m, forma: PaymentForm.Check);
        var lote = LoteDeCheques(orden);

        var r = await EmitirAsync(lote, chequera);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        var cheque = r.Value.Single();
        cheque.CheckNumber.Should().Be(100);
        cheque.Crossed.Should().BeTrue();
        cheque.FirstBeneficiaryOnly.Should().BeTrue();
        cheque.Status.Should().Be(IssuedCheckState.Issued);
        (await _t.E.Db.PaymentOrders.SingleAsync(o => o.Id == orden.Id)).Status.Should().Be(PaymentOrderState.Sent);
        (await _t.E.Db.PaymentBatches.SingleAsync(b => b.Id == lote.Id)).Status.Should().Be(PaymentBatchState.Sent);
        (await _t.E.Db.Checkbooks.SingleAsync()).NextCheckNumber.Should().Be(101);
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == ChequeGiradoEmitidoV1.Type)).Should().BeTrue();
        await _t.Cerrojo.Received().BloquearAsync(NivelDeTesoreria.Chequera, Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agotada_pide_la_siguiente_y_el_dañado_explica_su_hueco()
    {
        var chequera = await ChequeraAsync(1, 2);
        var danado = await new SpoilBlankCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, Auditoria, _t.E.Reloj)
            .Handle(new SpoilBlankCheckCommand(chequera.PublicId, "Se manchó") { OperationKey = Guid.NewGuid() }, default);
        danado.Value.Status.Should().Be(IssuedCheckState.SpoiledBlank);
        danado.Value.CheckNumber.Should().Be(1);

        var lote = LoteDeCheques(_t.Orden(forma: PaymentForm.Check), _t.Orden(forma: PaymentForm.Check));
        var r = await EmitirAsync(lote, chequera);

        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoChequeraAgotada);
    }

    [Fact]
    public async Task La_entrega_con_constancia_registra_girada()
    {
        var chequera = await ChequeraAsync();
        var orden = _t.Orden(forma: PaymentForm.Check);
        var cheque = (await EmitirAsync(LoteDeCheques(orden), chequera)).Value.Single();

        var r = await new DeliverIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, Auditoria, _t.E.Reloj)
            .Handle(new DeliverIssuedCheckCommand(cheque.PublicId, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(IssuedCheckState.Delivered);
        (await _t.E.Db.PaymentOrders.SingleAsync(o => o.Id == orden.Id)).Status.Should().Be(PaymentOrderState.Disbursed);
        (await _t.E.Db.DisbursementResults.SingleAsync()).CheckNumber.Should().Be("100");
    }

    [Fact]
    public async Task Anulado_antes_de_entregar_emite_el_espejo_sin_resultado_y_la_orden_recibe_otro_cheque()
    {
        var chequera = await ChequeraAsync();
        var orden = _t.Orden(forma: PaymentForm.Check);
        var lote = LoteDeCheques(orden);
        var cheque = (await EmitirAsync(lote, chequera)).Value.Single();

        var r = await new VoidIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, _t.Emision, _t.Parametros, Auditoria, _t.E.Reloj)
            .Handle(new VoidIssuedCheckCommand(cheque.PublicId, "Error en el beneficiario") { OperationKey = Guid.NewGuid() }, default);

        r.Value.Status.Should().Be(IssuedCheckState.Voided);
        (await _t.E.Db.DisbursementResults.AnyAsync()).Should().BeFalse();
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == ChequeGiradoAnuladoV1.Type)).Should().BeTrue();
        var otro = await EmitirAsync(lote, chequera);
        otro.Value.Single().CheckNumber.Should().Be(101);
    }

    [Fact]
    public async Task No_cobrado_alerta_a_los_dias_y_anulado_informa_cheque_no_cobrado()
    {
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.ChequesDiasSinCobrar, "30", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        var chequera = await ChequeraAsync();
        var orden = _t.Orden(forma: PaymentForm.Check);
        var cheque = (await EmitirAsync(LoteDeCheques(orden), chequera)).Value.Single();
        await new DeliverIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, Auditoria, _t.E.Reloj)
            .Handle(new DeliverIssuedCheckCommand(cheque.PublicId, Guid.NewGuid(), EscenarioDeRecaudo.Hoy.AddDays(-45)) { OperationKey = Guid.NewGuid() }, default);
        var anular = new VoidIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, _t.Emision, _t.Parametros, Auditoria, _t.E.Reloj);

        var alertados = await new VigilanciaDeTesoreria(_t.E.Db, _t.Parametros, Substitute.For<ICalendarioHabil>(), _t.E.Alertas, _t.E.Reloj).ChequesSinCobrarAsync(default);
        alertados.Should().Be(1);
        await _t.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaChequeSinCobrar), Arg.Any<CancellationToken>());

        var r = await anular.Handle(new VoidIssuedCheckCommand(cheque.PublicId, "No lo cobró") { OperationKey = Guid.NewGuid() }, default);
        r.Value.Status.Should().Be(IssuedCheckState.NotCashed);
        (await _t.E.Db.PaymentOrders.SingleAsync(o => o.Id == orden.Id)).Status.Should().Be(PaymentOrderState.CheckNotCashed);
        (await _t.E.Db.DisbursementResults.CountAsync(x => x.Outcome == DisbursementOutcome.CheckNotCashed)).Should().Be(1);
    }

    [Fact]
    public async Task Un_entregado_reciente_todavia_no_se_anula_como_no_cobrado()
    {
        var chequera = await ChequeraAsync();
        var cheque = (await EmitirAsync(LoteDeCheques(_t.Orden(forma: PaymentForm.Check)), chequera)).Value.Single();
        await new DeliverIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, Auditoria, _t.E.Reloj)
            .Handle(new DeliverIssuedCheckCommand(cheque.PublicId, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        var r = await new VoidIssuedCheckCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Resultados, _t.Emision, _t.Parametros, Auditoria, _t.E.Reloj)
            .Handle(new VoidIssuedCheckCommand(cheque.PublicId, "Pronto") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeTesoreria.ChequeTodaviaCobrable(30).Code);
    }

    [Fact]
    public void El_valor_en_letras_dice_pesos_y_centavos()
    {
        ValorEnLetras.Pesos(1_250_000m).Should().Be("un millón doscientos cincuenta mil pesos m/cte");
        ValorEnLetras.Pesos(21.50m).Should().Be("veintiuno pesos con cincuenta centavos");
    }
}
