using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T091–T098): lo que repiten las e2e de identidad con documento —registrar una cooperativa,
/// invitar con documento, aceptar e ingresar— por HTTP, como lo hace la pantalla. Cada prueba usa su propio
/// rango de documentos y su propio esquema: corren en la colección compartida.
/// </summary>
internal sealed class Escenario017(CentralIdentityApiFixture fx)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string Clave = "Clave-Documento-2026!";

    public CentralIdentityApiFixture Fx => fx;

    private static int _origen;

    /// <summary>
    /// Una IP de origen distinta por pedido (<c>CF-Connecting-IP</c>, la que cuenta el limitador del ingreso, 10 por
    /// minuto). Sin esto las e2e de la 017, que entran muchas veces, agotan el cupo de la colección compartida y las
    /// demás clases reciben 429. Es lo mismo que hacen <c>UsuariosDeCartera</c> y <c>LibranzaE2E</c>.
    /// </summary>
    public static string OtraIp()
    {
        var n = Interlocked.Increment(ref _origen);
        return $"10.254.{n / 250 % 250}.{n % 250 + 1}";
    }

    /// <summary>El cliente de una prueba, con su propia IP de origen para lo que no pase por <see cref="IngresarAsync"/>.</summary>
    public static HttpClient Cliente(CentralIdentityApiFixture fx)
    {
        var http = fx.CreateClient();
        http.DefaultRequestHeaders.Add("CF-Connecting-IP", OtraIp());
        return http;
    }

    public async Task<Guid> CooperativaAsync(HttpClient http, string maestro, string esquema, string nit, string? correoDelAdministrador = null)
    {
        var r = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = $"Coop {esquema}", schemaName = esquema, nit, legalName = $"Coop {esquema} SAS",
            contactEmail = $"contacto@{esquema}.test", planType = "Basic", maxUsers = 50, storageLimitMb = 5120,
            firstAdminEmail = correoDelAdministrador ?? $"admin@{esquema}.test",
        });
        if (r.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"No se registró {esquema}: {await r.Content.ReadAsStringAsync()}");
        return (await LeerAsync(r)).GetProperty("tenantPublicId").GetGuid();
    }

    /// <summary>El maestro invita con documento; devuelve el cuerpo de la respuesta.</summary>
    public async Task<JsonElement> InvitarAsync(
        HttpClient http, string maestro, Guid cooperativa, string correo, string tipo, string numero,
        string nombre = "Persona", string apellido = "De Prueba")
    {
        var r = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/invitations", new
        {
            tenantPublicId = cooperativa, email = correo, inviteAsTenantAdmin = false,
            documentType = tipo, documentNumber = numero, documentNumberConfirmation = numero,
            firstName = nombre, lastName = apellido,
        });
        if (r.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"No se invitó a {correo}: {await r.Content.ReadAsStringAsync()}");
        return await LeerAsync(r);
    }

    /// <summary>Acepta la última invitación que llegó a <paramref name="correo"/> y devuelve la respuesta.</summary>
    public async Task<HttpResponseMessage> AceptarAsync(HttpClient http, string correo, string? numero, bool nueva = true) =>
        await ConIpPropiaAsync(http, "/api/invitations/accept", nueva
            ? new { token = TokenDelUltimoCorreo(correo), documentNumber = numero, registration = new { password = Clave } } as object
            : new { token = TokenDelUltimoCorreo(correo), documentNumber = numero, existingCredentials = new { password = Clave } });

    /// <summary>Invita, acepta y devuelve la sesión que dio la aceptación.</summary>
    public async Task<JsonElement> PersonaAsync(HttpClient http, string maestro, Guid cooperativa, string correo, string tipo, string numero)
    {
        await InvitarAsync(http, maestro, cooperativa, correo, tipo, numero);
        var r = await AceptarAsync(http, correo, numero);
        if (r.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"No se activó {correo}: {await r.Content.ReadAsStringAsync()}");
        return await LeerAsync(r);
    }

    public static Task<HttpResponseMessage> IngresarAsync(HttpClient http, string identificador, string clave = Clave) =>
        ConIpPropiaAsync(http, "/api/auth/login", new { identifier = identificador, password = clave });

    public static Task<HttpResponseMessage> RenovarAsync(HttpClient http, string refresh) =>
        ConIpPropiaAsync(http, "/api/auth/refresh", new { refreshToken = refresh });

    private static async Task<HttpResponseMessage> ConIpPropiaAsync(HttpClient http, string url, object cuerpo)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Remove("CF-Connecting-IP");
        req.Headers.Add("CF-Connecting-IP", OtraIp());
        return await http.SendAsync(req);
    }

    public static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null)
    {
        using var req = new HttpRequestMessage(metodo, url) { Content = cuerpo is null ? null : JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new("Bearer", token);
        return await http.SendAsync(req);
    }

    public string TokenDelUltimoCorreo(string correo)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => string.Equals(m.To, correo, StringComparison.OrdinalIgnoreCase));
        if (mensaje is null) throw new InvalidOperationException($"No llegó ningún correo a {correo}.");
        var m = Regex.Match(mensaje.BodyHtml, @"token=([A-Za-z0-9_\-%]+)");
        if (!m.Success) throw new InvalidOperationException($"El último correo a {correo} no trae token.");
        return Uri.UnescapeDataString(m.Groups[1].Value);
    }

    public static async Task<JsonElement> LeerAsync(HttpResponseMessage r) =>
        JsonSerializer.Deserialize<JsonElement>(await r.Content.ReadAsStringAsync(), Json);

    public static async Task<string?> CodigoAsync(HttpResponseMessage r)
    {
        var cuerpo = await r.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        var json = JsonSerializer.Deserialize<JsonElement>(cuerpo, Json);
        return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c) ? c.GetString() : null;
    }
}
