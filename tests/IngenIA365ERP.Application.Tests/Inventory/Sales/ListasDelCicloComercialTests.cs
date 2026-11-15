using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-12: las listas del ciclo comercial —cotizaciones, remisiones— filtran por un estado derivado (la cotización «Vencida», la remisión
/// pendiente, parcial o facturada, calculados de la vigencia y de los vínculos, sin columnas nuevas) y paginan en el servidor con su total.
/// </summary>
public class ListasDelCicloComercialTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    private static ListSalesDocumentsQueryHandler Lista(CicloComercialDePrueba c) => new(c.Db, c.V.K.Alcance, c.V.Compras.C.Reloj);

    [Theory]
    [InlineData(new[] { 5.0, 3.0 }, new[] { 5.0, 3.0 }, "Pending")]
    [InlineData(new[] { 5.0, 3.0 }, new[] { 0.0, 0.0 }, "Invoiced")]
    [InlineData(new[] { 5.0, 3.0 }, new[] { 2.0, 3.0 }, "Partial")]
    [InlineData(new[] { 5.0 }, new[] { -1.0 }, "Invoiced")]
    public void El_estado_de_facturacion_sale_de_lo_pendiente_de_cada_linea(double[] cantidades, double[] pendientes, string estado) =>
        EstadoDeFacturacion.De(cantidades.Zip(pendientes, (c, p) => ((decimal)c, (decimal)p)).ToList()).Should().Be(estado);

    [Theory]
    [InlineData("Pending", "Pending", true)]
    [InlineData("Partial", "Open", true)]
    [InlineData("Pending", "Open", true)]
    [InlineData("Invoiced", "Open", false)]
    [InlineData("Invoiced", "invoiced", true)]
    public void El_filtro_por_facturar_reune_las_pendientes_y_las_parciales(string estado, string filtro, bool cumple) =>
        EstadoDeFacturacion.Cumple(estado, filtro).Should().Be(cumple);

    [Fact]
    public async Task Las_remisiones_se_filtran_por_su_estado_de_facturacion_y_paginan()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var facturada = await c.RemisionAsync(5m);
        var parcial = await c.RemisionAsync(4m);
        var pendiente = await c.RemisionAsync(3m);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(await c.FacturaAsync("FVR", [facturada.PublicId])));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(await c.FacturaAsync("FVR", [parcial.PublicId],
            c.Linea(c.P1, 1m) with { OriginLinePublicId = parcial.Lines.Single(l => !l.IsDeleted).PublicId })));

        async Task<List<Guid>> Ids(string filtro) =>
            (await Lista(c).Handle(new ListSalesDocumentsQuery(DocumentClass.Shipment, Invoicing: filtro), default)).Value.Items.Select(i => i.DocumentPublicId).ToList();

        (await Ids("Pending")).Should().Equal(pendiente.PublicId);
        (await Ids("Partial")).Should().Equal(parcial.PublicId);
        (await Ids("Invoiced")).Should().Equal(facturada.PublicId);
        (await Ids("Open")).Should().BeEquivalentTo([pendiente.PublicId, parcial.PublicId]);

        var todas = await Lista(c).Handle(new ListSalesDocumentsQuery(DocumentClass.Shipment, PageSize: 2), default);
        (todas.Value.TotalCount, todas.Value.Items.Count).Should().Be((3L, 2));
        todas.Value.Items.Select(i => i.InvoicingState).Should().OnlyContain(e => e != null, "cada remisión confirmada dice su estado");
        var segunda = await Lista(c).Handle(new ListSalesDocumentsQuery(DocumentClass.Shipment, Page: 2, PageSize: 2, Invoicing: "Open"), default);
        (segunda.Value.TotalCount, segunda.Value.Items.Count).Should().Be((2L, 0));
    }

    [Fact]
    public async Task Las_cotizaciones_confirmadas_se_filtran_por_vigentes_o_vencidas_a_hoy()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        async Task<Guid> Cotizacion(DateOnly vence)
        {
            var b = CicloComercialDePrueba.Exito(await c.GuardarAsync(
                c.Documento("COT", lineas: [c.Linea(c.P1, 1m)]) with { ValidUntil = vence }, RutasDeVenta.Cotizaciones));
            CicloComercialDePrueba.Exito(await c.ConfirmarAsync(b.PublicId));
            return b.PublicId;
        }
        var corta = await Cotizacion(Hoy.AddDays(1));
        var larga = await Cotizacion(Hoy.AddDays(10));
        c.V.Compras.C.Reloj.HoyLocal.Returns(Hoy.AddDays(2));

        var vencidas = await Lista(c).Handle(new ListSalesDocumentsQuery(DocumentClass.SalesQuote, Validity: VigenciaDeCotizacion.Vencida), default);
        var vigentes = await Lista(c).Handle(new ListSalesDocumentsQuery(DocumentClass.SalesQuote, Validity: VigenciaDeCotizacion.Vigente), default);

        vencidas.Value.Items.Select(i => (i.DocumentPublicId, i.ValidUntil)).Should().Equal((corta, (DateOnly?)Hoy.AddDays(1)));
        vigentes.Value.Items.Select(i => i.DocumentPublicId).Should().Equal(larga);
    }
}
