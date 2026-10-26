using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0108 (US3 «Independent Test»; quickstart recorrido A pasos 2–6 y B): una persona con dos créditos paga la cuota de uno y un abono extraordinario
/// al otro con «reducir plazo», con efectivo y transferencia, en un recibo con dos aplicaciones, cada una con el comprobante de su módulo; lo esperado de la
/// sesión sube con el recibo. Sobre Cartera real. Se corre en la fase 22. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ReciboMultiproductoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_recibo_con_dos_aplicaciones_y_dos_medios_y_el_comprobante_de_cada_modulo()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "multiproducto");
        var pagables = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/payables?person={c.Persona}");
        pagables.GetProperty("obligations").GetProperty("obligations").GetArrayLength().Should().Be(2);

        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m), CajaE2E.Abono(c.CreditoB, 300_000m)],
            [CajaE2E.Medio(c.Efectivo, 200_000m), CajaE2E.Medio(c.Transferencia, 150_000m, "TRF-1")]);
        var resp = await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid());

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var recibo = await RecaudoYTesoreriaE2E.LeerAsync(resp);
        recibo.GetProperty("publicId").GetGuid().Should().Be(op);
        recibo.GetProperty("applications").EnumerateArray().Should().HaveCount(2).And.OnlyContain(a => a.GetProperty("moduleVoucherNumber").GetString() != null);

        var documento = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/receipts/receipts/{op}/document?format=thermal")
        {
            Headers = { Authorization = new("Bearer", c.Token) },
        });
        documento.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
    }
}
