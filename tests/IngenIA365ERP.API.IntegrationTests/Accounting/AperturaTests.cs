using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// T117 — US13 (FR-084..FR-087) por HTTP, en una cooperativa propia con primer ejercicio 2026: la
/// plantilla trae los encabezados en la fila 1; importar con errores responde 422 con las filas y
/// no guarda nada; importar bien deja un borrador AP del 31/12/2025 que se contabiliza; el balance
/// del primer mes lo muestra como saldo inicial al centavo y el tercero ve su documento pendiente;
/// una segunda apertura se rechaza hasta reversar la primera. Con <c>RUN_PERF_TESTS=1</c>, 5 000
/// filas se importan en menos de dos minutos (SC-014).
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class AperturaTests(CentralIdentityApiFixture fx)
{
    private const string Caja = "11050501";      // 110505 CAJA GENERAL
    private const string Cartera = "14050501";   // 140505 créditos (exige tercero y documento cruce)
    private const string Aportes = "31050501";   // 310505 aportes sociales
    private const string Agrupacion = "140505";

    private static readonly string[] Encabezados = ["cuenta", "tercero", "tipoDocumento", "numeroDocumento", "centroCosto", "sucursal", "debito", "credito", "detalle"];

    [Fact]
    public async Task La_apertura_se_importa_se_contabiliza_como_saldo_inicial_y_es_unica_hasta_reversarla()
    {
        var coop = await CooperativaAisladaAsync(fx, "apertura", 2026);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        await CrearAuxiliarAsync(http, admin, Caja, "Caja general");
        await CrearCuentaAsync(http, admin, Cartera, "Créditos de consumo", "140505", ["CNT"], tercero: true, cruce: true);
        await CrearAuxiliarAsync(http, admin, Aportes, "Aportes sociales");
        var asociado = await CrearPersonaAsync(http, admin, "Asociado");
        var documentoAsociado = await DocumentoDeAsync(http, admin, asociado);

        // (a) la plantilla: xlsx con los nueve encabezados en la fila 1 de la primera hoja
        var plantilla = await EnviarAsync(http, admin, HttpMethod.Get, "/api/accounting/opening/template.xlsx", null);
        plantilla.StatusCode.Should().Be(HttpStatusCode.OK, $"plantilla: «{await plantilla.Content.ReadAsStringAsync()}»");
        plantilla.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using (var libro = new XLWorkbook(new MemoryStream(await plantilla.Content.ReadAsByteArrayAsync())))
        {
            var hoja = libro.Worksheets.First();
            Enumerable.Range(1, Encabezados.Length).Select(i => hoja.Cell(1, i).GetString()).Should().Equal(Encabezados);
            hoja.Cell(2, 1).IsEmpty().Should().BeTrue("la plantilla va vacía: sólo encabezados");
        }

        // (b) el estado antes de importar
        var estado = await GetAsync(http, admin, "/api/accounting/opening");
        estado.GetProperty("expectedDate").GetString().Should().Be("2025-12-31");
        estado.GetProperty("posted").ValueKind.Should().Be(JsonValueKind.Null);

        // (c) con errores: 422 con cada fila y su columna, y nada guardado
        var conErrores = await ImportarAsync(http, admin, Csv(
            Fila(Agrupacion, 100m, 0m),                                          // fila 2: agrupación
            Fila(Cartera, 250m, 0m, tercero: "9999999990", tipo: "PG", numero: "1"), // fila 3: tercero inexistente
            Fila(Cartera, 250m, 0m, tercero: documentoAsociado),                 // fila 4: falta el documento cruce
            Fila(Caja, 10.005m, 0m),                                             // fila 5: tres decimales
            Fila(Aportes, 0m, 300m)));
        conErrores.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await conErrores.Content.ReadAsStringAsync()}»");
        var cuerpo = await LeerAsync(conErrores);
        cuerpo.GetProperty("code").GetString().Should().Be("Accounting.Opening.Invalid");
        var errores = cuerpo.GetProperty("data").GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("row").GetInt32(), e.GetProperty("column").GetString())).ToList();
        errores.Should().Contain((2, "cuenta")).And.Contain((3, "tercero")).And.Contain((4, "tipoDocumento")).And.Contain((5, "debito"));
        (await GetAsync(http, admin, "/api/accounting/opening")).GetProperty("drafts").GetArrayLength().Should().Be(0, "nada a medias");

        // (d) bien: un borrador AP del 31/12/2025 con las líneas del archivo
        var importada = await ImportarAsync(http, admin, Csv(
            Fila(Caja, 1_500_000m, 0m),
            Fila(Cartera, 2_500_000.5m, 0m, tercero: documentoAsociado, tipo: "PG", numero: "CR-77", detalle: "Crédito de consumo"),
            Fila(Aportes, 0m, 4_000_000.5m)));
        importada.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await importada.Content.ReadAsStringAsync()}»");
        var resultado = await LeerAsync(importada);
        resultado.GetProperty("lines").GetInt32().Should().Be(3);
        var borrador = resultado.GetProperty("draftPublicId").GetGuid();
        var documento = await GetAsync(http, admin, $"/api/accounting/documents/{borrador}");
        documento.GetProperty("voucherTypeCode").GetString().Should().Be("AP");
        documento.GetProperty("kind").GetString().Should().Be("Opening");
        documento.GetProperty("status").GetString().Should().Be("Draft");
        documento.GetProperty("date").GetString().Should().Be("2025-12-31");
        documento.GetProperty("totalDebit").GetDecimal().Should().Be(4_000_000.5m);
        var cartera = documento.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("accountCode").GetString() == Cartera);
        cartera.GetProperty("personPublicId").GetGuid().Should().Be(asociado);
        cartera.GetProperty("crossDocumentType").GetString().Should().Be("PG");
        cartera.GetProperty("crossDocumentNumber").GetString().Should().Be("CR-77");
        (await GetAsync(http, admin, "/api/accounting/opening")).GetProperty("drafts").EnumerateArray().Should().ContainSingle(d => d.GetProperty("publicId").GetGuid() == borrador);

        // (e) contabilizar: número AP, la apertura vigente
        var post = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{borrador}/post", null);
        post.StatusCode.Should().Be(HttpStatusCode.OK, $"contabilizar la apertura: «{await post.Content.ReadAsStringAsync()}»");
        estado = await GetAsync(http, admin, "/api/accounting/opening");
        estado.GetProperty("posted").GetProperty("publicId").GetGuid().Should().Be(borrador);
        (await GetAsync(http, admin, "/api/accounting/setup")).GetProperty("openingDocumentPublicId").GetGuid().Should().Be(borrador);

        // (f) el balance de enero de 2026 arranca con la apertura como saldo inicial, sin movimiento del mes, al centavo
        var balance = await InformeAsync(http, admin, "trial-balance?from=2026-01-01&to=2026-01-31");
        var filaCaja = balance.PorCodigo(Caja);
        filaCaja.Should().NotBeNull();
        balance.Numero(filaCaja!.Value, "Saldo inicial").Should().Be(1_500_000m);
        balance.Numero(filaCaja.Value, "Débitos").Should().Be(0m, "la apertura no es movimiento del mes");
        var filaCartera = balance.PorCodigo(Cartera);
        balance.Numero(filaCartera!.Value, "Saldo inicial").Should().Be(2_500_000.5m);
        Math.Abs(balance.Numero(balance.PorCodigo(Aportes)!.Value, "Saldo inicial")).Should().Be(4_000_000.5m);

        // (g) el tercero ve su documento pendiente
        var pendientes = await InformeAsync(http, admin, $"pending-documents?from=2026-01-01&to=2026-01-31&person={asociado}");
        pendientes.Filas.Should().Contain(f => pendientes.Texto(f, "Número") == "CR-77" && pendientes.Numero(f, "Saldo") == 2_500_000.5m);

        // (h) otra apertura, importada o digitada, se rechaza mientras ésta siga vigente
        var segunda = await ImportarAsync(http, admin, Csv(Fila(Caja, 1m, 0m), Fila(Aportes, 0m, 1m)));
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var rechazo = await LeerAsync(segunda);
        rechazo.GetProperty("code").GetString().Should().Be("Accounting.Opening.AlreadyExists");
        rechazo.GetProperty("data").GetProperty("openingDocumentPublicId").GetGuid().Should().Be(borrador);
        var digitada = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "AP", date = new DateOnly(2026, 1, 15), description = "Otra apertura", lines = new[] { Linea(Caja, null, 1m, 0m), Linea(Aportes, null, 0m, 1m) },
        });
        (await CodigoDeErrorAsync(digitada)).Should().Be("Accounting.Opening.AlreadyExists");

        // (i) reversar libera el cupo; la reversión es también apertura, del 31/12/2025, y el balance vuelve a cero
        var reversion = await ReversarAsync(http, admin, borrador, "Saldos equivocados");
        var reverso = await GetAsync(http, admin, $"/api/accounting/documents/{reversion}");
        reverso.GetProperty("kind").GetString().Should().Be("Opening");
        reverso.GetProperty("date").GetString().Should().Be("2025-12-31");
        estado = await GetAsync(http, admin, "/api/accounting/opening");
        estado.GetProperty("posted").ValueKind.Should().Be(JsonValueKind.Null);
        estado.GetProperty("reversed").EnumerateArray().Should().ContainSingle(r => r.GetProperty("publicId").GetGuid() == borrador && r.GetProperty("reversedByPublicId").GetGuid() == reversion);
        (await GetAsync(http, admin, "/api/accounting/setup")).GetProperty("openingDocumentPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        var balanceReversado = await InformeAsync(http, admin, "trial-balance?from=2026-01-01&to=2026-01-31");
        var cajaReversada = balanceReversado.PorCodigo(Caja);
        (cajaReversada is null ? 0m : balanceReversado.Numero(cajaReversada.Value, "Saldo inicial")).Should().Be(0m);

        var tercera = await ImportarAsync(http, admin, Csv(Fila(Caja, 900_000m, 0m), Fila(Aportes, 0m, 900_000m)));
        tercera.StatusCode.Should().Be(HttpStatusCode.Created);
        var borrador2 = (await LeerAsync(tercera)).GetProperty("draftPublicId").GetGuid();
        (await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{borrador2}/post", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var eventos = await EventosDeAuditoriaAsync(http, admin, "Accounting", "Accounting.Opening.Imported", minimo: 2);
        eventos.Count.Should().BeGreaterThanOrEqualTo(2, "cada importación queda auditada; las dos aperturas quedan referenciadas");
    }

    [Fact]
    public async Task Cinco_mil_filas_se_importan_en_menos_de_dos_minutos()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1") return;
        var coop = await CooperativaAisladaAsync(fx, "aperturaperf", 2026);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        await CrearAuxiliarAsync(http, admin, Caja, "Caja general");
        await CrearAuxiliarAsync(http, admin, Aportes, "Aportes sociales");
        var filas = new List<string[]>();
        for (var i = 0; i < 2500; i++)
        {
            filas.Add(Fila(Caja, 1000m + i, 0m, detalle: $"Fila {i}"));
            filas.Add(Fila(Aportes, 0m, 1000m + i));
        }

        var reloj = Stopwatch.StartNew();
        var resp = await ImportarAsync(http, admin, Csv(filas.ToArray()));
        reloj.Stop();

        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await resp.Content.ReadAsStringAsync()}»");
        (await LeerAsync(resp)).GetProperty("lines").GetInt32().Should().Be(5000);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2), "SC-014");
    }

    // ------------------------------------------------------------------------------- ayudantes --

    private static string[] Fila(string cuenta, decimal debito, decimal credito, string? tercero = null, string? tipo = null, string? numero = null, string? detalle = null) =>
        [cuenta, tercero ?? "", tipo ?? "", numero ?? "", "", "", debito == 0m ? "" : debito.ToString(CultureInfo.InvariantCulture), credito == 0m ? "" : credito.ToString(CultureInfo.InvariantCulture), detalle ?? ""];

    private static byte[] Csv(params string[][] filas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Encabezados));
        foreach (var f in filas) sb.AppendLine(string.Join(';', f));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static async Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, byte[] csv)
    {
        using var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(csv);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "apertura.csv");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/accounting/opening/import") { Content = contenido };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }
}
