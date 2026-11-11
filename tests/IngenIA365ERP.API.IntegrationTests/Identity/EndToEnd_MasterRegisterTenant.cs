using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IngenIA365ERP.API.IntegrationTests.Identity.Documento;

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
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
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
                firstAdminDocumentType = "CC", firstAdminDocumentNumber = DocumentoDePrueba.Numero(adminEmail),
                firstAdminDocumentNumberConfirmation = DocumentoDePrueba.Numero(adminEmail),
                firstAdminFirstName = "Administrador", firstAdminLastName = "De Prueba",
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
            documentNumber = DocumentoDePrueba.Numero(adminEmail),
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

        // 7b) Feature 018: la empresa nace con su oficina principal y su agencia, vinculadas.
        var oficinas = await LeerItemsAsync(http, adminAccessToken!, "/api/admin/branches");
        var oficina = Assert.Single(oficinas);
        Assert.Equal("PRINCIPAL", oficina.GetProperty("code").GetString());
        Assert.True(oficina.GetProperty("isHeadquarters").GetBoolean());
        var agencias = await LeerItemsAsync(http, adminAccessToken!, "/api/core/branches?PageNumber=1&PageSize=50");
        var agencia = Assert.Single(agencias);
        Assert.Equal(oficina.GetProperty("publicId").GetGuid(), agencia.GetProperty("tenantBranchPublicId").GetGuid());

        // 7c) El administrador crea otra oficina sin decir la empresa, y su agencia queda creada en el mismo paso.
        using var nuevaOficina = new HttpRequestMessage(HttpMethod.Post, "/api/admin/branches")
        {
            Content = JsonContent.Create(new { code = "NORTE", name = "Oficina Norte", isHeadquarters = false, crearAgencia = true }),
        };
        nuevaOficina.Headers.Authorization = new("Bearer", adminAccessToken);
        var nuevaOficinaResp = await http.SendAsync(nuevaOficina);
        Assert.Equal(HttpStatusCode.OK, nuevaOficinaResp.StatusCode);
        Assert.Equal(2, (await LeerItemsAsync(http, adminAccessToken!, "/api/core/branches?PageNumber=1&PageSize=50")).Count);

        // 8) Replay del token consumido → 410 Invitation.AlreadyAccepted (FR-030).
        var replayResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            documentNumber = DocumentoDePrueba.Numero(adminEmail),
            registration = new { password = "Otra-Password-2026-XY" },
        });
        Assert.Equal(HttpStatusCode.Gone, replayResp.StatusCode);
    }

    private static async Task<List<JsonElement>> LeerItemsAsync(HttpClient http, string token, string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await ReadJsonAsync(resp);
        return json.GetProperty("items").EnumerateArray().ToList();
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
