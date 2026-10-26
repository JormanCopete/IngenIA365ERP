using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US11, T0155 (US11 «Independent Test»; quickstart.md recorrido G pasos 6–7): de un archivo de tres órdenes el banco rechaza una por cuenta inexistente:
/// dos quedan «girada» con su hecho y una «rechazada» con su causal; Cartera recibe los resultados; cargar la misma respuesta dos veces no cambia nada; una línea con la
/// cuenta alterada no se informa hasta revisarla. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class GiroRechazadoYRegiroTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Dos_giradas_una_rechazada_y_la_alterada_espera_su_revision()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "resultados");
        var ordenes = new List<JsonElement>();
        foreach (var monto in new[] { 1_100_000m, 1_200_000m, 1_300_000m, 1_400_000m })
            ordenes.Add(await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, monto));
        var lote = await TesoreriaE2E.LoteAsync(http, t.Admin, t.CuentaDePagos, ordenes.Select(o => o.GetProperty("publicId").GetGuid()));
        var id = lote.GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit"), "enviar a aprobación");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve",
            new { }), "aprobar");
        var archivo = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/file");
        var archivoId = archivo.GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-files/{archivoId}/sent",
            new { uploadedAtBankBy = "Carga", authorizedAtBankBy = "Autoriza" }), "marcar enviado");

        // Cada orden es de su propio deudor (cada desembolso crea el suyo): cada línea lleva el documento de su beneficiario. Hasta la corrida del 2026-10-26 las
        // cuatro llevaban el del primero y las otras tres discrepaban por documento.
        string Doc(int k) => ordenes[k].GetProperty("beneficiaryTaxId").GetString()!;
        var hoy = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var respuesta = string.Join("\r\n",
            "linea;documento;cuenta;valor;resultado;causal;fecha",
            $"1;{Doc(0)};;1100000.00;Pagado;;{hoy}",
            $"2;{Doc(1)};;1200000.00;Pagado;;{hoy}",
            $"3;{Doc(2)};;1300000.00;Rechazado;Cuenta inexistente;{hoy}",
            $"4;{Doc(3)};000000001;1400000.00;Pagado;;{hoy}") + "\r\n";

        var cargada = await CargarAsync(http, t.Admin, archivoId, respuesta, Guid.NewGuid());
        cargada.GetProperty("informedCount").GetInt32().Should().Be(3, cargada.GetRawText());
        cargada.GetProperty("discrepancyCount").GetInt32().Should().Be(1);

        await CarteraE2E.DespacharAsync(fx, t.Escenario.Coop.TenantPublicId, 3);
        var bandeja = await CarteraE2E.GetAsync(http, t.Admin, "/api/treasury/payment-orders");
        Estado(bandeja, 1_100_000m).Should().Be(7);
        Estado(bandeja, 1_200_000m).Should().Be(7);
        Estado(bandeja, 1_300_000m).Should().Be(8);
        Estado(bandeja, 1_400_000m).Should().Be(6, "la línea con la cuenta alterada no se informa hasta revisarla (US11-7)");

        // La misma respuesta otra vez: nada cambia (UK (PaymentOrderId, Outcome)).
        var otraVez = await CargarAsync(http, t.Admin, archivoId, respuesta, Guid.NewGuid());
        otraVez.GetProperty("informedCount").GetInt32().Should().Be(0);
        var resultados = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{ordenes[0].GetProperty("publicId").GetGuid()}/results");
        resultados.GetArrayLength().Should().Be(1);
    }

    private static int Estado(JsonElement bandeja, decimal neto) =>
        bandeja.EnumerateArray().First(o => o.GetProperty("netAmount").GetDecimal() == neto).GetProperty("status").GetInt32();

    private static async Task<JsonElement> CargarAsync(HttpClient http, string token, Guid archivo, string contenido, Guid clave)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/treasury/payment-files/{archivo}/responses");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", clave.ToString());
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(bytes, "archivo", "RESPUESTA.csv");
        req.Content = multipart;
        using var resp = await http.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        ((int)resp.StatusCode).Should().BeInRange(200, 299, texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
