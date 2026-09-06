using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>T125 — US6 por HTTP: lote inválido → 422 sin cambios; lote válido → novedades listadas con origen Import.</summary>
[Collection(NominaCollection.Nombre)]
public class ImportNoveltiesEndpointsTests(CentralIdentityApiFixture fx)
{
    private static async Task<HttpResponseMessage> SubirAsync(HttpClient http, string token, Guid periodoId, byte[] contenido, string nombre = "novedades.csv")
    {
        using var form = new MultipartFormDataContent();
        var parte = new ByteArrayContent(contenido);
        parte.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(parte, "file", nombre);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties/import") { Content = form };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }

    private static byte[] Csv(params string[] filas) =>
        new UTF8Encoding(true).GetBytes("documento;concepto;cantidad;valor;desde;hasta;observacion\n" + string.Join("\n", filas) + "\n");

    [Fact]
    public async Task Un_lote_invalido_responde_422_sin_cambios_y_uno_valido_entra_completo()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var (_, documento) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Carla", 2_200_000m, new DateTime(2024, 6, 1));
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));

        // La plantilla que se descarga es la que el parser espera.
        var plantilla = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, "/api/payroll/novelties/import-template", null);
        plantilla.StatusCode.Should().Be(HttpStatusCode.OK);
        plantilla.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        (await plantilla.Content.ReadAsStringAsync()).TrimStart((char)0xFEFF).Should().StartWith("documento;concepto");

        // Lote inválido: una fila buena y una con documento inexistente → nada entra.
        var invalido = await SubirAsync(http, admin, periodoId, Csv($"{documento};HEX_NOCTURNA;6;;;;", "999999999;HEX_NOCTURNA;1;;;;"));
        invalido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await invalido.Content.ReadAsStringAsync()}»");
        var resultadoInvalido = await NominaE2E.LeerAsync(invalido);
        resultadoInvalido.GetProperty("applied").GetInt32().Should().Be(0);
        resultadoInvalido.GetProperty("errors").GetArrayLength().Should().Be(1);
        resultadoInvalido.GetProperty("errors")[0].GetProperty("row").GetInt32().Should().Be(3);
        resultadoInvalido.GetProperty("errors")[0].GetProperty("column").GetString().Should().Be("documento");
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/novelties")).GetArrayLength().Should().Be(0, "todo o nada");

        // Lote válido.
        var valido = await SubirAsync(http, admin, periodoId, Csv($"{documento};HEX_NOCTURNA;6;;;;Turno del 12", $"{documento};BONIF_NO_SALARIAL;;150000;;;Metas"));
        valido.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await valido.Content.ReadAsStringAsync()}»");
        var resultado = await NominaE2E.LeerAsync(valido);
        resultado.GetProperty("applied").GetInt32().Should().Be(2);
        resultado.GetProperty("errors").GetArrayLength().Should().Be(0);
        var lote = resultado.GetProperty("batchId").GetGuid();

        var importadas = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/novelties?origin=Import");
        importadas.GetArrayLength().Should().Be(2);
        importadas.EnumerateArray().Select(n => n.GetProperty("conceptCode").GetString()).Should().BeEquivalentTo(["HEX_NOCTURNA", "BONIF_NO_SALARIAL"]);
        importadas.EnumerateArray().Should().OnlyContain(n => n.GetProperty("origin").GetString() == "Import");
        lote.Should().NotBeEmpty();

        // Un archivo de más de 5 MB se rechaza antes de leerlo.
        var grande = new byte[6 * 1024 * 1024];
        Encoding.UTF8.GetBytes("documento;concepto\n").CopyTo(grande, 0);
        var rechazo = await SubirAsync(http, admin, periodoId, grande, "grande.csv");
        rechazo.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Sin_permiso_de_importar_la_ruta_no_existe()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, ctx.TokenAdmin, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        var resp = await SubirAsync(http, ctx.TokenSoloLectura, periodoId, Csv("1;HEX_NOCTURNA;1;;;;"));

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
