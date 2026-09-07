using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// La política de métodos, recorrida por la API de verdad.
///
/// <para>
/// Lo que estas pruebas cubren y las unitarias no: el claim <c>mfa_method</c> ida
/// y vuelta —lo emite el issuer real y lo lee el accessor real desde el
/// <c>HttpContext</c>, que en las unitarias son los dos sustitutos—, la
/// invalidación de la caché de membresías al guardar la política, y que el
/// endpoint acepte y devuelva los literales del contrato.
/// </para>
///
/// <para>
/// El caso central es el que convierte esta etapa en una mejora o en un encierro:
/// alguien que tiene un TOTP y cuya cooperativa pasa a aceptar sólo llaves.
/// Verifica bien —su identidad está probada— y aun así no puede entrar ahí. Lo
/// que NO puede pasar es que se quede sin nada: tiene que recibir el token de
/// inscripción y la lista de lo que sí le serviría.
/// </para>
/// </summary>
public sealed class EndToEnd_PoliticaDeMetodos(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Correo = "lucia.metodos@coop.politica.test";
    private const string Clave = "Lucia-Strong-Pwd-2026!";

    [Fact]
    public async Task Con_TOTP_donde_solo_aceptan_llaves_recibe_el_token_de_inscripcion_y_no_un_rechazo()
    {
        using var http = fx.CreateClient();

        var (token, tenantPublicId) = await MontarCooperativaAsync(http);
        var secreto = await InscribirTotpAsync(http, token);

        // --- Antes de restringir: entra con su TOTP sin problema ---
        var antes = await IntentarEntrarAsync(http, secreto);
        Assert.Equal("None", antes.GetProperty("challenge").GetString());

        // --- La cooperativa pasa a aceptar SÓLO llaves ---
        var guardado = await GuardarPoliticaAsync(
            http, token, tenantPublicId, exigir: true, metodos: ["WebAuthn"]);

        // El número que evita el incidente del lunes: Lucía tiene un TOTP y
        // ninguna llave, así que la política la deja a ella fuera. Que el endpoint
        // lo diga ANTES de que ella se lo encuentre es el punto.
        Assert.Equal(1, guardado.GetProperty("miembrosSinMetodoAceptado").GetInt32());

        // --- Ahora el propio LOGIN la manda a inscribir ---
        //
        // Y no la verificación. Es mejor así, y no era lo que esperaba esta prueba
        // al escribirla: como ninguna de sus cooperativas acepta ninguno de los
        // métodos que tiene, se lo dice antes de que teclee un código que iba a
        // verificar bien para nada. La comprobación post-verificación existe igual
        // —la prueba de abajo la ejercita— para el caso en que el login sí puede
        // admitirla y es el método concreto que usó el que no vale.
        var despues = await http.PostAsJsonAsync("/api/auth/login",
            new { email = Correo, password = Clave });
        Assert.Equal(HttpStatusCode.OK, despues.StatusCode);

        var cuerpo = await LeerJsonAsync(despues);
        Assert.Equal("MfaEnrollmentRequired", cuerpo.GetProperty("challenge").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(cuerpo.GetProperty("challengeToken").GetString()),
            "sin token de inscripción se queda sin ninguna forma de arreglarlo");
        Assert.Equal("mfa-enroll", cuerpo.GetProperty("challengeTokenPurpose").GetString());

        var metodos = cuerpo.GetProperty("metodosAceptados")
            .EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(["WebAuthn"], metodos);

        // Y el token que recibió sirve de verdad para inscribir una llave: si no
        // sirviera, la salida sería decorativa.
        using var beginReq = new HttpRequestMessage(
            HttpMethod.Post, "/api/profile/mfa/webauthn/begin");
        beginReq.Headers.Authorization =
            new("Bearer", cuerpo.GetProperty("challengeToken").GetString());
        var beginResp = await http.SendAsync(beginReq);

        Assert.True(beginResp.IsSuccessStatusCode,
            $"El token de inscripción no sirvió para empezar el alta de una llave: " +
            $"{(int)beginResp.StatusCode} {await beginResp.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// La comprobación DESPUÉS de verificar, que es la que atrapa el caso que el
    /// login no puede prever: la política cambia mientras la persona está en medio
    /// del ingreso.
    ///
    /// <para>
    /// Es el mismo hueco por el que se colaría alguien que tiene los dos métodos y
    /// usa el que su cooperativa no acepta — un caso que no se puede montar en esta
    /// prueba porque inscribir una llave necesita un autenticador de verdad, y aquí
    /// no hay navegador. Restringir entre el login y el verify produce exactamente
    /// el mismo estado y sí se puede montar.
    /// </para>
    ///
    /// <para>
    /// De paso comprueba que guardar la política invalida la caché de membresías:
    /// con el TTL de 60 segundos y sin invalidación, el verify habría leído la
    /// máscara vieja y la habría dejado entrar.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Si_la_politica_cambia_entre_el_login_y_el_verify_se_le_pide_inscribir()
    {
        using var http = fx.CreateClient();
        const string correo = "olga.enmedio@coop.politica.test";

        var (token, tenantPublicId) = await MontarCooperativaAsync(
            http, correo, "tenant_politica_enmedio", "900333444", "Coop. En Medio");
        var secreto = await InscribirTotpAsync(http, token);

        // Login con la política todavía permisiva: le sale MfaRequired.
        var loginResp = await http.PostAsJsonAsync("/api/auth/login",
            new { email = correo, password = Clave });
        var login = await LeerJsonAsync(loginResp);
        Assert.Equal("MfaRequired", login.GetProperty("challenge").GetString());
        var challengeToken = login.GetProperty("challengeToken").GetString();

        // Mientras tiene la pantalla abierta, la cooperativa restringe a llaves.
        await GuardarPoliticaAsync(http, token, tenantPublicId, exigir: true, metodos: ["WebAuthn"]);

        // Su código verifica —su identidad está probada— pero ya no le abre esa
        // cooperativa. Lo que NO puede pasar es un error: se quedaría con el
        // desafío consumido y sin nada.
        using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        verifyReq.Headers.Authorization = new("Bearer", challengeToken);
        var verifyResp = await http.SendAsync(verifyReq);

        Assert.True(verifyResp.IsSuccessStatusCode,
            $"El verify no puede fallar: la identidad está probada. " +
            $"{(int)verifyResp.StatusCode} {await verifyResp.Content.ReadAsStringAsync()}");

        var verify = await LeerJsonAsync(verifyResp);
        Assert.Equal("MfaEnrollmentRequired", verify.GetProperty("challenge").GetString());
        Assert.Equal("mfa-enroll", verify.GetProperty("challengeTokenPurpose").GetString());
        Assert.Null(verify.GetProperty("accessToken").GetString());

        var metodos = verify.GetProperty("metodosAceptados")
            .EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(["WebAuthn"], metodos);
    }

    /// <summary>
    /// El tope de autenticadores contaba el total, y con dos tipos eso era un
    /// segundo candado: quien tuviera cinco apps de códigos no podía inscribir la
    /// llave que le exigen, y retirar una necesita la sesión que no consigue.
    /// </summary>
    [Fact]
    public async Task Tener_muchas_apps_de_codigos_no_impide_inscribir_una_llave()
    {
        using var http = fx.CreateClient();
        const string correo = "mario.tope@coop.politica.test";

        var (token, _) = await MontarCooperativaAsync(
            http, correo, "tenant_politica_tope", "900333222", "Coop. Tope");

        for (var i = 1; i <= 5; i++)
        {
            await InscribirTotpAsync(http, token, $"App {i}");
        }

        using var beginReq = new HttpRequestMessage(
            HttpMethod.Post, "/api/profile/mfa/webauthn/begin");
        beginReq.Headers.Authorization = new("Bearer", token);
        var beginResp = await http.SendAsync(beginReq);

        Assert.True(beginResp.IsSuccessStatusCode,
            "Cinco apps de códigos no pueden bloquear la primera llave: " +
            $"{(int)beginResp.StatusCode} {await beginResp.Content.ReadAsStringAsync()}");

        // Pero el tope de SU tipo sigue vigente: la sexta app se rechaza.
        using var sextaReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        sextaReq.Headers.Authorization = new("Bearer", token);
        var sextaResp = await http.SendAsync(sextaReq);

        Assert.Equal(HttpStatusCode.Conflict, sextaResp.StatusCode);
        Assert.Equal("Profile.Mfa.DemasiadasCredenciales",
            (await LeerJsonAsync(sextaResp)).GetProperty("code").GetString());
    }

    /// <summary>
    /// Exigir segundo factor sin aceptar ningún método encerraría a la cooperativa
    /// entera con una sola llamada, y la salida que ofrece el sistema —«inscribí
    /// uno de estos»— sería mentira. Se rechaza al escribir.
    /// </summary>
    [Fact]
    public async Task No_se_puede_exigir_MFA_sin_aceptar_ningun_metodo()
    {
        using var http = fx.CreateClient();
        const string correo = "nora.vacia@coop.politica.test";

        var (token, tenantPublicId) = await MontarCooperativaAsync(
            http, correo, "tenant_politica_vacia", "900333333", "Coop. Vacia");

        using var req = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantPublicId}/mfa-policy")
        {
            Content = JsonContent.Create(new { isRequired = true, metodosAceptados = Array.Empty<string>() }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);

        Assert.False(resp.IsSuccessStatusCode);
        Assert.Equal("Validation.TenantMfaPolicy.SinMetodos",
            (await LeerJsonAsync(resp)).GetProperty("code").GetString());
    }

    // ---------- Auxiliares ----------

    private async Task<(string Token, Guid TenantPublicId)> MontarCooperativaAsync(
        HttpClient http,
        string? correo = null,
        string esquema = "tenant_politica_metodos",
        string nit = "900333111",
        string nombre = "Coop. Politica de Metodos")
    {
        var destino = correo ?? Correo;
        var masterToken = await fx.IniciarSesionMaestroAsync(http);

        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = nombre,
                schemaName = esquema,
                nit,
                legalName = $"{nombre} SAS",
                contactEmail = $"contacto.{esquema}@coop.politica.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = destino,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registro = await LeerJsonAsync(await http.SendAsync(registerReq));
        var tenantPublicId = registro.GetProperty("tenantPublicId").GetGuid();

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

        var token = (await LeerJsonAsync(acceptResp)).GetProperty("accessToken").GetString()!;
        return (token, tenantPublicId);
    }

    private static async Task<string> InscribirTotpAsync(
        HttpClient http, string token, string label = "Teléfono")
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var beginResp = await http.SendAsync(beginReq);
        Assert.True(beginResp.IsSuccessStatusCode,
            $"Begin falló: {(int)beginResp.StatusCode} {await beginResp.Content.ReadAsStringAsync()}");

        var secreto = (await LeerJsonAsync(beginResp)).GetProperty("secretBase32").GetString()!;

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
                label,
            }),
        };
        confirmReq.Headers.Authorization = new("Bearer", token);
        var confirmResp = await http.SendAsync(confirmReq);
        Assert.True(confirmResp.IsSuccessStatusCode,
            $"Confirm falló: {(int)confirmResp.StatusCode} {await confirmResp.Content.ReadAsStringAsync()}");

        return secreto;
    }

    private static async Task<JsonElement> GuardarPoliticaAsync(
        HttpClient http, string token, Guid tenantPublicId, bool exigir, string[] metodos)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantPublicId}/mfa-policy")
        {
            Content = JsonContent.Create(new { isRequired = exigir, metodosAceptados = metodos }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"PUT mfa-policy falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        return await LeerJsonAsync(resp);
    }

    /// <summary>Login + verify con el TOTP. Devuelve el cuerpo del verify.</summary>
    private static async Task<JsonElement> IntentarEntrarAsync(
        HttpClient http, string secreto, string? correo = null)
    {
        var loginResp = await http.PostAsJsonAsync("/api/auth/login",
            new { email = correo ?? Correo, password = Clave });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var login = await LeerJsonAsync(loginResp);
        Assert.Equal("MfaRequired", login.GetProperty("challenge").GetString());

        using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        verifyReq.Headers.Authorization = new("Bearer", login.GetProperty("challengeToken").GetString());
        var verifyResp = await http.SendAsync(verifyReq);

        Assert.True(verifyResp.IsSuccessStatusCode,
            $"El verify no puede fallar: la identidad está probada. " +
            $"{(int)verifyResp.StatusCode} {await verifyResp.Content.ReadAsStringAsync()}");

        return await LeerJsonAsync(verifyResp);
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
