using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, R1, T0395 (US23; FR-049): por HTTP, la caja consulta las cuentas por cobrar comerciales de la persona por la consulta de Contabilidad y rechaza cobrar
/// una factura sin saldo (nunca como concepto vario). El recorrido completo —vender a crédito un servicio en Comercial y cobrarlo con una cuota— se compone en la corrida de
/// cierre de R1 con el escenario de ventas de la 012 (<c>InventarioE2E</c>). Escrita, sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class CuentasPorCobrarComercialesTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Sin_saldo_no_hay_que_cobrar_y_la_factura_inexistente_se_rechaza()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "cxc");

        var pendientes = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/commercial-receivables?person={c.Persona}");
        pendientes.GetArrayLength().Should().Be(0, "la persona no tiene ventas a crédito sin Cartera");

        var op = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = c.Persona }));
        var items = await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{op.GetProperty("publicId").GetGuid()}/items",
            new { items = new[] { new { target = "CommercialReceivable", amount = 10_000m, crossDocument = "FV-NO-EXISTE" } } });
        items.StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await items.Content.ReadAsStringAsync()).Should().Contain("Receipts.CommercialReceivable.NotFound");
    }
}
