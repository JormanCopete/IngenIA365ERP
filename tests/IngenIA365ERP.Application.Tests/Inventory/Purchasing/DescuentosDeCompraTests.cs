using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// 2026-11-13 (descuentos, fase 1): una sola regla para el descuento de una línea en el ciclo común de compras.
/// <list type="bullet">
/// <item>la recepción parcial contra una orden lleva sólo su parte del descuento de la orden (lo deriva el servidor) y un descuento digitado
/// —también un cero— gana;</item>
/// <item><c>DiscountPercent</c> es fracción (0–1), como en ventas y el POS;</item>
/// <item>porcentaje y valor juntos se rechazan (<c>Inventory.Discount.PercentAndAmount</c>) y un descuento mayor que el bruto también
/// (<c>Inventory.Discount.ExceedsLine</c>; antes se recortaba en silencio);</item>
/// <item>los montos van al redondeo de la cooperativa (<c>Redondeo.Montos</c>), no a dos decimales fijos.</item>
/// </list>
/// </summary>
public class DescuentosDeCompraTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static async Task<ComprasDePrueba> ConOrdenesAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.Entrega = EntregaDelComercio.I5;
        return c;
    }

    /// <summary>La orden confirmada de 100 unidades de P1 a 1.000 con 10.000 de descuento (el 10 %) y su línea.</summary>
    private static async Task<Guid> LineaDeOrdenConDescuentoAsync(ComprasDePrueba c)
    {
        var orden = await c.ConfirmadoAsync(new SaveInventoryDraftRequest(c.Tipo("ORC"), null, c.K.Principal.PublicId, null, null, null, null, null, null,
            null, null, null, null, [c.Linea(c.P1, 100m, 1_000m, descuento: 10_000m)],
            SupplierPersonPublicId: c.ProveedorA.PublicId, ExpectedDate: Hoy.AddDays(10), PaymentTerms: "Crédito a 30 días"));
        return (await c.LineasAsync(orden.PublicId)).Single().PublicId;
    }

    private static string Detalle<T>(IngenIA365ERP.Application.Common.Models.Result<T> r) => r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty;

    [Fact]
    public async Task La_recepcion_parcial_contra_la_orden_lleva_solo_su_parte_del_descuento()
    {
        var c = await ConOrdenesAsync();
        var lineaDeOrden = await LineaDeOrdenConDescuentoAsync(c);

        var recepcion = await c.GuardarAsync(c.Recepcion(lineas: [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 60m, OrderLinePublicId: lineaDeOrden)]));

        recepcion.IsSuccess.Should().BeTrue(Detalle(recepcion));
        var linea = (await c.LineasAsync(recepcion.Value.PublicId)).Single();
        (linea.GrossAmount, linea.DiscountAmount, linea.NetAmount).Should().Be((60_000m, 6_000m, 54_000m),
            "60 de 100 llevan el 10 % de la orden, no los 10.000 enteros");
    }

    [Theory]
    [InlineData(1_000, 1_000)]
    [InlineData(0, 0)]
    public async Task Un_descuento_digitado_en_la_recepcion_gana_al_de_la_orden(decimal digitado, decimal esperado)
    {
        var c = await ConOrdenesAsync();
        var lineaDeOrden = await LineaDeOrdenConDescuentoAsync(c);

        var recepcion = await c.GuardarAsync(c.Recepcion(lineas:
            [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 60m, DiscountAmount: digitado, OrderLinePublicId: lineaDeOrden)]));

        recepcion.IsSuccess.Should().BeTrue(Detalle(recepcion));
        (await c.LineasAsync(recepcion.Value.PublicId)).Single().DiscountAmount.Should().Be(esperado, "lo escrito en la recepción —también un cero— gana");
    }

    [Fact]
    public async Task El_porcentaje_va_como_fraccion_de_cero_a_uno()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var conFraccion = c.Recepcion(lineas: [c.Linea(c.P1, 10m, 1_000m) with { DiscountPercent = 0.1m }]);
        var conCien = c.Recepcion(lineas: [c.Linea(c.P1, 10m, 1_000m) with { DiscountPercent = 10m }]);

        var guardada = await c.GuardarAsync(conFraccion);
        var validacion = new SaveInventoryDraftCommandValidator().Validate(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, conCien));

        guardada.IsSuccess.Should().BeTrue(Detalle(guardada));
        (await c.LineasAsync(guardada.Value.PublicId)).Single().DiscountAmount.Should().Be(1_000m, "0,1 es el 10 % de 10.000");
        validacion.IsValid.Should().BeFalse("10 ya no es «10 %»: el porcentaje va como fracción");
        validacion.Errors.Should().Contain(e => e.ErrorMessage.Contains("fracción"));
    }

    [Fact]
    public async Task Porcentaje_y_valor_en_la_misma_linea_se_rechazan()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var r = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 2m, 1_000m), c.Linea(c.P1, 2m, 1_000m, descuento: 100m) with { DiscountPercent = 0.05m }]));

        r.Error.Code.Should().Be(ErroresDePrecios.PercentAndAmountCode);
        r.Error.Message.Should().Contain("línea 2");
        CatalogoDePrueba.Datos(r.Error).Should().BeEquivalentTo(new { lineNumber = 2 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Un_descuento_mayor_que_el_bruto_se_rechaza_en_lugar_de_recortarse()
    {
        var c = await ComprasDePrueba.CrearAsync();

        var r = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 2m, 1_000m, descuento: 2_500m)]));

        r.Error.Code.Should().Be(ErroresDePrecios.ExceedsLineCode);
        r.Error.Message.Should().Contain("línea 1");
    }

    [Theory]
    [InlineData("Centavo", "3001.50", "300.15")]
    [InlineData("Peso", "3002", "300")]
    public async Task Los_montos_de_la_linea_van_al_redondeo_de_la_cooperativa(string redondeo, string brutoEsperado, string descuentoEsperado)
    {
        var bruto = decimal.Parse(brutoEsperado, System.Globalization.CultureInfo.InvariantCulture);
        var descuento = decimal.Parse(descuentoEsperado, System.Globalization.CultureInfo.InvariantCulture);
        var c = await ComprasDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, redondeo);

        var r = await c.GuardarAsync(c.Recepcion(lineas: [c.Linea(c.P1, 3m, 1_000.5m) with { DiscountPercent = 0.1m }]));

        r.IsSuccess.Should().BeTrue(Detalle(r));
        var linea = (await c.LineasAsync(r.Value.PublicId)).Single();
        (linea.GrossAmount, linea.DiscountAmount, linea.NetAmount).Should().Be((bruto, descuento, bruto - descuento));
    }
}
