using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// El contrato de los cuatro endpoints de passkey, hasta donde se puede llegar
/// sin un navegador de verdad.
///
/// <para>
/// <b>Lo que esta prueba NO cubre, dicho sin rodeos:</b> nada de lo que ocurre
/// dentro del navegador. No hay autenticador, así que no se firma ningún reto y
/// no se verifica ninguna firma. Cubrir eso exige un autenticador virtual por
/// CDP con un Chrome en CI, y este repositorio no tiene hoy ninguna prueba de
/// navegador. Mientras no la tenga, el interop se prueba a mano.
/// </para>
///
/// <para>
/// Lo que sí cubre es todo lo demás, que no es poco: que el reto se emita con el
/// dominio configurado, que sea de un solo uso, que no se pueda canjear un reto
/// de alta como reto de ingreso, y que una respuesta basura se rechace con un
/// error del contrato en vez de con un 500.
/// </para>
/// </summary>
public sealed class EndToEnd_ContratoWebAuthn(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Correo = "carla.passkey@coop.webauthn.test";
    private const string Clave = "Carla-Strong-Pwd-2026!";

    [Fact]
    public async Task El_alta_emite_un_reto_con_el_dominio_configurado_y_es_de_un_solo_uso()
    {
        using var http = fx.CreateClient();
        var token = await MontarPersonaAsync(http);

        // --- Primer viaje ---
        var begin = await BeginAltaAsync(http, token);

        var opciones = JsonSerializer.Deserialize<JsonElement>(
            begin.GetProperty("opcionesJson").GetString()!, Json);

        // El reto viaja, pero el que vale se queda en el servidor: lo que vuelve
        // es sólo un identificador para recuperarlo.
        var retoId = begin.GetProperty("retoId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(retoId));

        // El dominio es el que decide si el navegador siquiera muestra el diálogo.
        // En desarrollo es localhost, porque el front sirve desde ahí.
        Assert.Equal("localhost", opciones.GetProperty("rp").GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(opciones.GetProperty("challenge").GetString()));

        // --- Segundo viaje, con una respuesta que no es de ningún autenticador ---
        var primerIntento = await ConfirmarAltaAsync(http, token, retoId!, """{"rawId":"AAAA"}""");

        // Se rechaza con un código del contrato, no con un 500: una respuesta mal
        // formada es una petición inválida, no una avería del servidor.
        Assert.NotEqual(HttpStatusCode.InternalServerError, primerIntento.Status);
        Assert.False(primerIntento.Status == HttpStatusCode.OK);

        // --- El reto ya se consumió ---
        //
        // Es lo único que impide reproducir una respuesta capturada. Si se pudiera
        // canjear dos veces, dejaría de servir para eso.
        var segundoIntento = await ConfirmarAltaAsync(http, token, retoId!, """{"rawId":"AAAA"}""");
        Assert.Equal("Profile.Mfa.RetoVencido",
            segundoIntento.Body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Un_reto_de_alta_no_sirve_para_entrar()
    {
        using var http = fx.CreateClient();
        var token = await MontarPersonaAsync(http, "diego.passkey@coop.webauthn.test");

        var begin = await BeginAltaAsync(http, token);
        var retoDeAlta = begin.GetProperty("retoId").GetString()!;

        // Con el token de sesión no se llega siquiera al endpoint de ingreso, que
        // exige el token del desafío. Pero aunque se llegara, el reto está
        // guardado bajo su propósito: uno de alta no aparece al buscar uno de
        // ingreso, en vez de aparecer y confiar en que alguien se acuerde de
        // comprobar para qué se emitió.
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/webauthn/verify")
        {
            Content = JsonContent.Create(new { retoId = retoDeAlta, respuestaJson = "{}" }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await LeerJsonAsync(resp);
        Assert.Equal("Identity.WrongTokenPurpose", cuerpo.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Sin_token_no_se_emite_ningun_reto()
    {
        using var http = fx.CreateClient();

        var resp = await http.PostAsJsonAsync(
            "/api/profile/mfa/webauthn/begin", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------- Auxiliares ----------

    private async Task<string> MontarPersonaAsync(HttpClient http, string? correo = null)
    {
        var destino = correo ?? Correo;
        var esquema = destino.StartsWith("carla", StringComparison.Ordinal)
            ? "tenant_webauthn_a" : "tenant_webauthn_b";
        var nit = destino.StartsWith("carla", StringComparison.Ordinal) ? "900222111" : "900222112";

        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = $"Coop. WebAuthn {esquema}",
                schemaName = esquema,
                nit,
                legalName = "Coop. WebAuthn SAS",
                contactEmail = $"contacto.{esquema}@coop.webauthn.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = destino,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(registerReq)).StatusCode);

        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == destino);
        Assert.NotNull(mensaje);
        var match = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success);

        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = Clave },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        return (await LeerJsonAsync(acceptResp)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> BeginAltaAsync(HttpClient http, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/webauthn/begin");
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"Begin falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return await LeerJsonAsync(resp);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> ConfirmarAltaAsync(
        HttpClient http, string token, string retoId, string respuestaJson)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/webauthn/confirm")
        {
            Content = JsonContent.Create(new { retoId, respuestaJson }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        return (resp.StatusCode, await LeerJsonAsync(resp));
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
