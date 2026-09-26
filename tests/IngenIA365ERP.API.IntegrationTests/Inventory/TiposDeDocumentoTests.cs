using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T120 (FR-036 a FR-038; contracts/api.md §8), en el motor de <c>DB_PROVIDER</c>: la semilla
/// <c>InventoryDocumentTypesSeeder</c> deja un tipo activo por clase de I1 con su consecutivo (y el de
/// <c>OpeningBalance</c> con su política de un nivel); <c>GET /classes</c> marca <c>operable</c>; se crea un tipo con
/// prefijo y primer número, se cambia de prefijo con <c>POST /{id}/sequences</c> y se ve el historial; sin permiso, las
/// rutas responden el mismo 404 que lo inexistente.
///
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class TiposDeDocumentoTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/inventory/document-types";

    private static readonly string[] ClasesDeI1 =
    [
        "PurchaseReceipt", "SupplierInvoice", "SupplierNote", "SupplierReturn", "PositiveAdjustment", "NegativeAdjustment",
        "InternalConsumption", "WriteOff", "OpeningBalance", "TransferDispatch", "TransferReceipt", "LocationMove",
        "PhysicalCount", "CostAdjustment", "Voiding",
    ];

    /// <summary>El número de cada clase (los enums salen como número).</summary>
    private static int Numero(string clase) => (int)Enum.Parse<Domain.Enums.Inventory.DocumentClass>(clase);

    [Fact]
    public async Task La_semilla_deja_un_tipo_por_clase_de_I1_con_su_consecutivo_y_la_politica_del_saldo_inicial()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tipos");
        using var http = fx.CreateClient();

        var tipos = (await InventarioE2E.GetAsync(http, coop.TokenAdmin, Ruta)).EnumerateArray().ToList();

        foreach (var clase in ClasesDeI1)
        {
            // US11 (T397): los ajustes de conteo (CONP, CONN) son un segundo tipo de las clases de ajuste. Los tipos que
            // crean las otras pruebas de la clase en la misma cooperativa (AJE) no son de la semilla.
            var delaClase = tipos.Where(t => t.GetProperty("class").GetInt32() == Numero(clase)
                && t.GetProperty("isSeeded").GetBoolean()
                && t.GetProperty("code").GetString() is not ("CONP" or "CONN")).ToList();
            delaClase.Should().ContainSingle($"la semilla deja un tipo de {clase}");
            var tipo = delaClase[0];
            tipo.GetProperty("isSeeded").GetBoolean().Should().BeTrue();
            tipo.GetProperty("isActive").GetBoolean().Should().BeTrue();
            tipo.GetProperty("currentSequence").ValueKind.Should().Be(JsonValueKind.Object, $"{clase} numera con su consecutivo");
            tipo.GetProperty("currentSequence").GetProperty("prefix").GetString().Should().BeEmpty();
        }

        var saldo = tipos.Single(t => t.GetProperty("class").GetInt32() == Numero("OpeningBalance"));
        var niveles = saldo.GetProperty("approvalPolicy").GetProperty("levels").EnumerateArray().ToList();
        niveles.Should().ContainSingle();
        niveles[0].GetProperty("threshold").GetDecimal().Should().Be(0m);
        niveles[0].GetProperty("permissionCode").GetString().Should().Be("Inventory.OpeningBalance.Approve");
    }

    [Fact]
    public async Task Las_clases_dicen_si_son_operables()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tipos");
        using var http = fx.CreateClient();

        var clases = (await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Ruta}/classes")).EnumerateArray().ToList();

        clases.Should().HaveCount(34);
        clases.Single(c => c.GetProperty("class").GetInt32() == Numero("PositiveAdjustment")).GetProperty("operable").GetBoolean().Should().BeTrue();
        clases.Single(c => c.GetProperty("class").GetInt32() == Numero("SalesInvoice")).GetProperty("operable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Crear_un_tipo_cambiar_de_prefijo_y_ver_el_historial()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tipos");
        using var http = fx.CreateClient();

        var alta = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new
        {
            code = "AJE", name = "Ajuste por evento", @class = "PositiveAdjustment",
            requiresCounterparty = false, requiresCostCenter = false, requiresReason = true, requiresExternalReference = false,
            prefix = "AE", firstNumber = 1500, validFrom = "2026-01-01",
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var tipo = await InventarioE2E.LeerAsync(alta);
        var id = tipo.GetProperty("publicId").GetGuid();
        tipo.GetProperty("currentSequence").GetProperty("nextValue").GetInt64().Should().Be(1500);

        var cambio = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Ruta}/{id}/sequences", new
        {
            prefix = "AF", nextValue = 1, validFrom = "2026-12-01", reason = "Talonario nuevo",
        });
        cambio.StatusCode.Should().Be(HttpStatusCode.Created, await cambio.Content.ReadAsStringAsync());

        var detalle = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Ruta}/{id}");
        var historial = detalle.GetProperty("sequences").EnumerateArray().ToList();
        historial.Should().HaveCount(2);
        historial.Single(s => s.GetProperty("prefix").GetString() == "AE").GetProperty("validTo").GetString().Should().Be("2026-11-30");
        historial.Single(s => s.GetProperty("prefix").GetString() == "AF").GetProperty("validFrom").GetString().Should().Be("2026-12-01");

        var cruzado = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Ruta}/{id}/sequences", new
        {
            prefix = "AG", nextValue = 1, validFrom = "2026-06-01", reason = "Se cruza",
        });
        cruzado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(cruzado)).Should().Be("Inventory.Sequence.Overlaps");

        var repetido = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new
        {
            code = "aje", name = "Otro", @class = "PositiveAdjustment", prefix = "X",
        });
        (await InventarioE2E.CodigoDeErrorAsync(repetido)).Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task Sin_permiso_las_rutas_responden_el_mismo_404()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "tipos");
        using var http = fx.CreateClient();
        var operador = await OperadorAsync(http, coop);

        // Operator sólo recibe *.View del comercio (T126): lee los tipos, no los administra.
        (await InventarioE2E.EnviarAsync(http, operador, HttpMethod.Get, Ruta, null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var alta = await EnviarConClaveAsync(http, operador, HttpMethod.Post, Ruta, new { code = "NOP", name = "No", @class = "PositiveAdjustment" });
        alta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var inexistente = await EnviarConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Ruta}/{Guid.NewGuid()}/deactivate", new { reason = "x" });
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnviarConClaveAsync(http, operador, HttpMethod.Post, $"{Ruta}/{Guid.NewGuid()}/deactivate", new { reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "sin permiso es indistinguible de inexistente");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static Task<HttpResponseMessage> EnviarConClaveAsync(HttpClient http, string token, HttpMethod metodo, string url, object cuerpo)
    {
        var peticion = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    /// <summary>Un usuario con el rol integrado Operador en la cooperativa aislada: invitado, acepta y recibe el rol.</summary>
    private async Task<string> OperadorAsync(HttpClient http, InventarioE2E.CooperativaAislada coop)
    {
        const string correo = "operador.tipos@coop.inventario.test";
        var invitacion = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", new { email = correo });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull();
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        var aceptar = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = "Operador-Tipos-2026!" } });
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
