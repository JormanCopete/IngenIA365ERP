using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US6, T0189 («Independent Test», quickstart recorrido D; SC-009): con Cartera real, cargar un extracto de cinco abonos y volver a cargarlo («5 omitidos
/// por huella» y el archivo reconocido); identificar cuatro a mano —uno dividido entre dos créditos con suma exacta— y dejar el quinto como partida; la partida se
/// identifica después y el recibo lo dice. Escrita en la fase 11; se corre en la fase 22 en los dos motores.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ExtractoIdentificacionYPartidasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Cargar_identificar_dividir_y_resolver_la_partida()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "extracto");
        var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, c.Token, c.Escenario, "RECEXT", 1, c.Escenario.CuentaDeOrigen);
        var hoy = c.Escenario.Hoy;
        var csv = string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            $"{hoy:yyyy-MM-dd};150000.00;R1;;;Abono;",
            $"{hoy:yyyy-MM-dd};200000.00;R2;;;Abono;",
            $"{hoy:yyyy-MM-dd};90000.00;R3;;;Abono;",
            $"{hoy:yyyy-MM-dd};70000.00;R4;;;Abono;",
            $"{hoy:yyyy-MM-dd};55000.00;R5;9999;Desconocido;Abono;");

        var carga = await ExtractoE2E.CargarAsync(http, c.Token, cuenta, csv);
        carga.GetProperty("loaded").GetInt32().Should().Be(5);
        var otra = await ExtractoE2E.CargarCrudoAsync(http, c.Token, cuenta, csv);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(otra)).Should().Be("Treasury.Statement.AlreadyLoaded");
        var mismasLineas = await ExtractoE2E.CargarAsync(http, c.Token, cuenta, csv + "\r\n");
        mismasLineas.GetProperty("possibleDuplicates").GetInt32().Should().Be(5, "sin secuencia del banco los iguales quedan para que una persona decida");

        var abonos = (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/bank-items?account={cuenta}&onlyPending=true")).GetProperty("items")
            .EnumerateArray().Where(a => !a.GetProperty("possibleDuplicate").GetBoolean()).ToList();
        abonos.Should().HaveCount(5);
        Guid Abono(decimal v) => abonos.First(a => a.GetProperty("amount").GetDecimal() == v).GetProperty("publicId").GetGuid();

        foreach (var (valor, credito) in new[] { (150_000m, c.CreditoA), (90_000m, c.CreditoB), (70_000m, c.CreditoA) })
            await ExtractoE2E.IdentificarAsync(http, c.Token, Abono(valor), [new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = valor, productPublicId = credito }]);
        var dividido = await ExtractoE2E.IdentificarAsync(http, c.Token, Abono(200_000m),
        [
            new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = 120_000m, productPublicId = c.CreditoA },
            new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = 80_000m, productPublicId = c.CreditoB },
        ]);
        dividido.GetProperty("pending").GetDecimal().Should().Be(0m);

        var partida = await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/bank-items/{Abono(55_000m)}/unidentified", new { note = "Sin identificar" });
        partida.GetProperty("balance").GetDecimal().Should().Be(55_000m);
        var identificada = await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/unidentified-items/{partida.GetProperty("publicId").GetGuid()}/identify",
            new { parts = new[] { new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = 55_000m, productPublicId = c.CreditoA } } });
        identificada.GetProperty("status").GetInt32().Should().Be(3, "identificada por completo");
        var recibos = await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/receipts?channel=Bank");
        recibos.GetArrayLength().Should().BeGreaterThanOrEqualTo(5);
    }
}

/// <summary>Lo común de las e2e del extracto y la identificación de abonos (fase 11). (nuevo)</summary>
public static class ExtractoE2E
{
    public static async Task<HttpResponseMessage> CargarCrudoAsync(HttpClient http, string token, Guid cuenta, string csv, bool revisar = false)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/treasury/statements");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(bytes, "archivo", "EXTRACTO.csv");
        multipart.Add(new StringContent(cuenta.ToString()), "account");
        multipart.Add(new StringContent(revisar ? "true" : "false"), "preview");
        req.Content = multipart;
        return await http.SendAsync(req);
    }

    public static async Task<JsonElement> CargarAsync(HttpClient http, string token, Guid cuenta, string csv)
    {
        var resp = await CargarCrudoAsync(http, token, cuenta, csv);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await RecaudoYTesoreriaE2E.LeerAsync(resp);
    }

    public static async Task<JsonElement> IdentificarAsync(HttpClient http, string token, Guid abono, object[] partes, Guid? corrida = null) =>
        await InventarioE2E.ExitoAsync(http, token, HttpMethod.Post, $"/api/receipts/bank-items/{abono}/identify", new { parts = partes, runPublicId = corrida });
}
