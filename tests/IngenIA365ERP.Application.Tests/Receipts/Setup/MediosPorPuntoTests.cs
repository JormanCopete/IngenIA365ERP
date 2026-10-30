using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// Feature 015, T0066 (US1; FR-023, FR-024; data-model §1.5): la clase de crédito no se ofrece (<c>CreditClassNotAllowed</c>, US1-1); la tarjeta sin
/// adquirente o datáfono tampoco (<c>CardSetupIncomplete</c>, US1-2); el cheque sólo con <c>Caja.AdmiteCheque</c> (<c>CheckDisabled</c>); en R0, fuera de
/// efectivo, transferencia, consignación y cheque, <c>NotInDelivery</c>; un medio inactivo no se ofrece ni aparece; retirar un medio cierra su vigencia y no
/// borra la fila.
/// </summary>
public class MediosPorPuntoTests
{
    private readonly EscenarioDeRecaudo _e = new();

    private async Task<string?> OfrecerAsync(PaymentMeans medio, EntregaDeRecaudo entrega = EntregaDeRecaudo.R0)
    {
        var punto = await _e.Db.CollectionPoints.FirstOrDefaultAsync() ?? _e.Punto("NORTE");
        var r = await new OfferPaymentMeansCommandHandler(_e.Db, _e.Alcance, _e.Parametros, _e.Reloj) { Entrega = entrega }
            .Handle(new OfferPaymentMeansCommand(punto.PublicId, medio.PublicId), CancellationToken.None);
        return r.IsSuccess ? null : r.Error.Code;
    }

    [Fact]
    public async Task Efectivo_y_transferencia_se_ofrecen_en_R0()
    {
        (await OfrecerAsync(_e.Medio("EF", PaymentMeansClass.Cash))).Should().BeNull();
        (await OfrecerAsync(_e.Medio("TR", PaymentMeansClass.Transfer))).Should().BeNull();
        (await OfrecerAsync(_e.Medio("CO", PaymentMeansClass.BankDeposit))).Should().BeNull();
    }

    [Fact]
    public async Task Una_clase_de_credito_no_es_un_recaudo()
    {
        (await OfrecerAsync(_e.Medio("CA", PaymentMeansClass.AssociateCredit))).Should().Be(ErroresDeRecaudo.CodigoMedioDeCredito);
        (await OfrecerAsync(_e.Medio("CC", PaymentMeansClass.CustomerCredit))).Should().Be(ErroresDeRecaudo.CodigoMedioDeCredito);
    }

    [Fact]
    public async Task La_tarjeta_sin_adquirente_ni_datafono_se_rechaza_y_completa_queda_fuera_de_R0()
    {
        var sinAdquirente = _e.Medio("TD", PaymentMeansClass.DebitCard);
        (await OfrecerAsync(sinAdquirente)).Should().Be(ErroresDeRecaudo.CodigoTarjetaIncompleta);

        var adquirente = new CardAcquirer { Code = "RB", Name = "Redeban" };
        _e.Db.CardAcquirers.Add(adquirente);
        await _e.Db.SaveChangesAsync();
        var sinDatafono = _e.Medio("TC", PaymentMeansClass.CreditCard);
        sinDatafono.CardAcquirerId = adquirente.Id;
        await _e.Db.SaveChangesAsync();
        (await OfrecerAsync(sinDatafono)).Should().Be(ErroresDeRecaudo.CodigoTarjetaIncompleta);

        _e.Db.CardTerminals.Add(new CardTerminal { CardAcquirerId = adquirente.Id, Code = "D1" });
        await _e.Db.SaveChangesAsync();
        (await OfrecerAsync(sinDatafono)).Should().Be(ErroresDeRecaudo.CodigoMedioFueraDeLaEntrega, "en R0 la caja no recibe tarjetas");
        (await OfrecerAsync(sinDatafono, EntregaDeRecaudo.R1)).Should().BeNull("desde R1 sí");
    }

    [Fact]
    public async Task El_cheque_sigue_al_parametro()
    {
        var cheque = _e.Medio("CH", PaymentMeansClass.Check);
        (await OfrecerAsync(cheque)).Should().Be(ErroresDeRecaudo.CodigoChequeApagado, "Caja.AdmiteCheque arranca apagado (D37)");

        _e.Parametro(ParametrosDeRecaudoYTesoreria.CajaAdmiteCheque, "true");
        (await OfrecerAsync(cheque)).Should().BeNull();
    }

    [Fact]
    public async Task Un_medio_inactivo_no_se_ofrece_ni_aparece()
    {
        var bono = _e.Medio("EF2", PaymentMeansClass.Cash, activo: false);
        (await OfrecerAsync(bono)).Should().Be(ErroresDeRecaudo.CodigoMedioInactivo);

        var efectivo = _e.Medio("EF", PaymentMeansClass.Cash);
        var punto = _e.Punto("SUR");
        _e.Ofrecer(punto, efectivo);
        efectivo.IsActive = false;
        await _e.Db.SaveChangesAsync();
        var hoy = await new ListPointPaymentMeansQueryHandler(_e.Db, _e.Alcance)
            .Handle(new ListPointPaymentMeansQuery(punto.PublicId, EscenarioDeRecaudo.Hoy), CancellationToken.None);
        hoy.Value.Should().BeEmpty("un medio inactivo no se ofrece aunque siga vinculado");
    }

    [Fact]
    public async Task Retirar_cierra_la_vigencia_y_no_borra()
    {
        var efectivo = _e.Medio("EF", PaymentMeansClass.Cash);
        var punto = _e.Punto("NORTE");
        _e.Ofrecer(punto, efectivo);
        (await OfrecerAsync(efectivo)).Should().Be(ErroresDeRecaudo.CodigoMedioYaOfrecido);

        var r = await new RetirePaymentMeansCommandHandler(_e.Db, _e.Alcance, _e.Reloj)
            .Handle(new RetirePaymentMeansCommand(punto.PublicId, efectivo.PublicId), CancellationToken.None);

        r.Value.Should().ContainSingle(m => m.ValidTo == EscenarioDeRecaudo.Hoy);
        (await _e.Db.PaymentMeansCollectionPoints.CountAsync()).Should().Be(1, "la fila no se borra (un medio con pagos no se borra, FR-024)");
    }
}
