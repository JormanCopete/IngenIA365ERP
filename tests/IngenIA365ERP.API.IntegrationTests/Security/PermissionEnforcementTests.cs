using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// FR-017 / SC-005 — a quien le falta el permiso, el endpoint le responde como
/// si no existiera.
///
/// <para>
/// <b>Qué cambió y por qué.</b> Esta prueba pedía 404 <i>sin enviar token</i> y
/// recibía 401, porque <c>RequireAuthorization</c> corta antes de que el filtro
/// de permisos llegue a ejecutarse. La expectativa era la equivocada: FR-017 y
/// SC-005 hablan del usuario que <b>tiene sesión</b> y no tiene el permiso, no
/// del anónimo; la constitución no dice nada del caso; y dos pruebas que ya
/// estaban en verde —<c>Security_TenantAdminScope</c> y
/// <c>Security_TenantCrossover</c>— asumen 401 para el anónimo. Lo único que
/// pedía 404 era la descripción de una tarea, que no es un requisito.
/// </para>
///
/// <para>
/// La otra mitad tampoco medía nada: mandaba el literal
/// <c>"invalid-but-formatted.token.here"</c> como bearer. Eso no es un token sin
/// permiso, es un token sin firma, así que moría en la autenticación y el 404
/// que la prueba celebraba nunca vino del filtro de permisos. Ahora el token es
/// real, emitido por el flujo de invitación.
/// </para>
/// </summary>
public class PermissionEnforcementTests(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Un permiso SaaS-global: opera SOBRE las cooperativas, no dentro de una, y
    // BuiltInRolesSeeder lo deja explicitamente fuera de TODOS los roles —
    // incluido CompanyAdmin—. Solo lo ejerce el administrador maestro, por el
    // atajo del filtro. Es el unico caso limpio de «tiene sesion y no tiene el
    // permiso»: cualquier permiso de cooperativa lo tiene su administrador.
    //
    // Antes esta prueba usaba /api/admin/users y pasaba, pero por el motivo
    // equivocado: el alta por API dejaba la cooperativa sin aprovisionar, asi
    // que su administrador no tenia NINGUN permiso. Arreglado eso, la prueba
    // habria empezado a devolver 200 — y con ella se habria ido la unica
    // comprobacion del 404 indistinguible.
    private const string RutaProtegida = "/api/saas/tenants/";
    private const string RutaInexistente = "/api/esto/no/existe";

    [Fact]
    public async Task Sin_token_el_endpoint_protegido_responde_401()
    {
        // La autenticación va antes que la autorización. No es una fuga: para
        // saber que la ruta existe basta con no mandar credenciales, y eso es
        // cierto en cualquier API que distinga 401 de 404.
        using var http = fx.CreateClient();

        var resp = await http.GetAsync(RutaProtegida);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Con_sesion_pero_sin_el_permiso_la_respuesta_es_indistinguible_de_una_ruta_inexistente()
    {
        using var http = fx.CreateClient();

        var masterToken = await LoginAsync(
            http, CentralIdentityApiFixture.MasterEmail, CentralIdentityApiFixture.MasterPassword);

        await RegistrarCooperativaAsync(http, masterToken,
            nombre: "Coop. Permisos Test", esquema: "tenant_permisos",
            nit: "900555666", correoAdmin: "admin@coop.permisos.test");

        var tokenSinPermiso = await AceptarInvitacionAsync(
            http, "admin@coop.permisos.test", "AdminPermisos-Pwd-2026");

        var protegida = await ConTokenAsync(http, RutaProtegida, tokenSinPermiso);
        var inexistente = await ConTokenAsync(http, RutaInexistente, tokenSinPermiso);

        // El invariante entero: mismo status y mismo cuerpo, byte a byte en lo
        // que el cliente puede leer. Si difirieran, la existencia del endpoint
        // se filtraría por comparación.
        Assert.Equal(HttpStatusCode.NotFound, protegida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        var cuerpoProtegida = await LeerJsonAsync(protegida);
        var cuerpoInexistente = await LeerJsonAsync(inexistente);

        Assert.Equal("Generic.NotFound", cuerpoProtegida.GetProperty("code").GetString());
        Assert.Equal(
            cuerpoInexistente.GetProperty("code").GetString(),
            cuerpoProtegida.GetProperty("code").GetString());
        Assert.Equal(
            cuerpoInexistente.GetProperty("message").GetString(),
            cuerpoProtegida.GetProperty("message").GetString());
    }

    // === Ayudantes (mismo patrón que Security_TenantAdminScope) ===

    private static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var login = await LeerJsonAsync(resp);
        var token = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private async Task RegistrarCooperativaAsync(
        HttpClient http, string masterToken,
        string nombre, string esquema, string nit, string correoAdmin)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = nombre,
                schemaName = esquema,
                // UK_ADM_Tenants_Subdomain: dos NULL chocan en SQL Server.
                subdomain = esquema,
                nit,
                legalName = $"{nombre} SAS",
                contactEmail = $"contacto@{esquema}.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = correoAdmin,
            }),
        };
        req.Headers.Authorization = new("Bearer", masterToken);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private async Task<string> AceptarInvitacionAsync(
        HttpClient http, string correoAdmin, string password)
    {
        var correo = fx.Emails.Sent.LastOrDefault(m => m.To == correoAdmin);
        Assert.NotNull(correo);
        var token = Regex.Match(correo!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(token.Success, "El correo de invitación no contiene token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = token.Groups[1].Value,
            registration = new { password },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var accept = await LeerJsonAsync(resp);
        var accessToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        return accessToken!;
    }

    private static async Task<HttpResponseMessage> ConTokenAsync(
        HttpClient http, string url, string bearer)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new("Bearer", bearer);
        return await http.SendAsync(req);
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
