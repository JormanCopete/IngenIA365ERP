using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// T118 (US5) — Flujo completo del master admin registrando una cooperativa:
/// login master (sin tenants) → <c>POST /api/saas/tenants/with-admin</c> →
/// correo de invitación capturado → preview → accept (rama registro) →
/// el aceptante entra como tenant admin (claims <c>active_tenant_id</c> +
/// <c>tenant_admin</c>) y la membresía queda Active/IsTenantAdmin en la BD.
/// </summary>
public sealed class EndToEnd_MasterRegisterTenant(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Master_registra_tenant_y_el_admin_invitado_entra_como_tenant_admin()
    {
        using var http = fx.CreateClient();

        // 1) Login del master — sin membresías: sesión operativa master (challenge None).
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = CentralIdentityApiFixture.MasterPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        Assert.Equal("None", login.GetProperty("challenge").GetString());
        Assert.True(login.GetProperty("isGlobalMasterAdmin").GetBoolean());
        var masterToken = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 2) Registrar tenant + primera invitación admin (atómico).
        const string adminEmail = "sofia.rios@coop.integracion.test";
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Integracion Test",
                schemaName = "tenant_integracion",
                nit = "900555666",
                legalName = "Coop. Integracion SAS",
                contactEmail = "contacto@coop.integracion.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = adminEmail,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var register = await ReadJsonAsync(registerResp);
        var tenantPublicId = register.GetProperty("tenantPublicId").GetGuid();
        Assert.NotEqual(Guid.Empty, tenantPublicId);

        // 3) El correo de invitación fue "enviado" (capturado) con el token plano.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == adminEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");
        var plainToken = tokenMatch.Groups[1].Value;

        // 4) Preview sin consumir: válida, marcada como admin, email correcto.
        var previewResp = await http.GetAsync(
            $"/api/invitations/{Uri.EscapeDataString(plainToken)}/preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var preview = await ReadJsonAsync(previewResp);
        Assert.True(preview.GetProperty("isValid").GetBoolean());
        Assert.True(preview.GetProperty("inviteAsTenantAdmin").GetBoolean());
        Assert.False(preview.GetProperty("isExistingCentralUser").GetBoolean());
        Assert.Equal(adminEmail, preview.GetProperty("email").GetString());

        // 5) Accept rama registro. El tenant nuevo no tiene política MFA →
        //    challenge None + sesión operativa con tenant_admin.
        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            registration = new { password = "Sofia-Strong-Pwd-2026" },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        Assert.Equal(tenantPublicId, accept.GetProperty("activeTenantPublicId").GetGuid());
        var adminAccessToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(adminAccessToken));
        var centralUserId = accept.GetProperty("centralUserId").GetGuid();

        // 6) Claims del JWT del aceptante: opera sobre el tenant recién creado
        //    y con rol de admin de empresa (FR-005 / US5 AC-1).
        var claims = DecodeJwtPayload(adminAccessToken!);
        Assert.Equal(tenantPublicId.ToString(),
            claims.GetProperty("active_tenant_id").GetString());
        Assert.True(claims.GetProperty("tenant_admin").GetBoolean());
        Assert.Equal("full", claims.GetProperty("purpose").GetString());

        // 7) Estado en BD Admin: membresía Active + IsTenantAdmin.
        using var scope = fx.Factory.Services.CreateScope();
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var membership = await adminDb.TenantMemberships
            .AsNoTracking()
            .SingleAsync(m => m.CentralUserId == centralUserId && m.TenantId == tenantPublicId);
        Assert.True(membership.IsTenantAdmin);
        Assert.Equal("Active", membership.Status.ToString());

        // 8) Replay del token consumido → 410 Invitation.AlreadyAccepted (FR-030).
        var replayResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            registration = new { password = "Otra-Password-2026-XY" },
        });
        Assert.Equal(HttpStatusCode.Gone, replayResp.StatusCode);
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
