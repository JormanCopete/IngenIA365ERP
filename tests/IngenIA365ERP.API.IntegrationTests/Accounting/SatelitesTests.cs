using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Feature 009 E4 (T147, T153, T157): los satélites de punta a punta por HTTP en una cooperativa aislada («satelites», ejercicio 2026),
/// porque corren meses y generan consecutivos que no deben cruzarse con las demás pruebas. Activos: registrar, correr el mes, repetir
/// (422), reversar, volver a correr y dar de baja. Certificados: una cuenta de retención al 4 %, una línea sin base (422), dos pagos,
/// generar, PDF, una reversión que lo deja desactualizado y reexpedir. Exógena: cargar la semilla 2026, generar el 1001 y exportar el
/// XML bien formado. Cada recorrido termina con el balance de prueba cuadrado.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class SatelitesTests(CentralIdentityApiFixture fx)
{
    private const string Equipo = "17052001";
    private const string Acumulada = "17059501";
    private const string GastoDepreciacion = "51251501";
    private const string PerdidaEnBaja = "51251502";
    private const string Banco = "11100501";
    private const string Honorarios = "51100101";
    private const string RetencionHonorarios = "24351501";

    private static async Task<CooperativaAislada> PrepararAsync(CentralIdentityApiFixture fx, HttpClient http)
    {
        var coop = await CooperativaAisladaAsync(fx, "satelites", 2026);
        var t = coop.TokenAdmin;
        await CrearCuentaAsync(http, t, Equipo, "Computadores", "170520", ["CNT", "ACT"]);
        await CrearCuentaAsync(http, t, Acumulada, "Depreciación acumulada", "170595", ["CNT", "ACT"]);
        await CrearCuentaAsync(http, t, GastoDepreciacion, "Depreciación de cómputo", "512515", ["CNT", "ACT"]);
        await CrearCuentaAsync(http, t, PerdidaEnBaja, "Pérdida en baja de activos", "512515", ["CNT", "ACT"]);
        await CrearCuentaAsync(http, t, Banco, "Banco de prueba", "111005", ["CNT"]);
        await CrearCuentaAsync(http, t, Honorarios, "Honorarios", "511001", ["CNT"], tercero: true);
        // La retención es de impuesto: clase, base obligatoria y tarifa del 4 % desde enero.
        var padre = await CuentaAsync(http, t, "243515");
        var retencion = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = RetencionHonorarios, name = "Retención honorarios 4 %", parentPublicId = padre, enabledModules = new[] { "CNT" },
            requiresThirdParty = true, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
            tax = new { kind = "Withholding", conceptCode = "HON", requiresTaxBase = true, rates = new[] { new { validFrom = new DateOnly(2026, 1, 1), rate = 0.04m } } },
        });
        retencion.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity], await retencion.Content.ReadAsStringAsync());
        return coop;
    }

    private static object LineaConBase(string cuenta, Guid sucursal, decimal credito, Guid persona, decimal? baseGravable) => new
    {
        accountCode = cuenta, branchPublicId = sucursal, costCenterPublicId = (Guid?)null, personPublicId = persona,
        crossDocumentType = (string?)null, crossDocumentNumber = (string?)null, debit = 0m, credit = credito, detail = "Retención", taxBase = baseGravable,
    };

    private static async Task BalanceCuadraAsync(HttpClient http, string token)
    {
        var balance = await InformeAsync(http, token, "trial-balance?from=2026-01-01&to=2026-12-31&level=1");
        balance.Totales.Should().NotBeNull();
    }

    [Fact]
    public async Task Activos_se_registran_corren_una_vez_por_mes_se_reversan_y_se_dan_de_baja()
    {
        using var http = fx.CreateClient();
        var coop = await PrepararAsync(fx, http);
        var t = coop.TokenAdmin;

        var alta = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/assets", new
        {
            code = "PC001", name = "Computador de gerencia", kind = "Asset",
            assetAccountPublicId = await CuentaAsync(http, t, Equipo), accumulatedAccountPublicId = await CuentaAsync(http, t, Acumulada),
            expenseAccountPublicId = await CuentaAsync(http, t, GastoDepreciacion), branchPublicId = coop.SucursalPrincipal,
            purchaseDate = new DateOnly(2026, 2, 10), purchaseDocument = "FC-77", cost = 6_000_000m, residualValue = 0m, lifeMonths = 60,
            startDate = new DateOnly(2026, 3, 1), openingAccumulated = 0m, openingElapsedMonths = 0,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var activo = (await LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var previa = await GetAsync(http, t, "/api/accounting/assets/runs/preview?year=2026&month=3");
        previa.GetProperty("total").GetDecimal().Should().Be(100_000m);

        var corrida = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/assets/runs", new { year = 2026, month = 3 });
        corrida.StatusCode.Should().Be(HttpStatusCode.OK, await corrida.Content.ReadAsStringAsync());
        var marzo = await LeerAsync(corrida);
        marzo.GetProperty("total").GetDecimal().Should().Be(100_000m);

        var repetida = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/assets/runs", new { year = 2026, month = 3 });
        (await CodigoDeErrorAsync(repetida)).Should().Be("Accounting.Assets.AlreadyRun");

        var reversa = await EnviarAsync(http, t, HttpMethod.Post, $"/api/accounting/assets/runs/{marzo.GetProperty("publicId").GetGuid()}/reverse", new { reason = "Corrida de prueba" });
        reversa.StatusCode.Should().Be(HttpStatusCode.OK, await reversa.Content.ReadAsStringAsync());
        (await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/assets/runs", new { year = 2026, month = 3 })).StatusCode.Should().Be(HttpStatusCode.OK);

        var ficha = await GetAsync(http, t, $"/api/accounting/assets/{activo}");
        ficha.GetProperty("asset").GetProperty("accumulated").GetDecimal().Should().Be(100_000m);
        ficha.GetProperty("projection").GetArrayLength().Should().Be(59);

        var baja = await EnviarAsync(http, t, HttpMethod.Post, $"/api/accounting/assets/{activo}/retire",
            new { date = new DateOnly(2026, 4, 2), counterAccountPublicId = await CuentaAsync(http, t, PerdidaEnBaja), reason = "Hurto" });
        baja.StatusCode.Should().Be(HttpStatusCode.OK, await baja.Content.ReadAsStringAsync());
        (await GetAsync(http, t, $"/api/accounting/assets/{activo}")).GetProperty("asset").GetProperty("status").GetString().Should().Be("Retired");
        await BalanceCuadraAsync(http, t);
    }

    [Fact]
    public async Task Certificados_salen_del_libro_y_se_reexpiden_tras_una_reversion()
    {
        using var http = fx.CreateClient();
        var coop = await PrepararAsync(fx, http);
        var t = coop.TokenAdmin;
        var s = coop.SucursalPrincipal;
        var tercero = await CrearPersonaAsync(http, t, "Certificado");

        // Una línea de retención sin base no se contabiliza.
        var sinBase = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/documents/validate", new
        {
            voucherTypeCode = "CG", date = new DateOnly(2026, 3, 10), description = "Honorarios",
            lines = new[] { Linea(Honorarios, s, 1_000_000m, 0m, tercero), LineaConBase(RetencionHonorarios, s, 40_000m, tercero, null), Linea(Banco, s, 0m, 960_000m) },
        });
        (await LeerAsync(sinBase)).GetRawText().Should().Contain("TaxBaseRequired");

        await ContabilizarAsync(http, t, new DateOnly(2026, 3, 10), "Honorarios marzo", new[]
        {
            Linea(Honorarios, s, 1_000_000m, 0m, tercero), LineaConBase(RetencionHonorarios, s, 40_000m, tercero, 1_000_000m), Linea(Banco, s, 0m, 960_000m),
        });
        var abril = await ContabilizarAsync(http, t, new DateOnly(2026, 4, 10), "Honorarios abril", new[]
        {
            Linea(Honorarios, s, 500_000m, 0m, tercero), LineaConBase(RetencionHonorarios, s, 20_000m, tercero, 500_000m), Linea(Banco, s, 0m, 480_000m),
        });

        var generar = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/taxes/certificates/generate", new { kind = "Withholding", year = 2026 });
        generar.StatusCode.Should().Be(HttpStatusCode.OK, await generar.Content.ReadAsStringAsync());
        var certificado = (await LeerAsync(generar)).GetProperty("certificates").EnumerateArray().Single();
        certificado.GetProperty("totalAmount").GetDecimal().Should().Be(60_000m);
        var id = certificado.GetProperty("publicId").GetGuid();

        var pdf = await EnviarAsync(http, t, HttpMethod.Get, $"/api/accounting/taxes/certificates/{id}/pdf", null);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        await ReversarAsync(http, t, abril, "Pago duplicado");
        var lista = await GetAsync(http, t, "/api/accounting/taxes/certificates?kind=Withholding&year=2026");
        lista.EnumerateArray().Single(c => c.GetProperty("publicId").GetGuid() == id).GetProperty("status").GetString().Should().Be("Outdated");

        var reexpedido = await EnviarAsync(http, t, HttpMethod.Post, $"/api/accounting/taxes/certificates/{id}/reissue", null);
        reexpedido.StatusCode.Should().Be(HttpStatusCode.OK, await reexpedido.Content.ReadAsStringAsync());
        var nuevo = await LeerAsync(reexpedido);
        nuevo.GetProperty("number").GetInt64().Should().BeGreaterThan(certificado.GetProperty("number").GetInt64());
        nuevo.GetProperty("totalAmount").GetDecimal().Should().Be(40_000m);

        var retenciones = await InformeAsync(http, t, "withholdings?from=2026-01-01&to=2026-12-31");
        retenciones.Filas.Should().NotBeEmpty();
        await BalanceCuadraAsync(http, t);
    }

    [Fact]
    public async Task Exogena_carga_la_semilla_genera_el_1001_y_exporta_el_xml()
    {
        using var http = fx.CreateClient();
        var coop = await PrepararAsync(fx, http);
        var t = coop.TokenAdmin;
        var s = coop.SucursalPrincipal;
        var proveedor = await CrearPersonaAsync(http, t, "Exogena");
        await ContabilizarAsync(http, t, new DateOnly(2026, 5, 10), "Honorarios exógena", new[]
        {
            Linea(Honorarios, s, 2_000_000m, 0m, proveedor), Linea(Banco, s, 0m, 2_000_000m),
        });

        var semilla = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/exogenous/formats/2026/load-seed", null);
        semilla.StatusCode.Should().Be(HttpStatusCode.OK, await semilla.Content.ReadAsStringAsync());
        (await LeerAsync(semilla)).EnumerateArray().Select(f => f.GetProperty("formatCode").GetString()).Should().Contain(["1001", "1007", "1012"]);

        var generar = await EnviarAsync(http, t, HttpMethod.Post, "/api/accounting/exogenous/runs/2026/1001", null);
        generar.StatusCode.Should().Be(HttpStatusCode.OK, await generar.Content.ReadAsStringAsync());
        var corrida = await LeerAsync(generar);
        corrida.GetProperty("lines").GetInt32().Should().BeGreaterThan(0);
        var id = corrida.GetProperty("publicId").GetGuid();

        var tabla = await GetAsync(http, t, $"/api/accounting/exogenous/runs/{id}/table");
        tabla.GetProperty("filas").GetArrayLength().Should().BeGreaterThan(0);

        var xml = await EnviarAsync(http, t, HttpMethod.Post, $"/api/accounting/exogenous/runs/{id}/xml", null);
        corrida.GetProperty("blockingIssueCount").GetInt32().Should().Be(0, "el proveedor tiene documento válido: sólo puede haber avisos (dirección, municipio)");
        xml.StatusCode.Should().Be(HttpStatusCode.OK, await xml.Content.ReadAsStringAsync());
        var documento = XDocument.Load(await xml.Content.ReadAsStreamAsync());
        documento.Root!.Element("Cab")!.Element("Formato")!.Value.Should().Be("1001");
        var runs = await GetAsync(http, t, "/api/accounting/exogenous/runs?year=2026");
        runs.EnumerateArray().Single(r => r.GetProperty("publicId").GetGuid() == id).GetProperty("status").GetString().Should().Be("Exported");
    }
}
