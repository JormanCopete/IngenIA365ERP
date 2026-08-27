using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Varios autenticadores por persona, de punta a punta.
///
/// <para>
/// Recorre lo que hace alguien de verdad: inscribe el teléfono, luego el
/// escritorio, entra con cualquiera de los dos, y retira el que perdió. Cada paso
/// afirma algo que se podía romper en silencio:
/// </para>
///
/// <list type="bullet">
/// <item>Inscribir el segundo NO borra el primero — el escritor añadía en sitio y
/// habría sobrescrito.</item>
/// <item>Inscribir el segundo NO devuelve códigos de recuperación nuevos, porque
/// emitirlos invalida los que la persona guardó al inscribir el primero.</item>
/// <item>El código de CUALQUIERA de los dos abre la sesión: el ingreso los prueba
/// todos, no elige uno.</item>
/// <item>Y el que se revoca deja de servir. Esto es lo que más importa: la columna
/// heredada conserva su valor tras una baja lógica, así que sin la condición
/// correcta el autenticador retirado habría seguido entrando.</item>
/// </list>
/// </summary>
public sealed class EndToEnd_VariosAutenticadores(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Correo = "rosa.dosfactores@coop.varios.test";
    private const string Clave = "Rosa-Strong-Pwd-2026!";

    [Fact]
    public async Task Dos_autenticadores_sirven_los_dos_y_el_revocado_deja_de_servir()
    {
        using var http = fx.CreateClient();

        // --- Montaje: cooperativa con Rosa dentro ---
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Varios Autenticadores",
                schemaName = "tenant_varios_auth",
                nit = "900111333",
                legalName = "Coop. Varios Autenticadores SAS",
                contactEmail = "contacto@coop.varios.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = Correo,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(registerReq)).StatusCode);

        var token = await AceptarInvitacionAsync(http);

        // --- 1) El teléfono. Es el primero: sí trae códigos de recuperación ---
        var (telefono, codigosDelPrimero) = await InscribirAsync(http, token, "Teléfono");
        Assert.NotEmpty(codigosDelPrimero);

        // --- 2) El escritorio ---
        var (escritorio, codigosDelSegundo) = await InscribirAsync(http, token, "Escritorio");

        // No los regenera. Si lo hiciera, los diez códigos que Rosa anotó en un
        // papel al inscribir el teléfono habrían dejado de valer sin avisarle, y
        // se enteraría el día que los necesita.
        Assert.Empty(codigosDelSegundo);

        // --- 3) Los dos están, con su nombre ---
        var credenciales = await ListarAsync(http, token);
        Assert.Equal(2, credenciales.Count);
        Assert.Contains(credenciales, c => c.GetProperty("label").GetString() == "Teléfono");
        Assert.Contains(credenciales, c => c.GetProperty("label").GetString() == "Escritorio");

        // --- 4) El código de CUALQUIERA de los dos abre la sesión ---
        Assert.True(await PuedeEntrarConAsync(http, telefono),
            "El primer autenticador debe seguir sirviendo tras inscribir el segundo.");
        Assert.True(await PuedeEntrarConAsync(http, escritorio),
            "El segundo autenticador debe servir: el ingreso prueba todas las credenciales.");

        // --- 5) Rosa pierde el teléfono y lo retira ---
        var idTelefono = credenciales
            .Single(c => c.GetProperty("label").GetString() == "Teléfono")
            .GetProperty("publicId").GetGuid();

        using var revocarReq = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/profile/mfa/credentials/{idTelefono}");
        revocarReq.Headers.Authorization = new("Bearer", token);
        var revocarResp = await http.SendAsync(revocarReq);
        Assert.True(revocarResp.IsSuccessStatusCode,
            $"Revocar falló: {(int)revocarResp.StatusCode} {await revocarResp.Content.ReadAsStringAsync()}");

        Assert.Single(await ListarAsync(http, token));

        // --- 6) LO QUE MÁS IMPORTA ---
        //
        // El escritorio sigue entrando; el teléfono retirado, NO. Sin la condición
        // correcta en el modo compatibilidad, la columna heredada —que conserva su
        // valor tras una baja lógica— habría resucitado el teléfono que Rosa
        // acababa de dar de baja porque lo perdió.
        Assert.True(await PuedeEntrarConAsync(http, escritorio),
            "El autenticador que se conserva debe seguir sirviendo.");
        Assert.False(await PuedeEntrarConAsync(http, telefono),
            "El autenticador REVOCADO no puede seguir abriendo la sesión.");
    }

    [Fact]
    public async Task No_se_puede_retirar_la_ultima_credencial_si_la_cooperativa_exige_mfa()
    {
        using var http = fx.CreateClient();

        const string correo = "beto.ultimo@coop.varios.test";
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Ultimo Factor",
                schemaName = "tenant_ultimo_factor",
                nit = "900111444",
                legalName = "Coop. Ultimo Factor SAS",
                contactEmail = "contacto2@coop.varios.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = correo,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registro = await LeerJsonAsync(await http.SendAsync(registerReq));
        var tenantPublicId = registro.GetProperty("tenantPublicId").GetGuid();

        var token = await AceptarInvitacionAsync(http, correo);
        await InscribirAsync(http, token, "Único", correo);

        // La cooperativa exige segundo factor.
        using var policyReq = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantPublicId}/mfa-policy")
        {
            Content = JsonContent.Create(new { isRequired = true }),
        };
        policyReq.Headers.Authorization = new("Bearer", token);
        Assert.True((await http.SendAsync(policyReq)).IsSuccessStatusCode);

        var credenciales = await ListarAsync(http, token, correo);
        var idUnica = credenciales.Single().GetProperty("publicId").GetGuid();

        using var revocarReq = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/profile/mfa/credentials/{idUnica}");
        revocarReq.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(revocarReq);

        // Prohibido, no «petición mal formada»: es una acción que no le está
        // permitida, y el contrato promete 403.
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("Profile.Mfa.RequiredByTenantPolicy",
            (await LeerJsonAsync(resp)).GetProperty("code").GetString());

        // Y sigue ahí: un rechazo no puede dejar el estado a medias.
        Assert.Single(await ListarAsync(http, token, correo));
    }

    // ---------- Auxiliares ----------

    private async Task<string> AceptarInvitacionAsync(HttpClient http, string? correo = null)
    {
        var destino = correo ?? Correo;
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == destino);
        Assert.NotNull(mensaje);
        var match = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, $"El correo de invitación a {destino} no trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = Clave },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    /// <summary>Inscribe un autenticador y devuelve su secreto y los códigos que entregó.</summary>
    private static async Task<(string Secreto, List<string> Codigos)> InscribirAsync(
        HttpClient http, string token, string label, string? correo = null)
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var beginResp = await http.SendAsync(beginReq);
        Assert.True(beginResp.IsSuccessStatusCode,
            $"Begin falló: {(int)beginResp.StatusCode} {await beginResp.Content.ReadAsStringAsync()}");

        var begin = await LeerJsonAsync(beginResp);
        var secreto = begin.GetProperty("secretBase32").GetString()!;

        // El QR viaja incrustado, listo para un <img src>.
        Assert.StartsWith("data:image/png;base64,", begin.GetProperty("qrPngDataUri").GetString());

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

        var confirm = await LeerJsonAsync(confirmResp);
        var codigos = confirm.TryGetProperty("recoveryCodes", out var rc) && rc.ValueKind == JsonValueKind.Array
            ? rc.EnumerateArray().Select(c => c.GetString()!).ToList()
            : [];

        return (secreto, codigos);
    }

    private static async Task<List<JsonElement>> ListarAsync(
        HttpClient http, string token, string? correo = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/profile/mfa/credentials");
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"Listar falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");

        return (await LeerJsonAsync(resp)).GetProperty("credenciales").EnumerateArray().ToList();
    }

    private static async Task<bool> PuedeEntrarConAsync(HttpClient http, string secreto)
    {
        var loginResp = await http.PostAsJsonAsync("/api/auth/login",
            new { email = Correo, password = Clave });
        if (loginResp.StatusCode != HttpStatusCode.OK) return false;

        var login = await LeerJsonAsync(loginResp);
        if (login.GetProperty("challenge").GetString() != "MfaRequired") return false;

        using var verifyReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        verifyReq.Headers.Authorization = new("Bearer", login.GetProperty("challengeToken").GetString());
        return (await http.SendAsync(verifyReq)).StatusCode == HttpStatusCode.OK;
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
