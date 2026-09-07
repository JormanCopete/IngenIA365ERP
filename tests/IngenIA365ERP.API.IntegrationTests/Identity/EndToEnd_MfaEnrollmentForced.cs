using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Flujo completo de enrollment de MFA FORZADO por política de tenant:
/// master registra tenant + admin → el admin entra (challenge None, sin
/// política) → activa la política MFA del tenant (IsRequired=true) →
/// el siguiente login devuelve <c>MfaEnrollmentRequired</c> con challenge
/// token <c>purpose=mfa-enroll</c> → <c>POST /api/profile/mfa/enroll</c>
/// entrega el secreto TOTP → <c>POST /api/profile/mfa/confirm</c> con el
/// código calculado (Otp.NET) eleva la sesión a tokens operativos
/// (<c>purpose=full</c>, tenant activo resuelto) sin re-login → un login
/// posterior ya pide <c>MfaRequired</c> (mfa-verify), no re-enrollment.
/// </summary>
public sealed class EndToEnd_MfaEnrollmentForced(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Politica_mfa_forzada_obliga_enrollment_y_confirm_eleva_a_sesion_operativa()
    {
        using var http = fx.CreateClient();

        // 1) Login del master (sin membresías → sesión operativa master).
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 2) Registrar tenant + invitación del primer admin (atómico).
        const string memberEmail = "laura.mfa@coop.mfaforzado.test";
        const string memberPassword = "Laura-Strong-Pwd-2026!";
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. MFA Forzado Test",
                schemaName = "tenant_mfa_forzado",
                nit = "900777888",
                legalName = "Coop. MFA Forzado SAS",
                contactEmail = "contacto@coop.mfaforzado.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = memberEmail,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var register = await ReadJsonAsync(registerResp);
        var tenantPublicId = register.GetProperty("tenantPublicId").GetGuid();

        // 3) Aceptar la invitación (rama registro). El tenant aún no tiene
        //    política MFA → challenge None + sesión operativa tenant_admin.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == memberEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");

        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenMatch.Groups[1].Value,
            registration = new { password = memberPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        var adminToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(adminToken));

        // 4) El tenant admin activa la política MFA del tenant (IsRequired=true).
        using var policyReq = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantPublicId}/mfa-policy")
        {
            Content = JsonContent.Create(new { isRequired = true }),
        };
        policyReq.Headers.Authorization = new("Bearer", adminToken);
        var policyResp = await http.SendAsync(policyReq);
        Assert.True(policyResp.IsSuccessStatusCode,
            $"PUT mfa-policy falló: {(int)policyResp.StatusCode}");

        // 5) Login del miembro SIN MFA → MfaEnrollmentRequired con challenge
        //    token purpose=mfa-enroll y el tenant que fuerza la política.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = memberEmail,
            password = memberPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        Assert.Equal("MfaEnrollmentRequired", login.GetProperty("challenge").GetString());
        Assert.Equal("mfa-enroll", login.GetProperty("challengeTokenPurpose").GetString());
        var challengeToken = login.GetProperty("challengeToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(challengeToken));
        Assert.Contains(
            login.GetProperty("tenantsRequiringMfa").EnumerateArray(),
            t => t.GetProperty("tenantPublicId").GetGuid() == tenantPublicId);

        // El propio challenge token declara purpose=mfa-enroll en sus claims.
        Assert.Equal("mfa-enroll",
            DecodeJwtPayload(challengeToken!).GetProperty("purpose").GetString());

        // 6) Begin enrollment con el challenge token → secreto TOTP.
        using var enrollReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        enrollReq.Headers.Authorization = new("Bearer", challengeToken);
        var enrollResp = await http.SendAsync(enrollReq);
        Assert.Equal(HttpStatusCode.OK, enrollResp.StatusCode);
        var enroll = await ReadJsonAsync(enrollResp);
        var secretBase32 = enroll.GetProperty("secretBase32").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secretBase32));
        Assert.StartsWith("otpauth://totp/", enroll.GetProperty("otpAuthUri").GetString());

        // 7) Confirm con el código TOTP calculado (Otp.NET, SHA1/30s/6 dígitos —
        //    mismos defaults que AspNetCoreIdentityProvider.ConfirmMfaSetupAsync).
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new { code = totp.ComputeTotp() }),
        };
        confirmReq.Headers.Authorization = new("Bearer", challengeToken);
        var confirmResp = await http.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirm = await ReadJsonAsync(confirmResp);

        // Recovery codes entregados + elevación a sesión operativa (el usuario
        // tiene exactamente 1 membresía activa → tenant auto-resuelto).
        Assert.NotEmpty(confirm.GetProperty("recoveryCodes").EnumerateArray().ToList());
        var operationalToken = confirm.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(operationalToken));
        Assert.False(string.IsNullOrWhiteSpace(confirm.GetProperty("refreshToken").GetString()));
        Assert.Equal(tenantPublicId, confirm.GetProperty("activeTenantPublicId").GetGuid());

        // 8) Claims del token operativo: purpose=full sobre el tenant correcto.
        var claims = DecodeJwtPayload(operationalToken!);
        Assert.Equal("full", claims.GetProperty("purpose").GetString());
        Assert.Equal(tenantPublicId.ToString(),
            claims.GetProperty("active_tenant_id").GetString());

        // 9) El token operativo sirve para endpoints autenticados (/api/auth/me).
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meReq.Headers.Authorization = new("Bearer", operationalToken);
        var meResp = await http.SendAsync(meReq);
        Assert.Equal(HttpStatusCode.OK, meResp.StatusCode);

        // 10) Un nuevo login ya NO pide enrollment: el usuario tiene MFA →
        //     challenge MfaRequired (purpose mfa-verify).
        var reloginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = memberEmail,
            password = memberPassword,
        });
        Assert.Equal(HttpStatusCode.OK, reloginResp.StatusCode);
        var relogin = await ReadJsonAsync(reloginResp);
        Assert.Equal("MfaRequired", relogin.GetProperty("challenge").GetString());
        Assert.Equal("mfa-verify", relogin.GetProperty("challengeTokenPurpose").GetString());
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
