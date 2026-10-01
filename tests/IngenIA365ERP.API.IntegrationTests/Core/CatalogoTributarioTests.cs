using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Core;

/// <summary>
/// T119 (T22; contracts/api.md §30; contracts/plantillas.md §1), en el motor de <c>DB_PROVIDER</c> (se corre en los dos):
/// la semilla <c>TaxCatalogSeeder</c> deja las tarifas «pendientes de validar»; alta de impuesto, tarifa y concepto por
/// HTTP con <c>Idempotency-Key</c>; una vigencia cruzada es 422 <c>Core.TaxRate.Overlaps</c>; la plantilla con datos se
/// revisa y se aplica sin cambios («sin cambio» en todas las filas); y con el token de lectura, escribir responde el
/// mismo 404 que algo inexistente.
///
/// <para>
/// <b>Escrita, no corrida</b> hasta que exista el par <c>PlataformaParaInventario</c> (T186), que crea
/// <c>COR_TaxDefinitions</c>, <c>COR_TaxRates</c> y <c>COR_WithholdingConcepts</c>. Corre en una cooperativa aislada
/// («tributario») porque cambia el catálogo de la cooperativa.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CatalogoTributarioTests(CentralIdentityApiFixture fx)
{
    private const string RutaImpuestos = "/api/core/taxes";
    private const string RutaTarifas = "/api/core/tax-rates";
    private const string RutaConceptos = "/api/core/withholding-concepts";

    [Fact]
    public async Task La_semilla_deja_las_tarifas_pendientes_de_validar_con_su_norma()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tributario");
        using var http = fx.CreateClient();

        var tarifas = (await InventarioE2E.GetAsync(http, coop.TokenAdmin, RutaTarifas)).EnumerateArray().ToList();

        tarifas.Should().NotBeEmpty("la semilla deja el catálogo inicial");
        tarifas.Should().OnlyContain(t => t.GetProperty("reviewPending").GetBoolean() || !EsSembrada(t));
        tarifas.Select(t => t.GetProperty("code").GetString()).Should().Contain(["IVA19", "IVA5", "BOLSA", "RFCOMP25", "RIVA15"]);
        tarifas.Should().OnlyContain(t => t.GetProperty("legalSource").GetString()!.Length > 0);
        tarifas.Single(t => t.GetProperty("code").GetString() == "IVA19").GetProperty("rate").GetDecimal().Should().Be(0.19m, "la tarifa viaja como fracción");

        var pendientes = (await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{RutaTarifas}?reviewPending=true")).EnumerateArray().Count();
        pendientes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Alta_de_impuesto_concepto_y_tarifa_con_clave_y_la_vigencia_cruzada_es_422()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tributario");
        using var http = fx.CreateClient();

        var impuesto = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, RutaImpuestos, new
        {
            code = "IBUA", name = "Impuesto a las bebidas ultraprocesadas", kind = "Other", calculationForm = "AmountPerUnit",
            isWithholding = false, dianTaxCode = "ZZ", reason = "Alta de prueba",
        });
        impuesto.StatusCode.Should().Be(HttpStatusCode.Created, await impuesto.Content.ReadAsStringAsync());
        var impuestoId = (await InventarioE2E.LeerAsync(impuesto)).GetProperty("taxPublicId").GetGuid();

        var concepto = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, RutaConceptos, new { code = "COMISION", name = "Comisiones", reason = "Alta" });
        concepto.StatusCode.Should().Be(HttpStatusCode.Created, await concepto.Content.ReadAsStringAsync());

        var tarifa = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, RutaTarifas, new
        {
            taxPublicId = impuestoId, code = "IBUA100", name = "IBUA por 100 ml", amountPerUnit = 28m, appliesTo = "Sales",
            priority = 0, validFrom = "2026-01-01", legalSource = "Ley 2277 de 2022, art. 54", reason = "Alta",
        });
        tarifa.StatusCode.Should().Be(HttpStatusCode.Created, await tarifa.Content.ReadAsStringAsync());
        var tarifaId = (await InventarioE2E.LeerAsync(tarifa)).GetProperty("taxRatePublicId").GetGuid();
        (await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{RutaTarifas}/{tarifaId}")).GetProperty("reviewPending").GetBoolean()
            .Should().BeFalse("lo que crea una persona no queda pendiente de validar");

        var cruzada = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, RutaTarifas, new
        {
            taxPublicId = impuestoId, code = "IBUA100", name = "IBUA por 100 ml", amountPerUnit = 35m, appliesTo = "Sales",
            priority = 0, validFrom = "2027-01-01", legalSource = "Ley 2277 de 2022, art. 54", reason = "Vigencia nueva sin cerrar la anterior",
        });
        cruzada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(cruzada)).Should().Be("Core.TaxRate.Overlaps");

        var sinClave = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{RutaTarifas}/{tarifaId}/review", new { reason = "Revisada" });
        sinClave.StatusCode.Should().Be(HttpStatusCode.BadRequest, "toda escritura exige Idempotency-Key");
    }

    [Fact]
    public async Task La_plantilla_con_datos_se_revisa_y_se_aplica_sin_cambios()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tributario");
        using var http = fx.CreateClient();

        var descarga = new HttpRequestMessage(HttpMethod.Get, $"{RutaImpuestos}/template.xlsx?withData=true");
        descarga.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
        var libro = await http.SendAsync(descarga);
        libro.StatusCode.Should().Be(HttpStatusCode.OK, await libro.Content.ReadAsStringAsync());
        var contenido = await libro.Content.ReadAsByteArrayAsync();
        contenido.Should().NotBeEmpty();

        var revision = await ImportarAsync(http, coop.TokenAdmin, contenido, "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var cuerpo = await InventarioE2E.LeerAsync(revision);
        cuerpo.GetProperty("valid").GetBoolean().Should().BeTrue(cuerpo.GetRawText());
        cuerpo.GetProperty("requiresReason").GetBoolean().Should().BeFalse();
        foreach (var hoja in cuerpo.GetProperty("sheets").EnumerateArray())
        {
            hoja.GetProperty("created").GetInt32().Should().Be(0, hoja.GetRawText());
            hoja.GetProperty("updated").GetInt32().Should().Be(0, hoja.GetRawText());
            hoja.GetProperty("unchanged").GetInt32().Should().Be(hoja.GetProperty("rows").GetInt32(), "«sin cambio» en todas las filas");
        }

        var aplicacion = await ImportarAsync(http, coop.TokenAdmin, contenido, "apply");
        aplicacion.StatusCode.Should().Be(HttpStatusCode.OK, await aplicacion.Content.ReadAsStringAsync());
        (await InventarioE2E.LeerAsync(aplicacion)).GetProperty("applied").GetBoolean().Should().BeTrue();

        var sinModo = await ImportarAsync(http, coop.TokenAdmin, contenido, null);
        sinModo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.CodigoDeErrorAsync(sinModo)).Should().Be("Import.ModeRequired");
    }

    [Fact]
    public async Task Con_el_token_de_lectura_escribir_responde_el_mismo_404_que_lo_inexistente()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tributario");
        using var http = fx.CreateClient();
        var lector = await OperadorAsync(http, coop);

        (await InventarioE2E.EnviarAsync(http, lector, HttpMethod.Get, RutaImpuestos, null)).StatusCode.Should().Be(HttpStatusCode.OK, "Operador lee (*.View)");

        var alta = await EnviarConClaveAsync(http, lector, HttpMethod.Post, RutaImpuestos, new
        {
            code = "NOP", name = "No", kind = "Iva", calculationForm = "PercentOfBase", dianTaxCode = "01", reason = "x",
        });
        alta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var inexistente = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Put, $"{RutaImpuestos}/{Guid.NewGuid()}", new { name = "x", isActive = true, reason = "x" });
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await InventarioE2E.CodigoDeErrorAsync(alta)).Should().Be(await InventarioE2E.CodigoDeErrorAsync(
            await EnviarConClaveAsync(http, lector, HttpMethod.Post, $"{RutaTarifas}/{Guid.NewGuid()}/review", new { reason = "x" })),
            "sin permiso es indistinguible de inexistente");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static bool EsSembrada(JsonElement t) =>
        t.GetProperty("code").GetString() is "IVA19" or "IVA5" or "IVAEXE" or "IVAEXC" or "INC8" or "BOLSA"
            or "RFCOMP25" or "RFCOMP35" or "RFSERV4" or "RFSERV6" or "RFHON11" or "RFHON10" or "RFARR35" or "RFTRA1" or "RIVA15";

    private static Task<HttpResponseMessage> EnviarConClaveAsync(HttpClient http, string token, HttpMethod metodo, string url, object cuerpo)
    {
        var peticion = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, byte[] contenido, string? modo)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "archivo", "impuestos.xlsx");
        var peticion = new HttpRequestMessage(HttpMethod.Post, modo is null ? $"{RutaImpuestos}/import" : $"{RutaImpuestos}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    /// <summary>Un usuario con el rol integrado Operador (sólo <c>*.View</c>) en la cooperativa aislada.</summary>
    private async Task<string> OperadorAsync(HttpClient http, InventarioE2E.CooperativaAislada coop)
    {
        const string correo = "operador.tributario@coop.inventario.test";
        var invitacion = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", new { email = correo });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull();
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        var aceptar = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = "Operador-Tributario-2026!" } });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());
        var acceso = (await InventarioE2E.LeerAsync(aceptar)).GetProperty("accessToken").GetString()!;

        var roles = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/roles?includeBuiltIn=true&pageSize=50");
        var rol = roles.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "Operator").GetProperty("publicId").GetGuid();
        var usuarios = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"/api/admin/users?search={Uri.EscapeDataString(correo)}&pageSize=50");
        var usuario = usuarios.GetProperty("items").EnumerateArray()
            .Single(u => string.Equals(u.GetProperty("email").GetString(), correo, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
        var asignacion = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
        asignacion.IsSuccessStatusCode.Should().BeTrue(await asignacion.Content.ReadAsStringAsync());
        return acceso;
    }
}
