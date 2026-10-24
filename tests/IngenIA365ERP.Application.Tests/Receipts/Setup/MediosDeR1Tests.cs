using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// Feature 015, R1, T0355 (FR-023, FR-024, FR-031, FR-041; US1 «Independent Test» de R1): desde R1 las clases de tarjeta, bono y otro dejan de responder
/// <c>Receipts.PaymentMeans.NotInDelivery</c>; la tarjeta sin adquirente o datáfono sigue en <c>CardSetupIncomplete</c>; el pago con tarjeta captura el datáfono del
/// adquirente, el lote y la aprobación (<c>CardCaptureIncomplete</c>) y nunca el número de la tarjeta (<c>CardNumberNotAllowed</c>); el bono lleva su número, único
/// (<c>VoucherAlreadyUsed</c>), también dentro de la misma operación; «otro» lleva su referencia. Prueba escrita, sin correr.
/// </summary>
public sealed class MediosDeR1Tests
{
    private readonly CajaConContratos _k = new();

    private async Task<(PaymentMeans Tarjeta, CardTerminal Datafono, CardTerminal Ajeno)> TarjetaOfrecidaAsync()
    {
        var e = _k.C.E;
        var adquirente = new CardAcquirer { Code = "RB", Name = "Redeban" };
        var otro = new CardAcquirer { Code = "CB", Name = "Credibanco" };
        e.Db.CardAcquirers.AddRange(adquirente, otro);
        await e.Db.SaveChangesAsync();
        var datafono = new CardTerminal { CardAcquirerId = adquirente.Id, Code = "D1" };
        var ajeno = new CardTerminal { CardAcquirerId = otro.Id, Code = "D9" };
        e.Db.CardTerminals.AddRange(datafono, ajeno);
        var tarjeta = e.Medio("TD", PaymentMeansClass.DebitCard, 0, CashCountMethod.VoucherTotal);
        tarjeta.CardAcquirerId = adquirente.Id;
        await e.Db.SaveChangesAsync();
        var r = await new OfferPaymentMeansCommandHandler(e.Db, e.Alcance, e.Parametros, e.Reloj)
            .Handle(new OfferPaymentMeansCommand(_k.C.Punto.PublicId, tarjeta.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return (tarjeta, datafono, ajeno);
    }

    private async Task<Guid> OperacionAsync(decimal valor = 100_000m)
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, valor)]), default);
        return op.PublicId;
    }

    [Fact]
    public void La_entrega_desplegada_es_R1_o_posterior_y_admite_las_clases_de_R1()
    {
        // R2 (fase 36): la entrega sube a R2 y las clases de R1 se siguen ofreciendo.
        ((int)CatalogoDeRecaudo.EntregaVigente).Should().BeGreaterThanOrEqualTo((int)EntregaDeRecaudo.R1);
        foreach (var clase in new[] { PaymentMeansClass.CreditCard, PaymentMeansClass.DebitCard, PaymentMeansClass.Voucher, PaymentMeansClass.Other })
            MediosDeRecaudo.EnLaEntrega(clase, CatalogoDeRecaudo.EntregaVigente).Should().BeTrue(clase.ToString());
    }

    [Fact]
    public async Task El_bono_y_otro_se_ofrecen_y_la_tarjeta_sin_datafono_sigue_incompleta()
    {
        var e = _k.C.E;
        foreach (var (codigo, clase) in new[] { ("BONO", PaymentMeansClass.Voucher), ("OTRO", PaymentMeansClass.Other) })
        {
            var r = await new OfferPaymentMeansCommandHandler(e.Db, e.Alcance, e.Parametros, e.Reloj)
                .Handle(new OfferPaymentMeansCommand(_k.C.Punto.PublicId, e.Medio(codigo, clase).PublicId), CancellationToken.None);
            r.IsSuccess.Should().BeTrue(codigo);
        }
        var sinAdquirente = await new OfferPaymentMeansCommandHandler(e.Db, e.Alcance, e.Parametros, e.Reloj)
            .Handle(new OfferPaymentMeansCommand(_k.C.Punto.PublicId, e.Medio("TC", PaymentMeansClass.CreditCard).PublicId), CancellationToken.None);
        sinAdquirente.Error.Code.Should().Be(ErroresDeRecaudo.CodigoTarjetaIncompleta);
    }

    [Fact]
    public async Task La_tarjeta_captura_datafono_del_adquirente_lote_y_aprobacion_sin_el_numero()
    {
        var (tarjeta, datafono, ajeno) = await TarjetaOfrecidaAsync();
        var op = await OperacionAsync();
        MedioDeCajaDto Pago() => new(tarjeta.PublicId, 100_000m);

        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [Pago()]), default)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoCapturaDeTarjetaIncompleta, "sin datáfono");
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [Pago() with { CardTerminalPublicId = datafono.PublicId, ApprovalCode = "123456" }]), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoCapturaDeTarjetaIncompleta, "sin lote");
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op,
                [Pago() with { CardTerminalPublicId = ajeno.PublicId, TerminalBatch = "001", ApprovalCode = "123456" }]), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoCapturaDeTarjetaIncompleta, "el datáfono es de otro adquirente");
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op,
                [Pago() with { CardTerminalPublicId = datafono.PublicId, TerminalBatch = "001", ApprovalCode = "123456", Reference = "4111 1111 1111 1111" }]), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoNumeroDeTarjeta, "el número de la tarjeta nunca se guarda");

        var bien = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op,
            [Pago() with { CardTerminalPublicId = datafono.PublicId, TerminalBatch = "001", ApprovalCode = "123456" }]), default);
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? bien.Error.Message : null);

        var recibo = await _k.Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);
        recibo.IsSuccess.Should().BeTrue(recibo.IsFailure ? recibo.Error.Message : null);
        var medio = await _k.C.E.Db.ReceiptMeans.AsNoTracking().SingleAsync();
        (medio.MeansClass, medio.CardTerminalId, medio.TerminalBatch, medio.ApprovalCode).Should().Be((PaymentMeansClass.DebitCard, datafono.Id, "001", "123456"));
    }

    [Fact]
    public async Task El_bono_lleva_numero_unico_tambien_dentro_de_la_operacion()
    {
        var e = _k.C.E;
        var bono = e.Medio("BONO", PaymentMeansClass.Voucher, 0);
        e.Ofrecer(_k.C.Punto, bono);
        var op = await OperacionAsync();

        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [new MedioDeCajaDto(bono.PublicId, 100_000m)]), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoReferenciaRequerida, "el bono sin número");
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op,
                [new MedioDeCajaDto(bono.PublicId, 50_000m, VoucherNumber: "B-1"), new MedioDeCajaDto(bono.PublicId, 50_000m, VoucherNumber: "B-1")]), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoBonoUsado, "el mismo bono dos veces en la operación");
    }
}
