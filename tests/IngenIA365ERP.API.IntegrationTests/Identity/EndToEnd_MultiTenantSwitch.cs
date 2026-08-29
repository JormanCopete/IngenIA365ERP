using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Flujo multi-tenant de sesión (US-Sesiones, Feature 002):
/// el master registra DOS cooperativas invitando al MISMO admin →
/// el usuario acepta ambas invitaciones (registro + credenciales existentes) →
/// login con 2 membresías devuelve <c>challenge=TenantSelection</c> (sin tokens
/// operativos) → <c>POST /api/sessions/select-tenant</c> con el challengeToken
/// (purpose=tenant-select) emite tokens full para A → <c>POST
/// /api/sessions/switch-tenant</c> re-emite tokens con
/// <c>active_tenant_id</c> = B sin re-login.
/// </summary>
public sealed class EndToEnd_MultiTenantSwitch(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Usuario_con_dos_membresias_selecciona_tenant_A_y_cambia_a_tenant_B()
    {
        using var http = fx.CreateClient();

        const string userEmail = "carlos.multi@coop.integracion.test";
        const string userPassword = "Carlos-Strong-Pwd-2026";

        // 0) Login del master (sin membresías → sesión operativa, challenge None).
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 1) Registrar tenant A + invitación admin al usuario.
        var tenantAId = await RegisterTenantAsync(
            http, masterToken!, "Coop. Multi A", "tenant_multi_a", "900111222", userEmail);

        // 2) Aceptar invitación A por la rama REGISTRO (crea la identidad central).
        var tokenA = ExtractInvitationToken(userEmail);
        var acceptAResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenA,
            registration = new { password = userPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptAResp.StatusCode);
        var acceptA = await ReadJsonAsync(acceptAResp);
        Assert.Equal(tenantAId, acceptA.GetProperty("activeTenantPublicId").GetGuid());

        // 3) Registrar tenant B invitando al MISMO email.
        var tenantBId = await RegisterTenantAsync(
            http, masterToken!, "Coop. Multi B", "tenant_multi_b", "900333444", userEmail);
        Assert.NotEqual(tenantAId, tenantBId);

        // 4) Aceptar invitación B por la rama CREDENCIALES EXISTENTES
        //    (la identidad central ya existe → segunda membresía Active).
        var tokenB = ExtractInvitationToken(userEmail);
        Assert.NotEqual(tokenA, tokenB);
        var acceptBResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenB,
            existingCredentials = new { password = userPassword },
        });
        Assert.True(acceptBResp.StatusCode == HttpStatusCode.OK,
            $"accept B → {(int)acceptBResp.StatusCode}: {await acceptBResp.Content.ReadAsStringAsync()}");
        var acceptB = await ReadJsonAsync(acceptBResp);
        Assert.Equal(tenantBId, acceptB.GetProperty("activeTenantPublicId").GetGuid());

        // 5) Login del usuario: con 2 membresías y sin default → TenantSelection.
        //    NO debe traer tokens operativos, solo el challengeToken tenant-select
        //    y el listado de empresas activas.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = userEmail,
            password = userPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        Assert.Equal("TenantSelection", login.GetProperty("challenge").GetString());
        Assert.Equal("tenant-select", login.GetProperty("challengeTokenPurpose").GetString());
        AssertNullOrMissing(login, "accessToken");
        var challengeToken = login.GetProperty("challengeToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(challengeToken));

        var offered = login.GetProperty("activeTenants").EnumerateArray()
            .Select(t => t.GetProperty("tenantPublicId").GetGuid())
            .ToList();
        Assert.Equal(2, offered.Count);
        Assert.Contains(tenantAId, offered);
        Assert.Contains(tenantBId, offered);

        // 6) select-tenant A con el challengeToken en Bearer → tokens operativos.
        var select = await PostSessionAsync(
            http, "/api/sessions/select-tenant", challengeToken!, tenantAId);
        Assert.Equal(tenantAId, select.GetProperty("tenant").GetProperty("tenantPublicId").GetGuid());
        var accessA = select.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessA));

        // Claims del token de A: purpose=full + active_tenant_id = A.
        var claimsA = DecodeJwtPayload(accessA!);
        Assert.Equal("full", claimsA.GetProperty("purpose").GetString());
        Assert.Equal(tenantAId.ToString(), claimsA.GetProperty("active_tenant_id").GetString());

        // 7) switch-tenant a B con el token full de A → nuevos tokens para B.
        var @switch = await PostSessionAsync(
            http, "/api/sessions/switch-tenant", accessA!, tenantBId);
        var switchedTenant = @switch.GetProperty("tenant");
        Assert.Equal(tenantBId, switchedTenant.GetProperty("tenantPublicId").GetGuid());
        Assert.True(switchedTenant.GetProperty("isTenantAdmin").GetBoolean());
        var accessB = @switch.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessB));
        Assert.NotEqual(accessA, accessB);
        Assert.False(string.IsNullOrWhiteSpace(@switch.GetProperty("refreshToken").GetString()));

        // 8) El nuevo access token trae los claims del tenant B (objetivo del test).
        var claimsB = DecodeJwtPayload(accessB!);
        Assert.Equal("full", claimsB.GetProperty("purpose").GetString());
        Assert.Equal(tenantBId.ToString(), claimsB.GetProperty("active_tenant_id").GetString());
    }

    // -------------------- Helpers --------------------

    /// <summary>Registra un tenant con primera invitación admin y devuelve su PublicId.</summary>
    private static async Task<Guid> RegisterTenantAsync(
        HttpClient http, string masterToken, string name, string schemaName,
        string nit, string firstAdminEmail)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name,
                schemaName,
                // UK_ADM_Tenants_Subdomain: dos NULL chocan en SQL Server — usar único.
                subdomain = schemaName,
                nit,
                legalName = $"{name} SAS",
                contactEmail = $"contacto@{schemaName}.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail,
            }),
        };
        req.Headers.Authorization = new("Bearer", masterToken);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await ReadJsonAsync(resp);
        return body.GetProperty("tenantPublicId").GetGuid();
    }

    /// <summary>Extrae el token plano del ÚLTIMO correo de invitación capturado.</summary>
    private string ExtractInvitationToken(string email)
    {
        var message = fx.Emails.Sent.LastOrDefault(m => m.To == email);
        Assert.NotNull(message);
        var match = Regex.Match(message!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, "El correo de invitación no contiene token=");
        return match.Groups[1].Value;
    }

    /// <summary>POST a un endpoint de sesión con Bearer + body {tenantPublicId}.</summary>
    private static async Task<JsonElement> PostSessionAsync(
        HttpClient http, string route, string bearerToken, Guid tenantPublicId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { tenantPublicId }),
        };
        req.Headers.Authorization = new("Bearer", bearerToken);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await ReadJsonAsync(resp);
    }

    private static void AssertNullOrMissing(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value))
        {
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonSerializer.Deserialize<JsonElement>(json, Json);
    }
}
