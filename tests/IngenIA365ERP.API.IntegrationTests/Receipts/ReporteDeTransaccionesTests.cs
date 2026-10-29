using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, R1, T0387 (SC-017; FR-096), al Anexo 2 TRSO desde la segunda ronda del 2026-10-27 (T0507): por HTTP, el reporte de transacciones a la UIAF de un mes
/// con un recibo en efectivo sobre el umbral. Sin el código de la organización ni el usuario SIREL queda validado sin archivo; con ellos se genera con el nombre del
/// anexo (<c>TRSOSSTTTCCCCCMMAA.txt</c>) y la estructura cotejada, se regenera (la anterior queda reemplazada), se marca cargado con radicado y el período ya no se
/// regenera. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ReporteDeTransaccionesTests(CentralIdentityApiFixture fx)
{
    private const string Codigo = "0600100777";
    private const string Motivo = "Reporte de transacciones a la UIAF (e2e)";
    private const string Ruta = "/api/receipts/compliance/cash-transaction-reports";

    private static async Task<HttpResponseMessage> PostAsync(HttpClient http, string token, string url, object? cuerpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Headers = { Authorization = new("Bearer", token) } };
        if (cuerpo is not null) req.Content = System.Net.Http.Json.JsonContent.Create(cuerpo);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task Sin_identificacion_valida_y_con_ella_genera_regenera_y_marca_cargado()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "uiaf");
        var hoy = c.Escenario.Hoy;
        var coop = c.Escenario.Coop;
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, coop, "RCD", "Sarlaft.UmbralTransaccionIndividual", "40000", hoy, Motivo);
        // El municipio de la oficina (DANE) es un campo obligatorio del archivo.
        var sucursal = await CarteraE2E.GetAsync(http, coop.TokenAdmin, $"/api/core/branches/{coop.SucursalPrincipal}");
        (await InventarioE2E.MandarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/branches/{coop.SucursalPrincipal}", new
        {
            publicId = coop.SucursalPrincipal, code = sucursal.GetProperty("code").GetString(), name = sucursal.GetProperty("name").GetString(),
            shortName = sucursal.GetProperty("shortName").GetString(), tenantBranchPublicId = sucursal.GetProperty("tenantBranchPublicId").GetString() is { } o ? Guid.Parse(o) : (Guid?)null,
            municipalityDaneCode = "05001",
        })).IsSuccessStatusCode.Should().BeTrue("la oficina recibe su municipio");
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m)], [CajaE2E.Medio(c.Efectivo, 50_000m)]);
        // Sobre el umbral individual la caja pide quién efectúa el pago: es «quien realiza la transacción» del archivo.
        var mensajero = await CarteraE2E.PersonaAsync(http, c.Token, "Mensajero", "Uiaf");
        await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{op}/delivered-by", new { deliveredByPersonPublicId = mensajero });
        var confirmado = await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid());
        confirmado.StatusCode.Should().Be(HttpStatusCode.OK, await confirmado.Content.ReadAsStringAsync());

        var sinCodigo = await RecaudoYTesoreriaE2E.LeerAsync(await PostAsync(http, c.Token, Ruta, new { year = hoy.Year, month = hoy.Month }));
        sinCodigo.GetProperty("status").GetInt32().Should().Be(1, "sin el código de la organización ni el usuario SIREL no hay archivo");
        sinCodigo.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("code").GetString()).Should()
            .Contain(["Recaudo.Sarlaft.CodigoDeOrganizacionFaltante", "Recaudo.Sarlaft.UsuarioSirelFaltante"]);

        await RecaudoYTesoreriaE2E.ParametroAsync(fx, coop, "RCD", "Sarlaft.Reporte.CodigoDeOrganizacion", Codigo, hoy, Motivo);
        await RecaudoYTesoreriaE2E.ParametroAsync(fx, coop, "RCD", "Sarlaft.Reporte.UsuarioSirel", "oficial.e2e", hoy, Motivo);
        var cuerpo = await RecaudoYTesoreriaE2E.LeerAsync(await PostAsync(http, c.Token, Ruta, new { year = hoy.Year, month = hoy.Month }));
        cuerpo.GetProperty("status").GetInt32().Should().Be(2, cuerpo.GetProperty("issues").ToString());
        cuerpo.GetProperty("layoutVerified").GetBoolean().Should().BeTrue("la estructura está cotejada con el anexo");
        cuerpo.GetProperty("layoutCode").GetString().Should().Be("UIAF-TRSO-v1");
        cuerpo.GetProperty("fileName").GetString().Should().Be($"TRSO{Codigo}{hoy.Month:00}{hoy.Year % 100:00}.txt");
        cuerpo.GetProperty("lines").GetInt32().Should().BeGreaterThan(0);
        var primero = cuerpo.GetProperty("publicId").GetGuid();

        var otra = await RecaudoYTesoreriaE2E.LeerAsync(await PostAsync(http, c.Token, $"{Ruta}/{primero}/regenerate", null));
        otra.GetProperty("version").GetInt32().Should().Be(3, "la primera quedó validada sin archivo, la segunda generada");
        otra.GetProperty("status").GetInt32().Should().Be(2);

        var lista = await InventarioE2E.GetAsync(http, c.Token, $"{Ruta}?year={hoy.Year}");
        lista.EnumerateArray().Should().Contain(r => r.GetProperty("publicId").GetGuid() == primero && r.GetProperty("status").GetInt32() == 3);
        var cargado = await PostAsync(http, c.Token, $"{Ruta}/{otra.GetProperty("publicId").GetGuid()}/uploaded", new { reference = "UIAF-E2E-1" });
        cargado.StatusCode.Should().Be(HttpStatusCode.OK, await cargado.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(await PostAsync(http, c.Token, Ruta, new { year = hoy.Year, month = hoy.Month })))
            .Should().Be("Receipts.CashTransactionReport.AlreadyUploaded");
    }
}
