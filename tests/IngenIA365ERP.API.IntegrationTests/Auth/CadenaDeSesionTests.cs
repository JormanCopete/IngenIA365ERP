using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IngenIA365ERP.API.IntegrationTests.Identity;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Auth;

/// <summary>
/// La cadena completa de una sesión por HTTP: login → refresh → logout, y la
/// detección de reuso del refresh.
///
/// <para>
/// <b>Reemplaza a <c>LoginFlowTests</c> y <c>RefreshTokenRotationTests</c></b>,
/// que eran de Fase 0 y llevaban desde el cutover T071 posteando a
/// <c>/api/auth/login</c> con <c>{tenantSubdomainOrNit, username, password}</c>.
/// Esas rutas están comentadas; quien atiende hoy es la identidad central, que
/// sólo acepta <c>{email, password}</c>. El 400 del validador y los 422 que se
/// veían después eran cascadas de eso, no defectos.
/// </para>
///
/// <para>
/// No se borraron sin más porque eran la ÚNICA cobertura HTTP de
/// <c>/api/auth/logout</c> y de la rotación del refresh: lo demás vive en
/// pruebas de unidad del handler, que no tocan el mapeo a estados HTTP — y ahí
/// justamente había un defecto (ver <see cref="Reusar_un_refresh_ya_rotado_responde_401"/>).
/// </para>
/// </summary>
public class CadenaDeSesionTests(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_refresh_y_logout_encadenados()
    {
        using var http = fx.CreateClient();

        var sesion = await LoginAsync(http);
        var accesoInicial = sesion.GetProperty("accessToken").GetString()!;
        var refrescoInicial = sesion.GetProperty("refreshToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accesoInicial));
        Assert.False(string.IsNullOrWhiteSpace(refrescoInicial));

        // Refresh: rota de verdad. Si devolviera el mismo refresh, la rotación
        // sería decorativa y un token robado valdría para siempre.
        var refrescoResp = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = refrescoInicial });
        Assert.Equal(HttpStatusCode.OK, refrescoResp.StatusCode);
        var rotado = await LeerJsonAsync(refrescoResp);
        var refrescoNuevo = rotado.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(refrescoInicial, refrescoNuevo);

        // Logout: exige sesión (RequireAuthorization) y cierra la familia.
        using var salidaReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new { refreshToken = refrescoNuevo }),
        };
        salidaReq.Headers.Authorization =
            new("Bearer", rotado.GetProperty("accessToken").GetString());
        var salidaResp = await http.SendAsync(salidaReq);
        Assert.True(salidaResp.IsSuccessStatusCode,
            $"logout respondió {(int)salidaResp.StatusCode}");

        // Y después del logout el refresh ya no vale: si valiera, cerrar sesión
        // no cerraría nada.
        var despues = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = refrescoNuevo });
        Assert.False(despues.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Reusar_un_refresh_ya_rotado_responde_401()
    {
        // Esta prueba fija un DEFECTO DE PRODUCTO que se corrigió con ella:
        // Identity.RefreshToken.* no tenía mapeo en ErrorEnvelopeFilter y caía
        // al default 422, cuando specs/002/contracts/auth.md:138 promete 401.
        // Un token reusado es un fallo de autenticación, no una entidad no
        // procesable — y los clientes escritos contra el contrato reaccionan al
        // 401, no al 422.
        using var http = fx.CreateClient();

        var sesion = await LoginAsync(http);
        var original = sesion.GetProperty("refreshToken").GetString()!;

        var rotadoResp = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = original });
        Assert.Equal(HttpStatusCode.OK, rotadoResp.StatusCode);
        var rotado = await LeerJsonAsync(rotadoResp);
        var sucesor = rotado.GetProperty("refreshToken").GetString()!;

        // Reuso del ORIGINAL, ya rotado.
        var reusoResp = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = original });
        Assert.Equal(HttpStatusCode.Unauthorized, reusoResp.StatusCode);
        var reuso = await LeerJsonAsync(reusoResp);
        Assert.StartsWith("Identity.RefreshToken.",
            reuso.GetProperty("code").GetString());

        // La familia entera cae: el sucesor legítimo tampoco sirve ya. Es lo que
        // convierte la detección en una defensa y no en un aviso.
        var sucesorResp = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = sucesor });
        Assert.False(sucesorResp.IsSuccessStatusCode,
            "detectado el reuso, el sucesor debe morir con la familia");
    }

    /// <summary>
    /// Login del master: no tiene segundo factor inscrito, así que la respuesta
    /// trae los tokens directamente y la cadena se puede seguir sin simular TOTP.
    /// </summary>
    /// <summary>
    /// Sesión del maestro, con su segundo factor. Antes esta prueba entraba con
    /// sólo contraseña —el maestro se saltaba el MFA— y por eso podía pedir el
    /// login en una línea. Ahora el recorrido lo resuelve la fixture.
    /// </summary>
    private async Task<JsonElement> LoginAsync(HttpClient http) =>
        await fx.SesionMaestroAsync(http);

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
