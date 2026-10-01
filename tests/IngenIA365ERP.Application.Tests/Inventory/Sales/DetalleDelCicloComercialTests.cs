using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T893–T898 (contracts/api.md §18.1, §18.4): lo que las pantallas del ciclo comercial leen del detalle de la venta
/// (<c>GET /sales/documents/{id}</c>) y de su lista. La cotización trae su vigencia y el pedido la de su reserva; cada línea que nace de
/// otra dice de cuál (<c>originLinePublicId</c>, para volver a guardar el borrador sin perder el vínculo); la línea de un pedido dice lo
/// pendiente por despachar o facturar y la de una remisión lo pendiente por facturar; el documento lista todos sus orígenes (una factura
/// desde remisiones tiene varios) y la lista trae la persona del cliente, para facturar juntas sólo remisiones del mismo. (nuevo)
/// </summary>
public class DetalleDelCicloComercialTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    private static async Task<SalesDocumentDto> DetalleAsync(CicloComercialDePrueba c, Guid documento)
    {
        c.Db.ChangeTracker.Clear();
        var r = await new GetSalesDocumentQueryHandler(c.Db, c.V.K.Vista(), c.V.K.Alcance).Handle(new GetSalesDocumentQuery(documento), default);
        return CicloComercialDePrueba.Exito(r);
    }

    [Fact]
    public async Task La_cotizacion_trae_su_vigencia_y_el_pedido_la_de_su_reserva()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var cotizacion = await c.ConfirmadoAsync(c.Documento("COT", lineas: [c.Linea(c.P1, 2m)]) with { ValidUntil = Hoy.AddDays(5) }, RutasDeVenta.Cotizaciones);
        var pedido = await c.PedidoAsync(4m);

        (await DetalleAsync(c, cotizacion.PublicId)).ValidUntil.Should().Be(Hoy.AddDays(5));
        (await DetalleAsync(c, pedido.PublicId)).ValidUntil.Should().Be(c.ReservasDe(pedido).Single().ExpiresOn, "el pedido sella el vencimiento de su reserva");
    }

    [Fact]
    public async Task La_linea_del_pedido_dice_lo_pendiente_y_la_de_la_remision_su_origen_y_lo_pendiente_por_facturar()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var pedido = await c.PedidoAsync(10m);
        var remision = await c.RemisionAsync(4m, pedido);

        var delPedido = await DetalleAsync(c, pedido.PublicId);
        delPedido.Lines.Single().Pending.Should().Be(6m, "de 10 se despacharon 4");

        var deLaRemision = await DetalleAsync(c, remision.PublicId);
        var linea = deLaRemision.Lines.Single();
        linea.OriginLinePublicId.Should().Be(pedido.Lines.Single(l => !l.IsDeleted).PublicId);
        linea.Pending.Should().Be(4m, "nada se ha facturado");
        deLaRemision.Origins.Should().ContainSingle().Which.PublicId.Should().Be(pedido.PublicId);

        var factura = await c.FacturaAsync("FVR", [remision.PublicId]);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));
        (await DetalleAsync(c, remision.PublicId)).Lines.Single().Pending.Should().Be(0m);
    }

    [Fact]
    public async Task La_factura_desde_remisiones_lista_todos_sus_origenes_y_la_nota_debito_su_concepto()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var r1 = await c.RemisionAsync(2m);
        var r2 = await c.RemisionAsync(3m);

        var factura = await c.FacturaAsync("FVR", [r1.PublicId, r2.PublicId]);
        var detalle = await DetalleAsync(c, factura);

        detalle.Origins.Select(o => o.PublicId).Should().BeEquivalentTo([r1.PublicId, r2.PublicId]);
        detalle.Lines.Should().OnlyContain(l => l.OriginLinePublicId != null, "cada línea viene de una remisión");
        detalle.Lines.Should().OnlyContain(l => l.Pending == null, "lo pendiente es de la línea origen, no de la factura");
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));

        var nota = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("NDV", origenes: [factura], lineas: [c.Linea(c.P1, 1m)]) with { CorrectionConceptCode = "1" }, RutasDeVenta.NotasDebito));
        (await DetalleAsync(c, nota.PublicId)).CorrectionConceptCode.Should().Be("1");
    }

    [Fact]
    public async Task La_lista_trae_la_persona_del_cliente()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var remision = await c.RemisionAsync(2m);

        c.Db.ChangeTracker.Clear();
        var lista = await new ListSalesDocumentsQueryHandler(c.Db, c.V.K.Alcance).Handle(new ListSalesDocumentsQuery(Class: DocumentClass.Shipment), default);

        CicloComercialDePrueba.Exito(lista).Items.Should().ContainSingle(d => d.DocumentPublicId == remision.PublicId)
            .Which.CounterpartyPersonPublicId.Should().Be(c.Ana.PublicId);
    }
}
