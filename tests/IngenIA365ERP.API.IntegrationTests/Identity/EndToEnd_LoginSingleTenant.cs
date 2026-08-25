using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Login con exactamente UNA membresía activa (rama autoSelected del
/// <c>LoginCommandHandler</c>): tras aceptar la invitación de su único tenant,
/// el usuario hace <c>POST /api/auth/login</c> y recibe directamente una sesión
/// operativa — <c>challenge=None</c>, <c>autoSelected=true</c> — sin pasar por
/// tenant-select. El access token JWT debe llevar el claim
/// <c>active_tenant_id</c> apuntando al tenant correcto.
/// </summary>
public sealed class EndToEnd_LoginSingleTenant(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_con_una_sola_membresia_autoselecciona_y_el_jwt_lleva_active_tenant_id()
    {
        using var http = fx.CreateClient();

        // 1) Login del master para poder registrar la cooperativa.
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 2) Registrar el ÚNICO tenant del usuario bajo prueba (con invitación admin).
        const string userEmail = "camila.unica@coop.singletenant.test";
        const string userPassword = "Camila-Unica-Pwd-2026!";
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Single Tenant Test",
                schemaName = "tenant_single_login",
                nit = "900777888",
                legalName = "Coop. Single Tenant SAS",
                contactEmail = "contacto@coop.singletenant.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = userEmail,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var register = await ReadJsonAsync(registerResp);
        var tenantPublicId = register.GetProperty("tenantPublicId").GetGuid();
        Assert.NotEqual(Guid.Empty, tenantPublicId);

        // 3) Extraer el token plano del correo de invitación capturado.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == userEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");

        // 4) Accept rama registro → el usuario queda con exactamente 1 membresía Active.
        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenMatch.Groups[1].Value,
            registration = new { password = userPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // 5) Login normal del usuario con 1 sola membresía → autoSelected
        //    (caso "1 membership" de la state machine del login).
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = userEmail,
            password = userPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);

        // 6) Sesión operativa directa: challenge None + autoSelected=true,
        //    con el tenant activo resuelto al único disponible.
        Assert.Equal("None", login.GetProperty("challenge").GetString());
        Assert.True(login.GetProperty("autoSelected").GetBoolean());
        Assert.Equal(tenantPublicId, login.GetProperty("activeTenantPublicId").GetGuid());
        var accessToken = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("refreshToken").GetString()));

        // 7) El payload del JWT (base64url) lleva active_tenant_id del tenant
        //    correcto y purpose=full (token operativo, no challenge).
        var claims = DecodeJwtPayload(accessToken!);
        Assert.Equal(tenantPublicId.ToString(),
            claims.GetProperty("active_tenant_id").GetString());
        Assert.Equal("full", claims.GetProperty("purpose").GetString());
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
