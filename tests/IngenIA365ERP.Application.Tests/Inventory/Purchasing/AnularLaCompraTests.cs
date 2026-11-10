using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// 2026-11-10 (pedido del dueño): la compra directa nace en un paso y se anula en un paso, desde la factura o desde la recepción;
/// la factura primero y la recepción después, en una transacción; sin pareja uno a uno, se anula documento por documento.
/// </summary>
public class AnularLaCompraTests
{
    private static async Task<(ComprasDePrueba C, DirectPurchaseResultDto Compra)> CompraAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var r = await c.CompraDirectaHandler().Handle(new ConfirmDirectPurchaseCommand(
            c.Recepcion(lineas: [c.Linea(c.P1, 3m, 15_600m, c.Docena)]),
            new DirectPurchaseInvoiceRequest(c.Tipo("FCP"), ComprasDePrueba.Documento("4521"))), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        return (c, r.Value);
    }

    private static VoidDirectPurchaseCommandHandler Handler(ComprasDePrueba c) => new(c.C.Db, c.Anular());

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Desde_la_factura_o_la_recepcion_anula_las_dos(bool desdeLaFactura)
    {
        var (c, compra) = await CompraAsync();
        var desde = desdeLaFactura ? compra.SupplierInvoice.PublicId : compra.Receipt.PublicId;

        var r = await Handler(c).Handle(new VoidDirectPurchaseCommand(desde, "Se digitó con el proveedor equivocado"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);

        c.Documento(compra.SupplierInvoice.PublicId).Status.Should().Be(DocumentStatus.Voided);
        c.Documento(compra.Receipt.PublicId).Status.Should().Be(DocumentStatus.Voided);
        r.Value.InvoiceVoiding.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.ReceiptVoiding.Status.Should().Be(DocumentStatus.Confirmed);
        // La mercancía sale como entró: el kardex de la recepción y el de su anulación se compensan.
        var recepcion = c.Documento(compra.Receipt.PublicId).Id;
        var anulacion = c.Documento(r.Value.ReceiptVoiding.VoidingDocumentPublicId).Id;
        var neto = await c.C.Db.KardexEntries.Where(k => k.DocumentId == recepcion || k.DocumentId == anulacion)
            .SumAsync(k => k.QuantityBase); // la salida se guarda con signo
        neto.Should().Be(0m);
    }

    [Fact]
    public async Task Una_recepcion_sin_factura_se_anula_por_su_lado()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();

        var r = await Handler(c).Handle(new VoidDirectPurchaseCommand(recepcion.PublicId, "Prueba"), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Purchase.NotSinglePurchase");
        c.Documento(recepcion.PublicId).Status.Should().Be(DocumentStatus.Confirmed);
    }

    [Fact]
    public async Task Una_compra_ya_anulada_no_se_anula_dos_veces()
    {
        var (c, compra) = await CompraAsync();
        (await Handler(c).Handle(new VoidDirectPurchaseCommand(compra.Receipt.PublicId, "Primera"), default)).IsSuccess.Should().BeTrue();

        var otra = await Handler(c).Handle(new VoidDirectPurchaseCommand(compra.Receipt.PublicId, "Segunda"), default);

        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be("Inventory.Purchase.NotSinglePurchase");
    }
}
