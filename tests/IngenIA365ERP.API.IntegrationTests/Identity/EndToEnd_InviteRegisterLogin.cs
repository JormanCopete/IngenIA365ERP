using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Flujo completo de invitación de un usuario regular (no admin) por el master:
/// login master → tenant registrado vía <c>POST /api/saas/tenants/with-admin</c> →
/// <c>POST /api/saas/invitations</c> para un usuario adicional → correo capturado →
/// preview → accept (rama registro nuevo) → login con las credenciales recién
/// creadas (membresía única → auto-selección, challenge None) →
/// <c>GET /api/auth/me</c> confirma la membresía activa sobre el tenant.
/// </summary>
public sealed class EndToEnd_InviteRegisterLogin(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Usuario_invitado_por_master_se_registra_y_entra_con_membresia_activa()
    {
        using var http = fx.CreateClient();

        // 1) Login del master — sin membresías: sesión operativa master (challenge None).
        var loginMasterResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = CentralIdentityApiFixture.MasterPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginMasterResp.StatusCode);
        var loginMaster = await ReadJsonAsync(loginMasterResp);
        Assert.Equal("None", loginMaster.GetProperty("challenge").GetString());
        var masterToken = loginMaster.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 2) El master registra un tenant (necesario para poder invitar sobre él).
        //    La invitación del primer admin va a otro correo y aquí se ignora.
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Invitaciones Test",
                schemaName = "tenant_invitaciones",
                nit = "900777888",
                legalName = "Coop. Invitaciones SAS",
                contactEmail = "contacto@coop.invitaciones.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = "admin.inicial@coop.invitaciones.test",
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var register = await ReadJsonAsync(registerResp);
        var tenantPublicId = register.GetProperty("tenantPublicId").GetGuid();
        Assert.NotEqual(Guid.Empty, tenantPublicId);

        // 3) El master invita a un usuario regular (NO tenant admin) sobre el tenant.
        const string invitedEmail = "carlos.mesa@coop.invitaciones.test";
        using var inviteReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/invitations")
        {
            Content = JsonContent.Create(new
            {
                tenantPublicId,
                email = invitedEmail,
                inviteAsTenantAdmin = false,
            }),
        };
        inviteReq.Headers.Authorization = new("Bearer", masterToken);
        var inviteResp = await http.SendAsync(inviteReq);
        Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);
        var invite = await ReadJsonAsync(inviteResp);
        Assert.NotEqual(Guid.Empty, invite.GetProperty("invitationPublicId").GetGuid());
        Assert.False(invite.GetProperty("inviteAsTenantAdmin").GetBoolean());

        // 4) El correo de invitación fue "enviado" (capturado) con el token plano.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == invitedEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");
        var plainToken = tokenMatch.Groups[1].Value;

        // 5) Preview sin consumir: válida, NO admin, email nuevo (rama registro).
        var previewResp = await http.GetAsync(
            $"/api/invitations/{Uri.EscapeDataString(plainToken)}/preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var preview = await ReadJsonAsync(previewResp);
        Assert.True(preview.GetProperty("isValid").GetBoolean());
        Assert.False(preview.GetProperty("inviteAsTenantAdmin").GetBoolean());
        Assert.False(preview.GetProperty("isExistingCentralUser").GetBoolean());
        Assert.Equal(invitedEmail, preview.GetProperty("email").GetString());

        // 6) Accept rama registro nuevo — crea el CentralUser + membresía activa.
        const string invitedPassword = "Carlos-Strong-Pwd-2026";
        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            registration = new { password = invitedPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        Assert.Equal(tenantPublicId, accept.GetProperty("activeTenantPublicId").GetGuid());
        var centralUserId = accept.GetProperty("centralUserId").GetGuid();
        Assert.NotEqual(Guid.Empty, centralUserId);

        // 7) Login "normal" con las credenciales creadas en el accept.
        //    Membresía única → auto-selección de tenant + sesión operativa directa.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = invitedEmail,
            password = invitedPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        Assert.Equal("None", login.GetProperty("challenge").GetString());
        Assert.True(login.GetProperty("autoSelected").GetBoolean());
        Assert.Equal(tenantPublicId, login.GetProperty("activeTenantPublicId").GetGuid());
        Assert.Equal(centralUserId, login.GetProperty("centralUserId").GetGuid());
        var accessToken = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // 8) Claims del JWT operativo: tenant activo correcto y SIN tenant_admin.
        var claims = DecodeJwtPayload(accessToken!);
        Assert.Equal(tenantPublicId.ToString(),
            claims.GetProperty("active_tenant_id").GetString());
        Assert.Equal("full", claims.GetProperty("purpose").GetString());

        // 9) GET /api/auth/me — la membresía activa es verificable vía API.
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new("Bearer", accessToken);
        var meResp = await http.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);
        var me = await ReadJsonAsync(meResp);
        Assert.Equal(centralUserId, me.GetProperty("centralUserId").GetGuid());
        Assert.Equal(invitedEmail, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("isGlobalMasterAdmin").GetBoolean());

        var activeTenant = me.GetProperty("activeTenant");
        Assert.Equal(tenantPublicId, activeTenant.GetProperty("tenantPublicId").GetGuid());
        Assert.False(activeTenant.GetProperty("isTenantAdmin").GetBoolean());

        var availableTenants = me.GetProperty("availableTenants").EnumerateArray().ToList();
        Assert.Single(availableTenants);
        Assert.Equal(tenantPublicId,
            availableTenants[0].GetProperty("tenantPublicId").GetGuid());
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
