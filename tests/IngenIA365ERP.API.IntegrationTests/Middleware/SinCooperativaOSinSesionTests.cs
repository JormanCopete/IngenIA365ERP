using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using IngenIA365ERP.Identity.Configuration;
using IngenIA365ERP.Identity.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.API.IntegrationTests.Middleware;

/// <summary>
/// El portero de cooperativa responde tres cosas distintas que hasta el 2026-09-11
/// eran una sola («seleccionar una empresa»): sin token, token vencido, y con token
/// pero sin cooperativa. En QA una pestaña con la sesión caducada le decía a la
/// persona que eligiera empresa, cuando lo que había pasado era que su sesión expiró.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class SinCooperativaOSinSesionTests(CentralIdentityApiFixture fx)
{
    private const string RutaDeCooperativa = "/api/payroll/plans";

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage resp) =>
        JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Sin_token_es_Unauthenticated_no_seleccionar_empresa()
    {
        using var http = fx.CreateClient();

        var resp = await http.GetAsync(RutaDeCooperativa);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var sobre = await LeerAsync(resp);
        sobre.GetProperty("code").GetString().Should().Be("Identity.Unauthenticated");
        sobre.GetProperty("message").GetString().Should().NotContain("empresa");
    }

    [Fact]
    public async Task Token_vencido_es_TokenExpired_que_es_lo_que_el_cliente_renueva()
    {
        using var http = fx.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, RutaDeCooperativa);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenVencido());

        var resp = await http.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var sobre = await LeerAsync(resp);
        sobre.GetProperty("code").GetString().Should().Be("Identity.TokenExpired");
        sobre.GetProperty("errorCode").GetString().Should().Be("Identity.TokenExpired", "quien leía errorCode sigue leyéndolo");
        sobre.GetProperty("message").GetString().Should().Contain("expiró");
    }

    [Fact]
    public async Task Con_sesion_pero_sin_cooperativa_sigue_siendo_TenantNotSelected()
    {
        // El maestro no tiene cooperativa por diseño: para él, el texto de siempre.
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        using var req = new HttpRequestMessage(HttpMethod.Get, RutaDeCooperativa);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", maestro);

        var resp = await http.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var sobre = await LeerAsync(resp);
        sobre.GetProperty("code").GetString().Should().Be("Session.TenantNotSelected");
    }

    /// <summary>
    /// Un access token firmado con la clave real del host, con cooperativa y todo,
    /// pero vencido hace una hora: exactamente lo que trae una pestaña que quedó
    /// abierta.
    /// </summary>
    private string TokenVencido()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var clave = scope.ServiceProvider.GetRequiredService<IRsaKeyProvider>().GetSecurityKey();
        var hace = DateTime.UtcNow.AddHours(-2);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "vencido@cooperativa.test"),
                new Claim("purpose", "full"),
                new Claim("active_tenant_id", Guid.NewGuid().ToString()),
            ],
            notBefore: hace,
            expires: hace.AddHours(1),
            signingCredentials: new SigningCredentials(clave, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
