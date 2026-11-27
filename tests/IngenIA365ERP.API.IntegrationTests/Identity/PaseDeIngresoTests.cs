using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.API.IntegrationTests.Identity.Documento;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// Feature 027 (T042): el pase de ingreso sin segundo factor de punta a punta. Una sola historia, como la de la recuperación por correo, porque
/// el login está limitado a diez por minuto y por IP: la cooperativa enciende la opción, el administrador concede un pase al operador (y no se
/// lo puede dar a sí mismo), al operador le llega el enlace, entra con contraseña y pase sin el código y vuelve a entrar con el mismo pase; la
/// sesión queda atada a la cooperativa; entrar con la app anula el pase y el enlace ya no sirve.
/// </summary>
public sealed class PaseDeIngresoTests(CentralIdentityApiFixture fx) : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Clave = "Pase-Strong-Pwd-2026!";
    private const string CorreoAdmin = "admin.pase@coop.pase.test";
    private const string CorreoOperador = "operador.pase@coop.pase.test";

    [Fact]
    public async Task El_pase_se_concede_al_correo_sirve_la_jornada_ata_la_sesion_y_se_gasta_al_entrar_con_la_app()
    {
        using var http = fx.CreateClient();
        var (admin, tenantId) = await MontarAsync(http);
        await InscribirTotpAsync(http, admin);

        // El operador, invitado por el administrador, con su app inscrita.
        var invitacion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{tenantId}/invitations",
            DocumentoDePrueba.Invitacion(CorreoOperador));
        Assert.True(invitacion.IsSuccessStatusCode, await invitacion.Content.ReadAsStringAsync());
        var operador = await AceptarAsync(http, CorreoOperador);
        var secretoOperador = await InscribirTotpAsync(http, operador);
        var usuarioOperador = await UsuarioAsync(http, admin, CorreoOperador);
        var usuarioAdmin = await UsuarioAsync(http, admin, CorreoAdmin);

        // De fábrica la opción está apagada: no se concede.
        var apagado = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuarioOperador}/login-passes",
            new { hours = 12, reason = "Dejó el celular en casa" });
        Assert.Equal("LoginPass.Disabled", (await LeerAsync(apagado)).GetProperty("code").GetString());

        var politica = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/tenants/{tenantId}/mfa-policy",
            new { isRequired = true, permitirPasesDeIngreso = true, horasMaximasDelPase = 12 });
        Assert.True(politica.IsSuccessStatusCode, await politica.Content.ReadAsStringAsync());

        // A sí mismo no (y además es administrador: sólo el maestro).
        var asiMismo = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuarioAdmin}/login-passes",
            new { hours = 12, reason = "Me lo doy a mí mismo" });
        Assert.Equal("LoginPass.Self", (await LeerAsync(asiMismo)).GetProperty("code").GetString());

        // Se concede: la respuesta no trae el enlace; el correo sí, sólo al operador.
        var correosAntes = fx.Emails.Sent.Count;
        var concedido = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuarioOperador}/login-passes",
            new { hours = 12, reason = "Dejó el celular en casa" });
        var cuerpo = await concedido.Content.ReadAsStringAsync();
        Assert.True(concedido.IsSuccessStatusCode, cuerpo);
        Assert.DoesNotContain("pase=", cuerpo);
        Assert.True((await LeerAsync(concedido)).GetProperty("emailSent").GetBoolean());

        var correoDelPase = fx.Emails.Sent.Skip(correosAntes).Single(m => m.To == CorreoOperador);
        var pase = Regex.Match(correoDelPase.BodyHtml, @"/security/login\?pase=([A-Za-z0-9_\-%]+)");
        Assert.True(pase.Success, "el correo trae el enlace del pase");
        var enlace = $"https://erp.test/security/login?pase={pase.Groups[1].Value}";
        Assert.DoesNotContain(fx.Emails.Sent.Skip(correosAntes), m => m.To == CorreoAdmin && m.BodyHtml.Contains("pase="));

        // Entra con contraseña y pase, sin código: sesión de la cooperativa, marcada como de pase.
        var entrada = await CanjearAsync(http, enlace);
        Assert.Equal(HttpStatusCode.OK, entrada.Status);
        Assert.Equal("None", entrada.Body.GetProperty("challenge").GetString());
        var sesion = entrada.Body.GetProperty("accessToken").GetString()!;
        var yo = await GetAsync(http, sesion, "/api/auth/me");
        Assert.Equal(tenantId, yo.GetProperty("loginPassTenantPublicId").GetGuid());

        // Atada a su cooperativa: no se cambia a otra.
        var otra = await EnviarAsync(http, sesion, HttpMethod.Post, "/api/sessions/switch-tenant", new { tenantPublicId = Guid.NewGuid() });
        Assert.Equal("Identity.LoginPass.TenantLocked", (await LeerAsync(otra)).GetProperty("code").GetString());

        // El mismo enlace sirve otra vez (la sesión se cerró, por ejemplo).
        var otraVez = await CanjearAsync(http, enlace);
        Assert.Equal(HttpStatusCode.OK, otraVez.Status);

        var pases = await GetAsync(http, admin, $"/api/admin/users/{usuarioOperador}/login-passes");
        var vigente = pases.EnumerateArray().First();
        Assert.Equal("Active", vigente.GetProperty("status").GetString());
        Assert.Equal(2, vigente.GetProperty("useCount").GetInt32());

        // Entrar con la app gasta el pase: el enlace ya no sirve y no dice por qué.
        await EntrarConTotpAsync(http, CorreoOperador, secretoOperador);
        var gastado = await CanjearAsync(http, enlace);
        Assert.Equal(HttpStatusCode.Unauthorized, gastado.Status);
        Assert.Equal("Identity.LoginPass.Invalid", gastado.Body.GetProperty("code").GetString());

        pases = await GetAsync(http, admin, $"/api/admin/users/{usuarioOperador}/login-passes");
        Assert.Equal("entro-con-segundo-factor", pases.EnumerateArray().First().GetProperty("voidReason").GetString());
    }

    // ---------- Auxiliares ----------

    private async Task<(string Token, Guid TenantId)> MontarAsync(HttpClient http)
    {
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var registro = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = "Coop. Pase de Ingreso",
            schemaName = "tenant_pase_ingreso",
            nit = "900777333",
            legalName = "Coop. Pase de Ingreso SAS",
            contactEmail = "contacto.pase@coop.pase.test",
            planType = "Basic",
            maxUsers = 50,
            storageLimitMb = 5120,
            firstAdminEmail = CorreoAdmin,
            firstAdminDocumentType = "CC", firstAdminDocumentNumber = DocumentoDePrueba.Numero(CorreoAdmin),
            firstAdminDocumentNumberConfirmation = DocumentoDePrueba.Numero(CorreoAdmin),
            firstAdminFirstName = "Administrador", firstAdminLastName = "De Prueba",
        });
        Assert.True(registro.IsSuccessStatusCode, await registro.Content.ReadAsStringAsync());
        var tenantId = (await LeerAsync(registro)).GetProperty("tenantPublicId").GetGuid();
        return (await AceptarAsync(http, CorreoAdmin), tenantId);
    }

    private async Task<string> AceptarAsync(HttpClient http, string correo)
    {
        var invitacion = fx.Emails.Sent.Last(m => m.To == correo);
        var token = Regex.Match(invitacion.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(token.Success);
        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = token.Groups[1].Value,
            documentNumber = DocumentoDePrueba.Numero(correo),
            registration = new { password = Clave },
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
        return (await LeerAsync(resp)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> InscribirTotpAsync(HttpClient http, string token)
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var secreto = (await LeerAsync(await http.SendAsync(beginReq))).GetProperty("secretBase32").GetString()!;
        var confirmar = await EnviarAsync(http, token, HttpMethod.Post, "/api/profile/mfa/confirm",
            new { code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp() });
        Assert.True(confirmar.IsSuccessStatusCode, await confirmar.Content.ReadAsStringAsync());
        return secreto;
    }

    private static async Task<Guid> UsuarioAsync(HttpClient http, string admin, string correo)
    {
        var usuarios = await GetAsync(http, admin, $"/api/admin/users?search={Uri.EscapeDataString(correo)}&pageSize=50");
        return usuarios.GetProperty("items").EnumerateArray()
            .Single(u => string.Equals(u.GetProperty("email").GetString(), correo, StringComparison.OrdinalIgnoreCase))
            .GetProperty("publicId").GetGuid();
    }

    private static async Task<string> DesafioAsync(HttpClient http, string correo)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email = correo, password = Clave });
        var cuerpo = await LeerAsync(resp);
        Assert.Equal("MfaRequired", cuerpo.GetProperty("challenge").GetString());
        return cuerpo.GetProperty("challengeToken").GetString()!;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> CanjearAsync(HttpClient http, string enlace)
    {
        var desafio = await DesafioAsync(http, CorreoOperador);
        var resp = await EnviarAsync(http, desafio, HttpMethod.Post, "/api/auth/mfa/pass", new { pass = enlace });
        return (resp.StatusCode, await LeerAsync(resp));
    }

    private static async Task EntrarConTotpAsync(HttpClient http, string correo, string secreto)
    {
        var desafio = await DesafioAsync(http, correo);
        var resp = await EnviarAsync(http, desafio, HttpMethod.Post, "/api/auth/mfa/verify",
            new { code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp() });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var req = new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new("Bearer", token);
        return await http.SendAsync(req);
    }

    private static async Task<JsonElement> GetAsync(HttpClient http, string token, string ruta)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ruta);
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode, $"{ruta}: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return await LeerAsync(resp);
    }

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw) ? default : JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
