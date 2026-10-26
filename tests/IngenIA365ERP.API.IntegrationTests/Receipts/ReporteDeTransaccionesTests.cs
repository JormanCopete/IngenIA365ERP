using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, R1, T0387 (SC-017; FR-096): por HTTP, el reporte de transacciones de un mes con un recibo en efectivo sobre el umbral: se genera con la advertencia de la
/// estructura sin cotejar, se regenera (la anterior queda reemplazada), se marca cargado con radicado y el período ya no se regenera. Escrita para la corrida de cierre de
/// R1; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ReporteDeTransaccionesTests(CentralIdentityApiFixture fx)
{
    private static async Task<HttpResponseMessage> PostAsync(HttpClient http, string token, string url, object? cuerpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Headers = { Authorization = new("Bearer", token) } };
        if (cuerpo is not null) req.Content = System.Net.Http.Json.JsonContent.Create(cuerpo);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task Genera_regenera_y_marca_cargado()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "uiaf");
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m)], [CajaE2E.Medio(c.Efectivo, 50_000m)]);
        (await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.OK);
        var hoy = c.Escenario.Hoy;

        var generado = await PostAsync(http, c.Token, "/api/receipts/compliance/cash-transaction-reports", new { year = hoy.Year, month = hoy.Month });
        if (hoy < new DateOnly(2027, 1, 1))
        {
            // La estructura rige desde enero de 2027: antes, ningún período tiene layout vigente y se dice así.
            (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(generado)).Should().Be("Receipts.CashTransactionReport.LayoutMissing");
            return;
        }
        var cuerpo = await RecaudoYTesoreriaE2E.LeerAsync(generado);
        var primero = cuerpo.GetProperty("publicId").GetGuid();
        cuerpo.GetProperty("layoutVerified").GetBoolean().Should().BeFalse();

        var otra = await RecaudoYTesoreriaE2E.LeerAsync(await PostAsync(http, c.Token, $"/api/receipts/compliance/cash-transaction-reports/{primero}/regenerate", null));
        otra.GetProperty("version").GetInt32().Should().Be(2);

        var lista = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/compliance/cash-transaction-reports?year={hoy.Year}");
        lista.EnumerateArray().Should().Contain(r => r.GetProperty("publicId").GetGuid() == primero && r.GetProperty("status").GetInt32() == 3);
        if (otra.GetProperty("status").GetInt32() == 2)
        {
            var cargado = await PostAsync(http, c.Token, $"/api/receipts/compliance/cash-transaction-reports/{otra.GetProperty("publicId").GetGuid()}/uploaded",
                new { reference = "UIAF-E2E-1" });
            cargado.StatusCode.Should().Be(HttpStatusCode.OK, await cargado.Content.ReadAsStringAsync());
            (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(await PostAsync(http, c.Token, "/api/receipts/compliance/cash-transaction-reports",
                new { year = hoy.Year, month = hoy.Month }))).Should().Be("Receipts.CashTransactionReport.AlreadyUploaded");
        }
    }
}
