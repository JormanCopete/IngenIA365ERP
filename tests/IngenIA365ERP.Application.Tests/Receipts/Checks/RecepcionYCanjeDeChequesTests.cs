using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.Checks;

/// <summary>
/// Feature 015, US15, T0213 (FR-010, FR-083, FR-084, FR-086; D21, D37): con <c>Caja.AdmiteCheque</c> apagado el cheque no se recibe; trae banco, número, cuenta, fecha y
/// valor; el de tercero sólo con <c>Caja.AdmiteChequeDeTercero</c>; el plazo de presentación por plaza sólo avisa salvo <c>Cheques.BloquearFueraDePlazo</c> (caso de
/// FR-010); el posfechado no paga y va en custodia si se admite; el mismo cheque no entra dos veces; los estados manuales recibido → consignado → en canje → pagado dejan
/// su evento. Prueba escrita, sin correr.
/// </summary>
public class RecepcionYCanjeDeChequesTests
{
    private readonly ChequesDePrueba _c = new();

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CashOperationDto>> MediosAsync(params MedioDeCajaDto[] medios)
    {
        if (_c.K.Sesion is null) await _c.K.AbrirAsync(); // la sesión ya abierta por ReciboAsync se reusa
        var op = (await _c.K.Crear().Handle(new CreateCashOperationCommand(_c.K.Sesion.PublicId, _c.K.Asociado.PublicId), default)).Value;
        await _c.K.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_c.K.Cuota(_c.K.CreditoA, medios.Sum(m => m.Amount))]), default);
        return await _c.K.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, medios), default);
    }

    [Fact]
    public async Task Con_el_parametro_apagado_el_cheque_no_se_recibe()
    {
        var r = await MediosAsync(_c.Medio(100_000m));

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequeApagado);
    }

    [Fact]
    public async Task El_cheque_trae_sus_datos_y_queda_recibido_con_su_plazo()
    {
        _c.AdmitirCheques();

        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m, diasAtras: 2)]);

        cheque.Status.Should().Be(ReceivedCheckState.Received);
        cheque.PresentationDeadline.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(-2).AddDays(15));
        cheque.ReceiptId.Should().NotBeNull();
        (await _c.K.C.E.Db.ReceiptMeans.SingleAsync()).ReceivedCheckId.Should().Be(cheque.Id);
        var sinDatos = await MediosAsync(_c.Medio(100_000m) with { Check = null });
        sinDatos.Error.Code.Should().Be(ErroresDeRecaudo.ChequeSinDatos().Code);
    }

    [Fact]
    public async Task El_de_tercero_solo_si_la_cooperativa_lo_admite()
    {
        _c.AdmitirCheques(deTercero: false);

        var r = await MediosAsync(_c.Medio(100_000m, girador: "Otra persona S.A."));

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequeDeTercero);
    }

    [Fact]
    public async Task Fuera_de_plazo_solo_avisa_salvo_la_bandera()
    {
        _c.AdmitirCheques();
        var avisa = await MediosAsync(_c.Medio(100_000m, diasAtras: 40, numero: "1"));
        avisa.IsSuccess.Should().BeTrue(avisa.IsFailure ? avisa.Error.Code : null);

        var c2 = new ChequesDePrueba();
        c2.AdmitirCheques();
        c2.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.ChequesBloquearFueraDePlazo, "true");
        await c2.K.AbrirAsync();
        var op = (await c2.K.Crear().Handle(new CreateCashOperationCommand(c2.K.Sesion.PublicId, c2.K.Asociado.PublicId), default)).Value;
        await c2.K.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [c2.K.Cuota(c2.K.CreditoA, 100_000m)]), default);
        var bloquea = await c2.K.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [c2.Medio(100_000m, diasAtras: 40)]), default);
        bloquea.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequeFueraDePlazo);
    }

    [Fact]
    public async Task El_posfechado_no_paga_y_va_en_custodia_si_se_admite()
    {
        _c.AdmitirCheques();
        var r = await MediosAsync(_c.Medio(100_000m, diasAtras: -10));
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequePosfechado);

        var sinCustodia = await _c.Custodia().Handle(new RegisterCheckInCustodyCommand(_c.K.Asociado.PublicId, _c.K.C.E.OficinaA.PublicId, 100_000m,
            _c.Medio(100_000m, diasAtras: -10).Check!) { OperationKey = Guid.NewGuid() }, default);
        sinCustodia.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequePosfechado);

        _c.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaAdmitePosfechadoEnCustodia, "true");
        var enCustodia = await _c.Custodia().Handle(new RegisterCheckInCustodyCommand(_c.K.Asociado.PublicId, _c.K.C.E.OficinaA.PublicId, 100_000m,
            _c.Medio(100_000m, diasAtras: -10).Check!) { OperationKey = Guid.NewGuid() }, default);
        enCustodia.Value.Status.Should().Be(ReceivedCheckState.InCustody);
        enCustodia.Value.ReceiptNumber.Should().BeNull();
    }

    [Fact]
    public async Task El_mismo_cheque_no_entra_dos_veces()
    {
        _c.AdmitirCheques();
        await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);

        var op = (await _c.K.Crear().Handle(new CreateCashOperationCommand(_c.K.Sesion.PublicId, _c.K.Asociado.PublicId), default)).Value;
        await _c.K.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_c.K.Cuota(_c.K.CreditoB, 100_000m)]), default);
        var r = await _c.K.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_c.Medio(100_000m)]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoChequeRepetido);
    }

    [Fact]
    public async Task Los_estados_manuales_avanzan_en_orden_y_dejan_su_evento()
    {
        _c.AdmitirCheques();
        var cheque = await _c.ReciboAsync([_c.K.Cuota(_c.K.CreditoA, 100_000m)], [_c.Medio(100_000m)]);
        var t = _c.Transiciones();

        var pagadoAntes = await t.Handle(new MarkReceivedCheckPaidCommand(cheque.PublicId) { OperationKey = Guid.NewGuid() }, default);
        pagadoAntes.Error.Code.Should().Be(ErroresDeRecaudo.ChequeTransicionInvalida("x", "y").Code);
        (await t.Handle(new MarkReceivedCheckDepositedCommand(cheque.PublicId) { OperationKey = Guid.NewGuid() }, default)).Value.Status.Should().Be(ReceivedCheckState.Deposited);
        (await t.Handle(new MarkReceivedCheckInClearingCommand(cheque.PublicId) { OperationKey = Guid.NewGuid() }, default)).Value.Status.Should().Be(ReceivedCheckState.InClearing);
        (await t.Handle(new MarkReceivedCheckPaidCommand(cheque.PublicId) { OperationKey = Guid.NewGuid() }, default)).Value.Status.Should().Be(ReceivedCheckState.Paid);

        var eventos = await _c.K.C.E.Db.ReceivedCheckEvents.Where(e => e.ReceivedCheckId == cheque.Id).OrderBy(e => e.Id).Select(e => e.ToStatus).ToListAsync();
        eventos.Should().Equal(ReceivedCheckState.Deposited, ReceivedCheckState.InClearing, ReceivedCheckState.Paid);
    }
}
