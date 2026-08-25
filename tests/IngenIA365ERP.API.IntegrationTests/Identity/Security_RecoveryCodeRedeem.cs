using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// FR-108/FR-109 — Canje de recovery codes como segundo factor:
/// se registra un tenant con admin invitado, el admin enrola MFA (TOTP real
/// vía Otp.NET contra el secret devuelto por <c>/api/profile/mfa/enroll</c>),
/// vuelve a loguear (challenge <c>MfaRequired</c>) y canjea un recovery code
/// en <c>POST /api/auth/mfa/verify</c> con <c>useRecoveryCode=true</c>:
/// obtiene sesión operativa y <c>recoveryCodesRemaining=9</c>. El mismo
/// código reintentado debe fallar (one-shot) con
/// <c>Identity.MfaInvalid</c> (401 — es un fallo de autenticación).
/// </summary>
public sealed class Security_RecoveryCodeRedeem(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Recovery_code_se_canjea_una_sola_vez_y_reporta_los_restantes()
    {
        using var http = fx.CreateClient();

        // 1) Login del master y registro de tenant + primera invitación admin.
        var loginMasterResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = CentralIdentityApiFixture.MasterPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginMasterResp.StatusCode);
        var loginMaster = await ReadJsonAsync(loginMasterResp);
        var masterToken = loginMaster.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        const string adminEmail = "laura.mfa@coop.recovery.test";
        const string adminPassword = "Laura-Recovery-Pwd-2026";
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Recovery Test",
                schemaName = "tenant_recovery",
                nit = "900777888",
                legalName = "Coop. Recovery SAS",
                contactEmail = "contacto@coop.recovery.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = adminEmail,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        // 2) Aceptar la invitación (rama registro) → sesión operativa purpose=full.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == adminEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");

        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = tokenMatch.Groups[1].Value,
            registration = new { password = adminPassword },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.Equal("None", accept.GetProperty("challenge").GetString());
        var fullToken = accept.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(fullToken));

        // 3) Enrollment MFA voluntario: begin (secret) + confirm (TOTP real).
        using var enrollReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll")
        {
            Content = JsonContent.Create(new { }),
        };
        enrollReq.Headers.Authorization = new("Bearer", fullToken);
        var enrollResp = await http.SendAsync(enrollReq);
        Assert.Equal(HttpStatusCode.OK, enrollResp.StatusCode);
        var enroll = await ReadJsonAsync(enrollResp);
        var secretBase32 = enroll.GetProperty("secretBase32").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secretBase32));

        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new { code = totp.ComputeTotp() }),
        };
        confirmReq.Headers.Authorization = new("Bearer", fullToken);
        var confirmResp = await http.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirm = await ReadJsonAsync(confirmResp);

        // Los recovery codes DEFINITIVOS son los del confirm (los del begin se
        // descartan: ConfirmMfaSetupAsync regenera 10 nativos de Identity).
        var recoveryCodes = confirm.GetProperty("recoveryCodes")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();
        Assert.Equal(10, recoveryCodes.Count);
        var recoveryCode = recoveryCodes[0];

        // 4) Nuevo login del admin → challenge MfaRequired con challengeToken.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = adminEmail,
            password = adminPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        Assert.Equal("MfaRequired", login.GetProperty("challenge").GetString());
        var challengeToken = login.GetProperty("challengeToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(challengeToken));

        // 5) Canjear el recovery code → sesión operativa + 9 códigos restantes.
        using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = recoveryCode, useRecoveryCode = true }),
        };
        verifyReq.Headers.Authorization = new("Bearer", challengeToken);
        var verifyResp = await http.SendAsync(verifyReq);
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var verify = await ReadJsonAsync(verifyResp);
        Assert.Equal("None", verify.GetProperty("challenge").GetString());
        Assert.False(string.IsNullOrWhiteSpace(verify.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(verify.GetProperty("refreshToken").GetString()));
        Assert.Equal(9, verify.GetProperty("recoveryCodesRemaining").GetInt32());

        // 6) One-shot: el MISMO código con un challenge fresco debe fallar.
        var loginRetryResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = adminEmail,
            password = adminPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginRetryResp.StatusCode);
        var loginRetry = await ReadJsonAsync(loginRetryResp);
        Assert.Equal("MfaRequired", loginRetry.GetProperty("challenge").GetString());
        var challengeToken2 = loginRetry.GetProperty("challengeToken").GetString();

        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new { code = recoveryCode, useRecoveryCode = true }),
        };
        replayReq.Headers.Authorization = new("Bearer", challengeToken2);
        var replayResp = await http.SendAsync(replayReq);

        // 401: un segundo factor inválido es un fallo de AUTENTICACIÓN, no una
        // regla de negocio incumplida, y el contrato lo fija en
        // specs/002/contracts/auth.md:111.
        Assert.Equal(HttpStatusCode.Unauthorized, replayResp.StatusCode);
        var replay = await ReadJsonAsync(replayResp);
        Assert.Equal("Identity.MfaInvalid", replay.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
