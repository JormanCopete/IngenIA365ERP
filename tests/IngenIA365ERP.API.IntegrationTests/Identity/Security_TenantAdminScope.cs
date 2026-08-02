using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Seguridad — alcance del tenant admin (US4): un administrador de la empresa A
/// NO puede administrar miembros de la empresa B. Los endpoints de
/// <c>MembershipsModule</c> (<c>/api/tenants/{tenantPublicId}/members</c>)
/// delegan la autorización al guard <c>Authz.EnsureTenantAdminOrMasterAsync</c>:
/// sin token → 401 (RequireAuthorization), autenticado sin membresía admin
/// activa en ese tenant → 403 <c>Membership.Forbidden</c>, y sobre el propio
/// tenant → 200.
/// </summary>
public sealed class Security_TenantAdminScope(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Admin_de_tenant_A_no_puede_administrar_miembros_de_tenant_B()
    {
        using var http = fx.CreateClient();

        // 1) Login master y registro de DOS tenants, cada uno con su primer admin.
        var masterToken = await LoginAsync(
            http, CentralIdentityApiFixture.MasterEmail, CentralIdentityApiFixture.MasterPassword);

        var tenantA = await RegisterTenantWithAdminAsync(http, masterToken,
            name: "Coop. Alcance A", schema: "tenant_scope_a", nit: "900111222",
            adminEmail: "admin.a@scope.integracion.test");
        var tenantB = await RegisterTenantWithAdminAsync(http, masterToken,
            name: "Coop. Alcance B", schema: "tenant_scope_b", nit: "900333444",
            adminEmail: "admin.b@scope.integracion.test");

        // 2) El admin de A acepta su invitación (rama registro) → token operativo.
        var adminAToken = await AcceptInvitationAsync(
            http, "admin.a@scope.integracion.test", "AdminA-Strong-Pwd-2026");

        // 3) Sin token: los endpoints exigen autenticación → 401.
        var anonResp = await http.GetAsync($"/api/tenants/{tenantB}/members/");
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // 4) Admin de A lista miembros del tenant B → 403 Membership.Forbidden.
        var crossListResp = await SendAsync(http, HttpMethod.Get,
            $"/api/tenants/{tenantB}/members/", adminAToken);
        Assert.Equal(HttpStatusCode.Forbidden, crossListResp.StatusCode);
        var crossList = await ReadJsonAsync(crossListResp);
        Assert.Equal("Membership.Forbidden", crossList.GetProperty("code").GetString());

        // 5) Acciones de administración cross-tenant: el guard corre ANTES de
        //    resolver la membresía objetivo, así que incluso con un publicId
        //    arbitrario la respuesta es 403 (no 404 — no se filtra existencia).
        foreach (var action in new[] { "suspend", "activate", "revoke", "promote-admin", "demote-admin" })
        {
            var resp = await SendAsync(http, HttpMethod.Post,
                $"/api/tenants/{tenantB}/members/{Guid.NewGuid()}/{action}", adminAToken);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            var body = await ReadJsonAsync(resp);
            Assert.Equal("Membership.Forbidden", body.GetProperty("code").GetString());
        }

        // 6) Sobre SU propio tenant A el mismo token sí administra: la lista
        //    responde 200 y contiene su propia membresía como admin activo.
        var ownListResp = await SendAsync(http, HttpMethod.Get,
            $"/api/tenants/{tenantA}/members/", adminAToken);
        Assert.Equal(HttpStatusCode.OK, ownListResp.StatusCode);
        var ownList = await ReadJsonAsync(ownListResp);
        var items = ownList.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, m =>
            m.GetProperty("email").GetString() == "admin.a@scope.integracion.test"
            && m.GetProperty("isTenantAdmin").GetBoolean());
    }

    // ---------- Helpers ----------

    private static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var login = await ReadJsonAsync(resp);
        var token = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private async Task<Guid> RegisterTenantWithAdminAsync(
        HttpClient http, string masterToken,
        string name, string schema, string nit, string adminEmail)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name,
                schemaName = schema,
                // UK_ADM_Tenants_Subdomain: dos NULL chocan en SQL Server — usar único.
                subdomain = schema,
                nit,
                legalName = $"{name} SAS",
                contactEmail = $"contacto@{schema}.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = adminEmail,
            }),
        };
        req.Headers.Authorization = new("Bearer", masterToken);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await ReadJsonAsync(resp);
        return body.GetProperty("tenantPublicId").GetGuid();
    }

    /// <summary>
    /// Extrae el token plano del correo de invitación capturado y lo acepta por
    /// la rama de registro (usuario nuevo). Devuelve el access token operativo.
    /// </summary>
    private async Task<string> AcceptInvitationAsync(
        HttpClient http, string adminEmail, string password)
    {
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == adminEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenMatch.Groups[1].Value,
            registration = new { password },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var accept = await ReadJsonAsync(resp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        var accessToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        return accessToken!;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpMethod method, string url, string bearerToken)
    {
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new("Bearer", bearerToken);
        return await http.SendAsync(req);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
