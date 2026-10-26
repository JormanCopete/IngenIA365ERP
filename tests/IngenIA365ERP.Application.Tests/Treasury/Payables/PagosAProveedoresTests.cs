using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Payables;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Payables;

/// <summary>
/// Feature 015, T2, fase 42 (T0451, T0452; FR-124 a FR-127, FR-166, FR-220; E-03): las facturas de proveedor a crédito que confirma Comercial llegan solas a la bandeja con su
/// vencimiento y su neto, una sola vez; su anulación saca la orden si no salió al banco; las cuentas por pagar contables se convierten en órdenes por todo o por una parte sin
/// pasar de lo pendiente; el calendario agrupa lo abierto por fecha de pago; y el cheque que pasó a cuenta por pagar y se cobra después deja su hecho de cobro tardío
/// (<c>ChequeNoCobradoPagado</c>, decisión pendiente del dueño). Prueba escrita, sin correr.
/// </summary>
public class PagosAProveedoresTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private ConsumeSupplierInvoiceMessageCommandHandler Consumir() => new(_t.E.Db, _t.Entrantes, _t.Numerador, _t.Validador, _t.Parametros, _t.Motor, _t.E.Reloj);

    private ConvertAccountingPayableCommandHandler Convertir() =>
        new(_t.E.Db, _t.Contabilidad, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Validador, _t.Parametros, Auditoria, _t.E.Reloj);

    private FacturaDeProveedorPorPagarV1 Factura(decimal neto = 950_000m) => new()
    {
        InvoicePublicId = Guid.NewGuid(), InvoiceNumber = "FP-12", SupplierDocumentNumber = "FE-998", SupplierPersonPublicId = _t.Beneficiaria.PublicId,
        IssueDate = EscenarioDeRecaudo.Hoy.AddDays(-5), DueDate = EscenarioDeRecaudo.Hoy.AddDays(25), Total = 1_000_000m, WithholdingTotal = 1_000_000m - neto, AmountDue = neto,
    };

    private async Task<ResultadoDeConsumo> RecibirAsync(object contenido, string tipo)
    {
        var id = _t.Mensaje(contenido, tipo, 1, _t.Beneficiaria.PublicId);
        return (await Consumir().Handle(new ConsumeSupplierInvoiceMessageCommand([id]), default)).Value;
    }

    [Fact]
    public async Task La_factura_a_credito_llega_como_orden_con_su_vencimiento_y_su_neto()
    {
        var f = Factura();
        (await RecibirAsync(f, FacturaDeProveedorPorPagarV1.Type)).Should().BeOfType<ResultadoDeConsumo.Processed>();

        var orden = await _t.E.Db.PaymentOrders.SingleAsync();
        (orden.Origin, orden.OrderKind).Should().Be((PaymentOrderOrigin.SupplierInvoice, CuentasPorPagarDeTesoreria.ClaseProveedor));
        (orden.NetAmount, orden.GrossAmount, orden.PayDate).Should().Be((950_000m, 1_000_000m, f.DueDate));
        orden.OriginOrderPublicId.Should().Be(f.InvoicePublicId);
        orden.Status.Should().BeOneOf(PaymentOrderState.Ready, PaymentOrderState.Pending);
    }

    [Fact]
    public async Task La_misma_factura_no_entra_dos_veces()
    {
        var f = Factura();
        await RecibirAsync(f, FacturaDeProveedorPorPagarV1.Type);
        await RecibirAsync(f with { }, FacturaDeProveedorPorPagarV1.Type);
        (await _t.E.Db.PaymentOrders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_proveedor_inexistente_se_rechaza_con_recibo()
    {
        var r = await RecibirAsync(Factura() with { SupplierPersonPublicId = Guid.NewGuid() }, FacturaDeProveedorPorPagarV1.Type);
        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ConsumeSupplierInvoiceMessageCommandHandler.CodigoProveedorInexistente);
        _t.E.Db.TreasuryInboundMessages.Should().ContainSingle(m => m.Outcome == InboundMessageOutcome.Rejected);
    }

    [Fact]
    public async Task La_anulacion_anula_la_orden_que_no_salio_y_rechaza_la_que_ya_salio()
    {
        var f = Factura();
        await RecibirAsync(f, FacturaDeProveedorPorPagarV1.Type);
        await RecibirAsync(new AnulacionDeFacturaDeProveedorV1 { InvoicePublicId = f.InvoicePublicId, InvoiceNumber = f.InvoiceNumber, Reason = "Error del proveedor" },
            AnulacionDeFacturaDeProveedorV1.Type);
        (await _t.E.Db.PaymentOrders.SingleAsync()).Status.Should().Be(PaymentOrderState.Voided);

        var g = Factura();
        await RecibirAsync(g, FacturaDeProveedorPorPagarV1.Type);
        var enviada = await _t.E.Db.PaymentOrders.SingleAsync(o => o.OriginOrderPublicId == g.InvoicePublicId);
        enviada.Status = PaymentOrderState.Sent;
        await _t.E.Db.SaveChangesAsync();
        var r = await RecibirAsync(new AnulacionDeFacturaDeProveedorV1 { InvoicePublicId = g.InvoicePublicId, InvoiceNumber = g.InvoiceNumber }, AnulacionDeFacturaDeProveedorV1.Type);
        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ConsumeSupplierInvoiceMessageCommandHandler.CodigoYaEnviada);
    }

    [Fact]
    public async Task La_cuenta_por_pagar_contable_se_convierte_por_partes_sin_pasar_de_lo_pendiente()
    {
        _t.E.Como(_t.E.Cajera);
        var documento = Guid.NewGuid();
        _t.Contabilidad.CuentasPorPagarAsync(Arg.Any<FiltroDeCuentasPorPagar>(), Arg.Any<CancellationToken>())
            .Returns([new CuentaPorPagarDto(documento, _t.Beneficiaria.PublicId, "CXP-77", EscenarioDeRecaudo.Hoy.AddDays(10), 600_000m)]);

        var primera = await Convertir().Handle(new ConvertAccountingPayableCommand(_t.Beneficiaria.PublicId, "CXP-77", 400_000m) { OperationKey = Guid.NewGuid() }, default);
        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? $"{primera.Error.Code}: {primera.Error.Message}" : null);
        primera.Value.InOrders.Should().Be(400_000m);

        var demas = await Convertir().Handle(new ConvertAccountingPayableCommand(_t.Beneficiaria.PublicId, "CXP-77", 300_000m) { OperationKey = Guid.NewGuid() }, default);
        demas.Error.Code.Should().Be("Treasury.Payable.OverPending");

        var resto = await Convertir().Handle(new ConvertAccountingPayableCommand(_t.Beneficiaria.PublicId, "CXP-77", 200_000m) { OperationKey = Guid.NewGuid() }, default);
        resto.IsSuccess.Should().BeTrue();
        (await _t.E.Db.PaymentOrders.Where(o => o.Origin == PaymentOrderOrigin.AccountingPayable).SumAsync(o => o.NetAmount)).Should().Be(600_000m);
    }

    [Fact]
    public async Task Un_documento_cruce_que_contabilidad_no_tiene_no_se_convierte()
    {
        _t.E.Como(_t.E.Cajera);
        _t.Contabilidad.CuentasPorPagarAsync(Arg.Any<FiltroDeCuentasPorPagar>(), Arg.Any<CancellationToken>()).Returns(new List<CuentaPorPagarDto>());
        var r = await Convertir().Handle(new ConvertAccountingPayableCommand(_t.Beneficiaria.PublicId, "CXP-00", 1m) { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be("Treasury.Payable.NotFound");
    }

    [Fact]
    public async Task El_calendario_agrupa_lo_abierto_por_fecha_de_pago_y_deja_fuera_lo_girado()
    {
        var a = _t.Orden(100m);
        var b = _t.Orden(250m);
        var girada = _t.Orden(999m, PaymentOrderState.Disbursed);
        b.PayDate = EscenarioDeRecaudo.Hoy.AddDays(3);
        await _t.E.Db.SaveChangesAsync();

        var r = await new GetPaymentCalendarQueryHandler(_t.E.Db, _t.E.Reloj).Handle(new GetPaymentCalendarQuery(), default);

        r.Value.Should().HaveCount(2);
        r.Value[0].Should().Match<DiaDelCalendarioDto>(d => d.Date == EscenarioDeRecaudo.Hoy && d.Total == 100m && d.OrderNumbers.Contains(a.Number));
        r.Value[1].Total.Should().Be(250m);
        r.Value.SelectMany(d => d.OrderNumbers).Should().NotContain(girada.Number);
    }

    [Fact]
    public async Task El_cheque_que_paso_a_cuenta_por_pagar_y_se_cobra_despues_deja_su_hecho_de_cobro_tardio()
    {
        _t.E.Como(_t.E.Cajera);
        var chequera = new Checkbook { BankAccountId = _t.Pagos.Id, FromNumber = 1, ToNumber = 50, NextCheckNumber = 5, CustodianUserId = _t.E.Cajera.Id, RegisteredByUserId = _t.E.Cajera.Id };
        _t.E.Db.Checkbooks.Add(chequera);
        await _t.E.Db.SaveChangesAsync();
        var orden = _t.Orden(700_000m, PaymentOrderState.Disbursed, PaymentForm.Check);
        var cheque = new IssuedCheck
        {
            CheckbookId = chequera.Id, CheckNumber = 3, PaymentOrderId = orden.Id, BeneficiaryPersonId = _t.Beneficiaria.Id, Amount = 700_000m,
            IssuedOn = EscenarioDeRecaudo.Hoy.AddDays(-300), DeliveredOn = EscenarioDeRecaudo.Hoy.AddDays(-290), Status = IssuedCheckState.Delivered,
            MovedToPayableRunId = 1, MovedToPayableOn = EscenarioDeRecaudo.Hoy.AddDays(-60),
        };
        _t.E.Db.IssuedChecks.Add(cheque);
        await _t.E.Db.SaveChangesAsync();

        var r = await new MarkIssuedCheckCashedCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, Auditoria, _t.E.Reloj, _t.Emision)
            .Handle(new MarkIssuedCheckCashedCommand(cheque.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var hecho = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ChequeNoCobradoPagadoV1.Type);
        hecho.Should().NotBeNull();
    }
}
