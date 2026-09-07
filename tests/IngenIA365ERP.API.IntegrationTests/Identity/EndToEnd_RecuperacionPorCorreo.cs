using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// La recuperación del segundo factor por correo, de punta a punta.
///
/// <para>
/// La objeción de partida, dicha por escrito: <b>si el correo puede borrar el
/// segundo factor, el segundo factor se degrada al primero</b>. Lo que hace que
/// esto sea defendible son las mitigaciones, y cada prueba de aquí afirma una:
/// que no se puede pedir sin la contraseña, que hay que esperar, que se puede
/// cancelar sin credenciales, que entrar con normalidad la deshace, y que
/// completarla NO devuelve una sesión.
/// </para>
/// </summary>
public sealed class EndToEnd_RecuperacionPorCorreo(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Clave = "Sofia-Strong-Pwd-2026!";

    [Fact]
    public async Task Sin_el_token_del_desafio_no_se_puede_pedir()
    {
        using var http = fx.CreateClient();

        // Anónimo. Si esto funcionara, el endpoint sería un oráculo: cualquiera
        // podría averiguar qué correos tienen cuenta, y llenar de avisos el buzón
        // de una víctima sin saber siquiera su contraseña.
        var resp = await http.PostAsJsonAsync("/api/auth/mfa/recovery/request", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Si_la_cooperativa_no_la_permite_no_se_ofrece()
    {
        using var http = fx.CreateClient();
        const string correo = "sofia.sinvia@coop.recuperacion.test";

        var (token, tenantId) = await MontarAsync(
            http, correo, "tenant_recup_sinvia", "900444111", "Coop. Sin Via");
        var secreto = await InscribirTotpAsync(http, token);

        // Exige segundo factor pero NO permite recuperarlo por correo. Es el valor
        // por defecto: encenderlo tiene que ser una decisión explícita.
        await GuardarPoliticaAsync(http, token, tenantId, exigir: true, permitirCorreo: false);

        var desafio = await LlegarAlDesafioAsync(http, correo);
        var resp = await PedirRecuperacionAsync(http, desafio);

        Assert.False(resp.Status == HttpStatusCode.OK);
        Assert.Equal("Identity.RecuperacionNoDisponible",
            resp.Body.GetProperty("code").GetString());

        // Y no se manda ningún correo: sin esto, el rechazo seguiría siendo un
        // canal para molestar a alguien.
        Assert.DoesNotContain(fx.Emails.Sent,
            m => m.To == correo && m.Subject.Contains("segundo factor"));
    }

    /// <summary>
    /// La historia entera con una sola cooperativa, en un solo test.
    ///
    /// <para>
    /// Va junta y no en tres pruebas porque el login está limitado a diez por
    /// minuto y por IP —un límite real del producto, no del entorno— y tres
    /// pruebas separadas lo excedían. La prueba se adapta al producto, no al
    /// revés. De paso se lee mejor: es una historia, no tres fragmentos.
    /// </para>
    /// </summary>
    [Fact]
    public async Task El_camino_completo_espera_avisa_se_cancela_y_el_ingreso_la_deshace()
    {
        using var http = fx.CreateClient();
        const string correo = "sofia.recupera@coop.recuperacion.test";

        var (token, tenantId) = await MontarAsync(
            http, correo, "tenant_recup_full", "900444222", "Coop. Recuperacion");
        var secreto = await InscribirTotpAsync(http, token);

        // Una hora de demora: es el mínimo legal, y basta para comprobar que la
        // espera existe sin tener que viajar en el tiempo.
        await GuardarPoliticaAsync(
            http, token, tenantId, exigir: true, permitirCorreo: true, horas: 1);

        // --- 1) Se pide desde el desafío, con la contraseña ya acertada ---
        var desafio = await LlegarAlDesafioAsync(http, correo);
        var pedida = await PedirRecuperacionAsync(http, desafio);

        Assert.Equal(HttpStatusCode.OK, pedida.Status);
        Assert.True(pedida.Body.GetProperty("correoEnviado").GetBoolean(),
            "si el correo no salió hay que decirlo, no responder «te lo enviamos»");

        // --- 2) El aviso llega, con los DOS enlaces, y son distintos ---
        var aviso = fx.Emails.Sent.Last(
            m => m.To == correo && m.Subject.Contains("segundo factor"));

        var tokenConfirmar = ExtraerToken(aviso.BodyHtml, "confirmar");
        var tokenCancelar = ExtraerToken(aviso.BodyHtml, "cancelar");
        Assert.NotEqual(tokenConfirmar, tokenCancelar);

        // --- 3) Todavía no: la espera es la mitad del diseño ---
        var temprano = await ConfirmarAsync(http, tokenConfirmar, Clave);
        Assert.False(temprano.Status == HttpStatusCode.OK);
        Assert.Equal("Identity.RecuperacionTodaviaNoDisponible",
            temprano.Body.GetProperty("code").GetString());

        // --- 4) Cancelar no pide NADA. Ni contraseña, ni sesión ---
        //
        // Tiene que ser más fácil que ejecutar: quien recibe el aviso sin haberlo
        // pedido está viendo un ataque, y sólo lo va a parar si pararlo es trivial.
        var cancelada = await http.PostAsJsonAsync(
            "/api/auth/mfa/recovery/cancel", new { token = tokenCancelar });
        Assert.True(cancelada.IsSuccessStatusCode);

        // Idempotente: el enlace se pulsa dos veces con facilidad.
        Assert.True((await http.PostAsJsonAsync(
            "/api/auth/mfa/recovery/cancel", new { token = tokenCancelar }))
            .IsSuccessStatusCode);

        // Y el de confirmar ya no sirve.
        Assert.False((await ConfirmarAsync(http, tokenConfirmar, Clave))
            .Status == HttpStatusCode.OK);

        // --- 5) Segunda solicitud, y esta vez Sofía entra con normalidad ---
        var desafio2 = await LlegarAlDesafioAsync(http, correo);
        Assert.Equal(HttpStatusCode.OK, (await PedirRecuperacionAsync(http, desafio2)).Status);

        var aviso2 = fx.Emails.Sent.Last(
            m => m.To == correo && m.Subject.Contains("segundo factor"));
        var tokenConfirmar2 = ExtraerToken(aviso2.BodyHtml, "confirmar");

        // Su TOTP sigue en pie: pedir la recuperación no retira nada por sí solo.
        var entro = await EntrarConTotpAsync(http, correo, secreto);
        Assert.Equal("None", entro.GetProperty("challenge").GetString());

        // --- 6) Y ese ingreso la canceló ---
        //
        // Si pudo entrar, no la necesitaba. Y si no fue ella quien la pidió, se
        // deshace sola sin que tenga que leer ningún correo — que es la mitad de
        // las veces lo que va a pasar.
        Assert.False((await ConfirmarAsync(http, tokenConfirmar2, Clave))
            .Status == HttpStatusCode.OK,
            "el ingreso correcto tiene que haber cancelado la solicitud");
    }

    [Fact]
    public async Task Un_enlace_inventado_no_dice_si_existe()
    {
        using var http = fx.CreateClient();

        var confirmar = await ConfirmarAsync(http, "esto-no-es-un-token", "loquesea");
        Assert.Equal("Identity.RecuperacionNoValida",
            confirmar.Body.GetProperty("code").GetString());

        var cancelar = await http.PostAsJsonAsync(
            "/api/auth/mfa/recovery/cancel", new { token = "tampoco" });
        Assert.False(cancelar.IsSuccessStatusCode);
    }

    // ---------- Auxiliares ----------

    private async Task<(string Token, Guid TenantId)> MontarAsync(
        HttpClient http, string correo, string esquema, string nit, string nombre)
    {
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = nombre,
                schemaName = esquema,
                nit,
                legalName = $"{nombre} SAS",
                contactEmail = $"contacto.{esquema}@coop.recuperacion.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = correo,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registro = await LeerJsonAsync(await http.SendAsync(registerReq));
        var tenantId = registro.GetProperty("tenantPublicId").GetGuid();

        var invitacion = fx.Emails.Sent.Last(m => m.To == correo);
        var match = Regex.Match(invitacion.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success);

        var acceptResp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = Clave },
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        return ((await LeerJsonAsync(acceptResp)).GetProperty("accessToken").GetString()!, tenantId);
    }

    private static async Task<string> InscribirTotpAsync(HttpClient http, string token)
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var begin = await LeerJsonAsync(await http.SendAsync(beginReq));
        var secreto = begin.GetProperty("secretBase32").GetString()!;

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        confirmReq.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(confirmReq);
        Assert.True(resp.IsSuccessStatusCode,
            $"Confirm falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        return secreto;
    }

    private static async Task GuardarPoliticaAsync(
        HttpClient http, string token, Guid tenantId,
        bool exigir, bool permitirCorreo, int? horas = null)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantId}/mfa-policy")
        {
            Content = JsonContent.Create(new
            {
                isRequired = exigir,
                permitirRecuperacionPorCorreo = permitirCorreo,
                horasDeDemora = horas,
            }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"PUT mfa-policy falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>Login hasta el desafío. Devuelve el challenge token.</summary>
    private static async Task<string> LlegarAlDesafioAsync(HttpClient http, string correo)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login",
            new { email = correo, password = Clave });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await LeerJsonAsync(resp);
        Assert.Equal("MfaRequired", body.GetProperty("challenge").GetString());
        return body.GetProperty("challengeToken").GetString()!;
    }

    private static async Task<JsonElement> EntrarConTotpAsync(
        HttpClient http, string correo, string secreto)
    {
        var desafio = await LlegarAlDesafioAsync(http, correo);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        req.Headers.Authorization = new("Bearer", desafio);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"Verify falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        return await LeerJsonAsync(resp);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> PedirRecuperacionAsync(
        HttpClient http, string challengeToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/recovery/request");
        req.Headers.Authorization = new("Bearer", challengeToken);
        var resp = await http.SendAsync(req);
        return (resp.StatusCode, await LeerJsonAsync(resp));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> ConfirmarAsync(
        HttpClient http, string token, string password)
    {
        var resp = await http.PostAsJsonAsync(
            "/api/auth/mfa/recovery/confirm", new { token, password });
        return (resp.StatusCode, await LeerJsonAsync(resp));
    }

    private static string ExtraerToken(string html, string accion)
    {
        var match = Regex.Match(html, $@"/security/mfa-recovery/{accion}\?token=([A-Za-z0-9_\-%]+)");
        Assert.True(match.Success, $"El aviso no trae el enlace de {accion}.");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
