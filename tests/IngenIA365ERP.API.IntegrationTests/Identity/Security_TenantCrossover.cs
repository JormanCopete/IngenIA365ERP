using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Seguridad — aislamiento entre tenants (crossover):
/// un admin con sesión operativa del tenant A NO puede operar recursos del
/// tenant B ni cambiarse a un tenant donde no tiene membresía.
/// <list type="bullet">
///   <item><c>GET /api/tenants/{B}/members</c> con token de A →
///         403 <c>Membership.Forbidden</c> (guard <c>Authz.EnsureTenantAdminOrMasterAsync</c>).</item>
///   <item><c>POST /api/sessions/switch-tenant</c> hacia B sin membresía →
///         422 <c>Membership.NotActive</c>.</item>
///   <item>Acciones de mutación sobre miembros de B (suspend) con token de A →
///         403 <c>Membership.Forbidden</c>.</item>
///   <item>Sin token → 401 del middleware JwtBearer.</item>
/// </list>
/// </summary>
public sealed class Security_TenantCrossover(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Token_operativo_del_tenant_A_es_rechazado_sobre_recursos_del_tenant_B()
    {
        using var http = fx.CreateClient();

        // ---- Arrange: master crea dos tenants con sus admins invitados ----
        var masterToken = await LoginMasterAsync(http);

        var (tenantAId, adminAToken, _) = await RegisterTenantWithAdminAsync(
            http, masterToken,
            name: "Coop Crossover A", schema: "tenant_crossover_a", nit: "900111001",
            adminEmail: "admin.a@crossover.integration.test",
            adminPassword: "AdminA-Strong-Pwd-2026");

        var (tenantBId, _, adminBMembershipId) = await RegisterTenantWithAdminAsync(
            http, masterToken,
            name: "Coop Crossover B", schema: "tenant_crossover_b", nit: "900111002",
            adminEmail: "admin.b@crossover.integration.test",
            adminPassword: "AdminB-Strong-Pwd-2026");

        Assert.NotEqual(tenantAId, tenantBId);

        // ---- 1) Listar miembros del tenant B con token operativo de A → 403 ----
        using (var req = Authorized(HttpMethod.Get, $"/api/tenants/{tenantBId}/members", adminAToken))
        {
            var resp = await http.SendAsync(req);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            var body = await ReadJsonAsync(resp);
            Assert.Equal("Membership.Forbidden", body.GetProperty("code").GetString());
        }

        // ---- 2) Switch a un tenant sin membresía → 422 Membership.NotActive ----
        using (var req = Authorized(HttpMethod.Post, "/api/sessions/switch-tenant", adminAToken))
        {
            req.Content = JsonContent.Create(new { tenantPublicId = tenantBId });
            var resp = await http.SendAsync(req);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
            var body = await ReadJsonAsync(resp);
            Assert.Equal("Membership.NotActive", body.GetProperty("code").GetString());
        }

        // ---- 3) Mutación (suspender) sobre un miembro real de B con token de A → 403 ----
        using (var req = Authorized(HttpMethod.Post,
                   $"/api/tenants/{tenantBId}/members/{adminBMembershipId}/suspend", adminAToken))
        {
            var resp = await http.SendAsync(req);
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            var body = await ReadJsonAsync(resp);
            Assert.Equal("Membership.Forbidden", body.GetProperty("code").GetString());
        }

        // ---- 4) Sin token → 401 del middleware (RequireAuthorization) ----
        var anonResp = await http.GetAsync($"/api/tenants/{tenantBId}/members");
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // ---- Control positivo: el token de A SÍ lista los miembros de A ----
        using (var req = Authorized(HttpMethod.Get, $"/api/tenants/{tenantAId}/members", adminAToken))
        {
            var resp = await http.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await ReadJsonAsync(resp);
            Assert.True(body.GetProperty("total").GetInt32() >= 1);
        }
    }

    // -------- Helpers --------

    /// <summary>Login del master sembrado por la fixture (sin membresías → challenge None).</summary>
    private static async Task<string> LoginMasterAsync(HttpClient http)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = CentralIdentityApiFixture.MasterPassword,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var login = await ReadJsonAsync(resp);
        var token = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    /// <summary>
    /// Registra un tenant vía <c>POST /api/saas/tenants/with-admin</c>, extrae el
    /// token de invitación del correo capturado y lo acepta (rama registro).
    /// Devuelve el PublicId del tenant, el access token operativo del admin y el
    /// PublicId de su membresía (para acciones de mutación de miembros).
    /// </summary>
    private async Task<(Guid TenantId, string AdminAccessToken, Guid AdminMembershipId)>
        RegisterTenantWithAdminAsync(
            HttpClient http, string masterToken,
            string name, string schema, string nit,
            string adminEmail, string adminPassword)
    {
        using var registerReq = Authorized(HttpMethod.Post, "/api/saas/tenants/with-admin", masterToken);
        registerReq.Content = JsonContent.Create(new
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
        });
        var registerResp = await http.SendAsync(registerReq);
        Assert.True(registerResp.StatusCode == HttpStatusCode.OK,
            $"register → {(int)registerResp.StatusCode}: {await registerResp.Content.ReadAsStringAsync()}");
        var register = await ReadJsonAsync(registerResp);
        var tenantId = register.GetProperty("tenantPublicId").GetGuid();

        // Token plano de la invitación desde el correo capturado.
        var email = fx.Emails.Sent.LastOrDefault(m => m.To == adminEmail);
        Assert.NotNull(email);
        var match = Regex.Match(email!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, $"El correo de invitación a {adminEmail} no contiene token=");

        // Accept rama registro — tenant nuevo sin política MFA → challenge None.
        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = adminPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        var accessToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // PublicId de la membresía del admin, vía listado del propio tenant.
        using var listReq = Authorized(HttpMethod.Get, $"/api/tenants/{tenantId}/members", accessToken!);
        var listResp = await http.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await ReadJsonAsync(listResp);
        var membershipId = list.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("email").GetString() == adminEmail)
            .GetProperty("membershipPublicId").GetGuid();

        return (tenantId, accessToken!, membershipId);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new("Bearer", token);
        return req;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
