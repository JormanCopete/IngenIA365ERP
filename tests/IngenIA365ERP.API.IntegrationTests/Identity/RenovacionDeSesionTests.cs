using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// <c>POST /api/auth/refresh</c> por HTTP, contra Redis real: rotación, reuso, y
/// el tope absoluto de la sesión. Lo que el cliente da por hecho al renovar en
/// silencio y avisar «tu sesión termina en…».
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public class RenovacionDeSesionTests(CentralIdentityApiFixture fx)
{
    private static async Task<JsonElement> LeerAsync(HttpResponseMessage resp)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    [Fact]
    public async Task El_refresh_rota_y_el_tope_de_la_sesion_no_se_desliza()
    {
        using var http = fx.CreateClient();
        var sesion = await fx.SesionMaestroAsync(http);
        var refresh1 = sesion.GetProperty("refreshToken").GetString();
        var tope = sesion.GetProperty("refreshTokenExpiresAt").GetDateTime();
        tope.Should().BeCloseTo(DateTime.UtcNow.AddHours(12), TimeSpan.FromMinutes(2), "el ingreso abre doce horas");

        // Primer canje: access nuevo, refresh nuevo, mismo tope.
        var r1 = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh1 });
        r1.StatusCode.Should().Be(HttpStatusCode.OK, await r1.Content.ReadAsStringAsync());
        var c1 = await LeerAsync(r1);
        c1.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        var refresh2 = c1.GetProperty("refreshToken").GetString();
        refresh2.Should().NotBe(refresh1, "rota");
        c1.GetProperty("refreshTokenExpiresAt").GetDateTime().Should().BeCloseTo(tope, TimeSpan.FromSeconds(2),
            "el tope es el del ingreso, no doce horas desde el canje");

        // El access nuevo sirve.
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new("Bearer", c1.GetProperty("accessToken").GetString());
        (await http.SendAsync(me)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Segundo canje con el nuevo: sigue el mismo tope.
        var r2 = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh2 });
        r2.StatusCode.Should().Be(HttpStatusCode.OK, await r2.Content.ReadAsStringAsync());
        (await LeerAsync(r2)).GetProperty("refreshTokenExpiresAt").GetDateTime()
            .Should().BeCloseTo(tope, TimeSpan.FromSeconds(2));

        // Reusar el primero, ya rotado, es una violación de familia: 401 y código propio.
        var reuso = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh1 });
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LeerAsync(reuso)).GetProperty("code").GetString().Should().Be("Identity.RefreshToken.Reused");
    }

    [Fact]
    public async Task Un_refresh_inventado_responde_401_con_sobre_de_error()
    {
        using var http = fx.CreateClient();

        var resp = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "no-existe" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var sobre = await LeerAsync(resp);
        sobre.GetProperty("code").GetString().Should().Be("Identity.RefreshToken.Invalid");
        sobre.TryGetProperty("traceId", out _).Should().BeTrue();
    }
}
