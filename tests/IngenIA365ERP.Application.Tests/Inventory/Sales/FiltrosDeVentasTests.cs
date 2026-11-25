using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-24 (pedido del dueño, filtros de Ventas): la lista de documentos de venta y el detalle de ventas por medio de pago filtran por el
/// cliente (documento o nombre, todos los términos en cualquier orden y sin tildes), el vendedor, el medio de pago, la clase de documento y el
/// total; el detalle dice el vendedor de cada venta.
/// </summary>
public class FiltrosDeVentasTests
{
    private static async Task<InventoryDocument> VentaAsync(VentasDePrueba v, Person? cliente, Guid? vendedor,
        Func<decimal, IReadOnlyList<DocumentPaymentInput>>? pagos, params SalesLineInput[] lineas)
    {
        var borrador = await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, lineas: lineas));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : string.Empty);
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        var conPago = await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, pagos: pagos?.Invoke(total) ?? [v.Pago(v.Efectivo, total)], lineas: lineas),
            borrador.Value.PublicId);
        conPago.IsSuccess.Should().BeTrue(conPago.IsFailure ? $"{conPago.Error.Code}: {conPago.Error.Message}" : string.Empty);
        var r = await v.ConfirmarAsync(borrador.Value.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return v.Documento(borrador.Value.PublicId);
    }

    /// <summary>Una venta grande a Ana con Luz de vendedora (efectivo y bono) y una pequeña sin cliente ni vendedor (efectivo).</summary>
    private static async Task<(VentasDePrueba V, InventoryDocument AAna, InventoryDocument SinCliente)> VentasAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        var aAna = await VentaAsync(v, v.Ana, v.Luz.PublicId, total => [v.Pago(v.Efectivo, 5000m), v.Pago(v.Bono, total - 5000m, "BN-9")],
            v.Linea(v.P1, 2m), v.Linea(v.P3, 1m));
        var sinCliente = await VentaAsync(v, null, null, null, v.Linea(v.P1, 1m));
        aAna.Total.Should().BeGreaterThan(sinCliente.Total);
        return (v, aAna, sinCliente);
    }

    private static async Task<List<Guid>> ListaAsync(VentasDePrueba v, ListSalesDocumentsQuery consulta)
    {
        var r = await new ListSalesDocumentsQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj).Handle(consulta, default);
        r.IsSuccess.Should().BeTrue();
        return [.. r.Value.Items.Select(i => i.DocumentPublicId)];
    }

    [Theory]
    [InlineData("perez ana")]
    [InlineData("ANA PÉREZ")]
    [InlineData("31999")]
    public async Task La_lista_busca_el_cliente_por_documento_o_nombre_en_cualquier_orden(string cliente)
    {
        var (v, aAna, _) = await VentasAsync();

        (await ListaAsync(v, new ListSalesDocumentsQuery(Customer: cliente))).Should().Equal(aAna.PublicId);
    }

    [Fact]
    public async Task La_lista_filtra_por_medio_de_pago_y_por_el_total()
    {
        var (v, aAna, sinCliente) = await VentasAsync();

        (await ListaAsync(v, new ListSalesDocumentsQuery(PaymentMeans: v.Bono.PublicId))).Should().Equal(aAna.PublicId);
        (await ListaAsync(v, new ListSalesDocumentsQuery(PaymentMeans: v.Efectivo.PublicId))).Should().BeEquivalentTo([aAna.PublicId, sinCliente.PublicId]);
        (await ListaAsync(v, new ListSalesDocumentsQuery(PaymentMeans: Guid.NewGuid()))).Should().BeEmpty("un medio que no existe no trae nada");
        (await ListaAsync(v, new ListSalesDocumentsQuery(MinTotal: aAna.Total))).Should().Equal(aAna.PublicId);
        (await ListaAsync(v, new ListSalesDocumentsQuery(MaxTotal: sinCliente.Total))).Should().Equal(sinCliente.PublicId);
        (await ListaAsync(v, new ListSalesDocumentsQuery(MinTotal: sinCliente.Total, MaxTotal: aAna.Total))).Should().HaveCount(2);
    }

    private static async Task<TablaExportable> DetalleAsync(VentasDePrueba v, string? cliente = null, Guid? vendedor = null,
        IReadOnlyList<DocumentClass>? clases = null)
    {
        var r = await new SalesPaymentsDetailReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new SalesPaymentsDetailReportQuery(new FiltrosDeInformeDeInventario(), Customer: cliente, Salesperson: vendedor, DocumentClasses: clases), default);
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) =>
        fila.Valores[tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i];

    private static string? Documento(TablaExportable tabla, FilaExportable fila) => (string?)fila.Valores[^1];

    [Fact]
    public async Task El_detalle_por_medio_de_pago_filtra_por_cliente_vendedor_y_clase_y_dice_el_vendedor()
    {
        var (v, aAna, sinCliente) = await VentasAsync();

        var todas = await DetalleAsync(v);
        todas.Filas.Should().HaveCount(3, "la venta a Ana tiene dos medios y la otra uno");
        todas.Filas.Where(f => Documento(todas, f) == aAna.PublicId.ToString()).Should().OnlyContain(f => (string?)Celda(todas, f, "Vendedor") == "Luz Gómez");
        todas.Filas.Where(f => Documento(todas, f) == sinCliente.PublicId.ToString()).Should().OnlyContain(f => Celda(todas, f, "Vendedor") == null);

        var deAna = await DetalleAsync(v, cliente: "pérez");
        deAna.Filas.Should().HaveCount(2).And.OnlyContain(f => Documento(deAna, f) == aAna.PublicId.ToString());

        var deLuz = await DetalleAsync(v, vendedor: v.Luz.PublicId);
        deLuz.Filas.Should().HaveCount(2).And.OnlyContain(f => Documento(deLuz, f) == aAna.PublicId.ToString());

        (await DetalleAsync(v, clases: [DocumentClass.NonElectronicSalesReceipt])).Filas.Should().HaveCount(3);
        (await DetalleAsync(v, clases: [DocumentClass.SalesInvoice])).Filas.Should().BeEmpty("no hay facturas electrónicas");
        SalesPaymentsDetailReportQueryHandler.Vista.OwnFilters.Should().Contain(["customer", "salesperson", "documentClass", "createdBy"]);
        SalesPaymentsDetailReportQueryHandler.Vista.Filters.Should().Contain("documentType");
    }
}
