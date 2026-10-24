using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US26, T0165 (US26 «Independent Test»; quickstart.md recorrido G pasos 2 y 6; SC-026): la cuenta destino cambiada queda pendiente hasta que otra persona
/// la aprueba con su certificación; la orden a ella queda marcada en el lote y el lote no se aprueba sin su revisión expresa; una discrepancia inducida entre el archivo y
/// la respuesta se detecta antes de informar. Se corre en la fase 22 (los parámetros <c>CuentasDestino.*</c> se fijan aquí para no depender de la semilla).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class CuentaDestinoCambiadaYArchivoAlteradoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_cuenta_cambiada_exige_revision_expresa_y_la_respuesta_alterada_no_se_informa()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "antifraude");
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        await ParametroAsync(http, t.Admin, "CuentasDestino.DiasDeEspera", "0", hoy.AddDays(-1));
        await ParametroAsync(http, t.Admin, "CuentasDestino.DiasDeMarca", "30", hoy.AddDays(-1));

        var orden = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 1_750_000m);
        var ordenId = orden.GetProperty("publicId").GetGuid();
        var persona = orden.GetProperty("beneficiaryPersonPublicId").GetGuid();
        // El beneficiario cambia de cuenta: la pide el administrador con la certificación y la aprueba otra persona; queda «cambiada hace 0 días».
        await TesoreriaE2E.CuentaDestinoAprobadaAsync(http, t.Escenario, persona, "0987654321");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-orders/{ordenId}/revalidate"), "revalidar");

        var lote = await TesoreriaE2E.LoteAsync(http, t.Admin, t.CuentaDePagos, [ordenId]);
        var id = lote.GetProperty("publicId").GetGuid();
        var item = lote.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("requiresReview").GetBoolean().Should().BeTrue();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit"), "enviar a aprobación");

        var sinRevision = await TesoreriaE2E.MandarAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinRevision)).Should().Be("Treasury.Batch.ReviewRequired");
        var propia = await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/items/{item.GetProperty("publicId").GetGuid()}/review",
            new { reason = "Revisé la certificación" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(propia)).Should().Be("Treasury.Batch.SelfReview");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post,
            $"/api/treasury/payment-batches/{id}/items/{item.GetProperty("publicId").GetGuid()}/review", new { reason = "Llamé al beneficiario: confirmó la cuenta" }),
            "revisión expresa");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { }),
            "aprobar con la revisión");

        // El archivo se envía y la respuesta trae la cuenta alterada: no se informa hasta revisarla.
        var archivo = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/file");
        var archivoId = archivo.GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-files/{archivoId}/sent",
            new { uploadedAtBankBy = "Carga", authorizedAtBankBy = "Autoriza" }), "marcar enviado");
        var respuesta = "linea;documento;cuenta;valor;resultado;causal;fecha\r\n" +
                        $"1;{orden.GetProperty("beneficiaryTaxId").GetString()};111111111;1750000.00;Pagado;;{hoy:yyyy-MM-dd}\r\n";
        var cargada = await ResultadosE2E.CargarRespuestaAsync(http, t.Escenario.Usuarios.Pruebas.Token, archivoId, respuesta);
        cargada.GetProperty("discrepancyCount").GetInt32().Should().Be(1);
        cargada.GetProperty("informedCount").GetInt32().Should().Be(0);
        var bandeja = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{ordenId}");
        bandeja.GetProperty("status").GetInt32().Should().Be(6, "la línea alterada no se informa (FR-212)");
    }

    private static async Task ParametroAsync(HttpClient http, string admin, string clave, string valor, DateOnly desde)
    {
        var resp = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"/api/inventory/parameters/TES/{clave}/versions", new
        {
            scopeKind = "None", value = valor, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Antifraude de Tesorería (e2e)",
        });
        resp.IsSuccessStatusCode.Should().BeTrue($"parámetro {clave}: «{await resp.Content.ReadAsStringAsync()}»");
    }
}

/// <summary>Lo común de las e2e de resultados del giro (feature 015, US11 y US26). (nuevo)</summary>
public static class ResultadosE2E
{
    /// <summary>Carga una respuesta del banco por multipart, con su clave.</summary>
    public static async Task<JsonElement> CargarRespuestaAsync(HttpClient http, string token, Guid archivo, string contenido)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/treasury/payment-files/{archivo}/responses");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var multipart = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(contenido));
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        multipart.Add(bytes, "archivo", "RESPUESTA.csv");
        req.Content = multipart;
        using var resp = await http.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        ((int)resp.StatusCode).Should().BeInRange(200, 299, texto);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
