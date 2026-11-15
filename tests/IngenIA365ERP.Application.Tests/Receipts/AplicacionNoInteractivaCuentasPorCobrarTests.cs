using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts;

/// <summary>
/// Feature 021, C2b (T129): la aplicación no interactiva cobra una factura de cuentas por cobrar comerciales como el recibo de caja —con su detalle
/// <c>RCD_CommercialReceivableApplications</c> y el saldo consultado— hasta su saldo; lo que sobra queda a favor del pagador. Sin factura o sin saldo, la línea
/// no se aplica.
/// </summary>
public sealed class AplicacionNoInteractivaCuentasPorCobrarTests
{
    private readonly BancosDePrueba _b = new();
    private readonly Guid _documento = Guid.NewGuid();

    public AplicacionNoInteractivaCuentasPorCobrarTests()
    {
        _b.Contabilidad.CuentasPorCobrarComercialesAsync(_b.K.Asociado.PublicId, Arg.Any<CancellationToken>())
            .Returns([new CuentaPorCobrarComercialDto(_documento, "FV-1001", EscenarioDeRecaudo.Hoy.AddDays(-10), 100_000m)]);
    }

    private LineaNoInteractiva Linea(string clave, decimal valor, string? factura = "FV-1001") =>
        new(clave, ReceiptChannel.Bank, _b.K.Asociado.PublicId, EscenarioDeRecaudo.Hoy, valor, _b.K.C.E.OficinaA.Id,
            [new ParteNoInteractiva(ApplicationTarget.CommercialReceivable, null, _b.K.Asociado.PublicId, valor, CrossDocument: factura)], ClaseDeMedioDeRecaudo.Transfer);

    [Fact]
    public async Task Cobra_la_factura_hasta_su_saldo_y_lo_demas_queda_a_favor()
    {
        var r = await _b.Aplicacion.AplicarAsync(Linea("CXC-1", 120_000m), default);

        r.Outcome.Should().Be(ProcessingLineOutcome.Applied, r.Message);
        r.Receipt!.TotalApplied.Should().Be(100_000m);
        r.CreditBalance!.Amount.Should().Be(20_000m);
        var detalle = await _b.K.C.E.Db.CommercialReceivableApplications.SingleAsync();
        detalle.InvoiceNumber.Should().Be("FV-1001");
        detalle.ConsultedBalance.Should().Be(100_000m);
        detalle.Amount.Should().Be(100_000m);
        detalle.AccountingDocumentPublicId.Should().Be(_documento);
        (await _b.K.C.E.Db.ReceiptApplications.SingleAsync()).Target.Should().Be(ApplicationTarget.CommercialReceivable);

        // Lo ya cobrado baja el saldo: una segunda línea sólo encuentra lo que falta.
        (await CuentasPorCobrarDeRecaudo.PendientesAsync(_b.K.C.E.Db, _b.Contabilidad, _b.K.Asociado.PublicId, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_factura_o_sin_saldo_no_se_aplica()
    {
        (await _b.Aplicacion.AplicarAsync(Linea("CXC-2", 10_000m, factura: null), default)).Code.Should().Be(ErroresDeCuentasPorCobrar.CodigoSinFactura);
        (await _b.Aplicacion.AplicarAsync(Linea("CXC-3", 10_000m, factura: "FV-9999"), default)).Code.Should().Be(ErroresDeCuentasPorCobrar.CodigoNoExiste);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);
    }
}
