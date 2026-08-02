using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Seguridad (FR-030) — Replay de token de invitación: un accept exitoso
/// consume el token de un solo uso; repetir el accept con el MISMO token
/// debe fallar con <c>410 Gone</c> y código <c>Invitation.AlreadyAccepted</c>
/// (mapeado por <c>ErrorEnvelopeFilter</c>), sin crear sesión ni usuario extra.
/// </summary>
public sealed class Security_InvitationReplay(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Replay_de_token_de_invitacion_ya_aceptado_devuelve_410_AlreadyAccepted()
    {
        using var http = fx.CreateClient();

        // 1) Login del master admin sembrado por la fixture.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = CentralIdentityApiFixture.MasterPassword,
        });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var login = await ReadJsonAsync(loginResp);
        var masterToken = login.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(masterToken));

        // 2) Registrar tenant + invitación del primer admin (atómico) para
        //    obtener una invitación real cuyo token llega por correo capturado.
        const string invitedEmail = "replay.victima@coop.replay.test";
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Replay Test",
                schemaName = "tenant_replay",
                nit = "900777888",
                legalName = "Coop. Replay SAS",
                contactEmail = "contacto@coop.replay.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = invitedEmail,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);

        // 3) Extraer el token plano del correo de invitación capturado.
        var invitationEmail = fx.Emails.Sent.LastOrDefault(m => m.To == invitedEmail);
        Assert.NotNull(invitationEmail);
        var tokenMatch = Regex.Match(invitationEmail!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(tokenMatch.Success, "El correo de invitación no contiene token=");
        var plainToken = tokenMatch.Groups[1].Value;

        // 4) Accept exitoso (rama registro): consume el token de un solo uso.
        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            registration = new { password = "Replay-Strong-Pwd-2026" },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
        var accept = await ReadJsonAsync(acceptResp);
        Assert.False(string.IsNullOrWhiteSpace(accept.GetProperty("accessToken").GetString()));

        // 5) Replay: mismo token → 410 Gone con envelope { code, message, traceId }.
        var replayResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = plainToken,
            registration = new { password = "Otro-Intento-Pwd-2026" },
        });
        Assert.Equal(HttpStatusCode.Gone, replayResp.StatusCode);
        var replay = await ReadJsonAsync(replayResp);
        Assert.Equal("Invitation.AlreadyAccepted", replay.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(replay.GetProperty("message").GetString()));

        // 6) El replay no debe emitir tokens de sesión.
        Assert.False(replay.TryGetProperty("accessToken", out _));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
