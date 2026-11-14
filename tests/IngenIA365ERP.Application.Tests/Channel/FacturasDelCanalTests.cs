using FluentAssertions;
using IngenIA365ERP.Application.Channel.Concepts;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, C2b (T128, T130): una factura pendiente de cuentas por cobrar comerciales es un concepto del canal (<c>INVOICE:{comprobante}</c>) y su pago
/// queda aplicado contra ella; lo que sobra queda a favor del asociado y la factura sin saldo no se cobra (el pago aprobado nunca se rechaza).
/// </summary>
public sealed class FacturasDelCanalTests
{
    private readonly BancosDePrueba _b = new();
    private readonly ChannelPaymentDestination _destino;
    private readonly Guid _documento = Guid.NewGuid();

    public FacturasDelCanalTests()
    {
        var e = _b.K.C.E;
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RL", Name = "Recibo en línea", Kind = ReceiptDocumentKind.OnlineReceipt });
        var convenio = new CollectionAgreement
        {
            Code = "AVAL", Name = "Pasarela", Kind = CollectionAgreementKind.Gateway, OwnBankAccountPublicId = _b.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(convenio);
        e.Db.SaveChanges();
        _destino = new ChannelPaymentDestination { Name = "Avalpay", CollectionAgreementId = convenio.Id };
        e.Db.ChannelPaymentDestinations.Add(_destino);
        e.Db.SaveChanges();
        e.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(_b.K.Asociado.PublicId, "canal", "190.0.0.1"));
        _b.Contabilidad.CuentasPorCobrarComercialesAsync(_b.K.Asociado.PublicId, Arg.Any<CancellationToken>())
            .Returns([new CuentaPorCobrarComercialDto(_documento, "FV-2001", EscenarioDeRecaudo.Hoy.AddDays(-5), 80_000m)]);
    }

    private RegisterChannelPaymentCommand Aviso(string referencia, decimal valor) =>
        new(_b.K.Asociado.PublicId, referencia, _destino.PublicId, valor, "COP", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
            new AprobacionDeLaPasarelaDto(1, 2, "123456", "R1", "_PSE_", "PSE", "Banco"), [new ItemDelAvisoDto(ClaveDeConcepto.DeFactura(_documento), "Due", valor, null)],
            null)
        { OperationKey = RegisterChannelPaymentCommand.ClaveDeOperacion(referencia) };

    private RegisterChannelPaymentCommandHandler Handler() => new(_b.K.C.E.Db,
        new AplicacionDelCanal(_b.K.C.E.Db, _b.Aplicacion, _b.K.Obligaciones, _b.K.Cuentas, _b.K.C.E.Alertas, _b.K.C.E.Reloj, _b.Contabilidad));

    [Fact]
    public void La_clave_de_la_factura_se_lee_como_factura()
    {
        var clave = ClaveDeConcepto.Leer(ClaveDeConcepto.DeFactura(_documento))!.Value;
        clave.EsFactura.Should().BeTrue();
        clave.EsCredito.Should().BeFalse();
        clave.EsCuenta.Should().BeFalse();
        clave.Producto.Should().Be(_documento);
    }

    [Fact]
    public async Task El_pago_queda_aplicado_contra_la_factura()
    {
        var r = await Handler().Handle(Aviso("ABC2612010000000F1", 80_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Outcome.Should().Be("Applied");
        var detalle = await _b.K.C.E.Db.CommercialReceivableApplications.SingleAsync();
        detalle.InvoiceNumber.Should().Be("FV-2001");
        detalle.Amount.Should().Be(80_000m);
        (await _b.K.C.E.Db.ChannelPaymentNotices.Include(n => n.Items).SingleAsync()).Items.Single().ConceptKind.Should().Be(ConceptKind.CommercialReceivable);
    }

    [Fact]
    public async Task Lo_que_sobra_queda_a_favor_y_sin_saldo_no_se_cobra()
    {
        var r = await Handler().Handle(Aviso("ABC2612010000000F2", 100_000m), default);

        r.Value.Outcome.Should().Be("InReview");
        (await _b.K.C.E.Db.CommercialReceivableApplications.SingleAsync()).Amount.Should().Be(80_000m);
        (await _b.K.C.E.Db.PayerCreditBalances.SumAsync(s => s.Amount)).Should().Be(20_000m);

        // La factura ya pagada: el siguiente pago no la cobra y no deja recibo (queda en revisión para devolverlo).
        var otro = await Handler().Handle(Aviso("ABC2612010000000F3", 10_000m), default);
        otro.Value.Outcome.Should().Be("InReview");
        otro.Value.ReceiptPublicId.Should().BeNull();
    }

    [Fact]
    public void Las_facturas_se_publican_sin_linea_ni_documentos()
    {
        var v = new SaveChannelConceptCommandValidator();
        Publicar(null, false).Should().BeTrue();
        Publicar("L1", false).Should().BeFalse();
        Publicar(null, true).Should().BeFalse();

        bool Publicar(string? linea, bool extracto) => v.Validate(new SaveChannelConceptCommand(null, ConceptKind.CommercialReceivable, linea, null, true, false, null,
            false, false, true, null, extracto, false, false)).IsValid;
    }
}
