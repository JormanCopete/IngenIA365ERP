using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.CanalDigital;

/// <summary>
/// Feature 021, T152 (research R2; constitución v2.1.0, «Sistemas satélite»): la API de servicio <c>/api/canal/v1</c> sólo acepta el token del canal
/// —ES256 con la llave publicada, a lo sumo 60 s, <c>jti</c> de un solo uso— y nunca el JWT de una persona; un token de otra cooperativa no abre las
/// rutas de esta.
/// </summary>
[Collection("Identidad central compartida")]
public sealed class CanalServicioTests(CentralIdentityApiFixture fx)
{
    private async Task<HttpResponseMessage> PedirAsync(HttpClient http, string ruta, string token)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(peticion);
    }

    [Fact]
    public async Task El_token_del_canal_abre_el_directorio_una_sola_vez()
    {
        using var http = fx.CreateClient();
        var token = CanalE2E.Token(fx, "platform");

        (await PedirAsync(http, "/api/canal/v1/tenants", token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PedirAsync(http, "/api/canal/v1/tenants", token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "el jti ya se usó");
    }

    [Fact]
    public async Task Un_token_largo_o_de_otra_llave_no_entra()
    {
        using var http = fx.CreateClient();
        (await PedirAsync(http, "/api/canal/v1/tenants", CanalE2E.Token(fx, "platform", vida: TimeSpan.FromMinutes(10)))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        using var otra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        (await PedirAsync(http, "/api/canal/v1/tenants", CanalE2E.Token(fx, "platform", llave: otra))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_JWT_de_una_persona_no_abre_la_API_de_servicio()
    {
        using var http = fx.CreateClient();
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);

        (await PedirAsync(http, "/api/canal/v1/tenants", maestro)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_token_de_otra_cooperativa_no_abre_las_rutas_de_esta()
    {
        using var http = fx.CreateClient();
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "canal_servicio");

        var r = await PedirAsync(http, $"/api/canal/v1/tenants/{coop.TenantPublicId}/legal-texts", CanalE2E.Token(fx, Guid.NewGuid().ToString()));

        r.IsSuccessStatusCode.Should().BeFalse();
        r.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}
