using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T092, T094, T095, T097, parte del MVP): de punta a punta por HTTP contra el motor real.
/// <list type="number">
///   <item>Una cuenta que entra con correo, como siempre, sigue entrando igual (regla de oro).</item>
///   <item>El maestro invita con documento; la vista previa no lo muestra y pide confirmarlo.</item>
///   <item>Un número equivocado no activa; el correcto activa y devuelve el usuario.</item>
///   <item>La persona entra con su usuario, con o sin separadores; un inexistente responde igual que
///         una clave errada.</item>
///   <item>Otra cooperativa invita el mismo documento con otro correo: la invitación sale al correo de
///         la identidad y el mensaje al que invita es el mismo.</item>
/// </list>
/// </summary>
public sealed class IngresoConDocumentoE2ETests(CentralIdentityApiFixture fx) : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Invitacion_con_documento_activacion_e_ingreso_por_usuario()
    {
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);

        var coopA = await RegistrarCooperativaAsync(http, maestro, "Coop Documento A", "tenant_doc_a", "900170001");
        var coopB = await RegistrarCooperativaAsync(http, maestro, "Coop Documento B", "tenant_doc_b", "900170002");

        // 1) Regla de oro: el administrador maestro (cuenta de antes, correo) entra igual.
        Assert.False(string.IsNullOrWhiteSpace(maestro));

        // 2) Invitar con documento (CE, con puntos y digitado dos veces).
        const string correo = "luz.extranjera@coop.documento.test";
        var invitacion = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/invitations", new
        {
            tenantPublicId = coopA, email = correo, inviteAsTenantAdmin = false,
            documentType = "CE", documentNumber = "987.654", documentNumberConfirmation = "987654",
            firstName = "Luz", lastName = "Extranjera", mobilePhone = "3001112233",
        });
        Assert.Equal(HttpStatusCode.OK, invitacion.StatusCode);
        var emitida = await LeerAsync(invitacion);
        var mensajeComun = emitida.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(mensajeComun));

        var token = TokenDelUltimoCorreo(correo);

        // 3) La vista previa pide el documento y no lo muestra.
        var preview = await LeerAsync(await http.GetAsync($"/api/invitations/{Uri.EscapeDataString(token)}/preview"));
        Assert.True(preview.GetProperty("isValid").GetBoolean());
        Assert.True(preview.GetProperty("requiresDocument").GetBoolean());
        Assert.False(preview.GetProperty("isExistingCentralUser").GetBoolean());
        Assert.DoesNotContain("987654", preview.GetRawText(), StringComparison.Ordinal);

        // 4) Número equivocado: no activa.
        const string clave = "Luz-Extranjera-Clave-2026";
        var mal = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token, documentNumber = "111111", registration = new { password = clave },
        });
        Assert.NotEqual(HttpStatusCode.OK, mal.StatusCode);
        Assert.Equal("Invitation.DocumentMismatch", (await LeerAsync(mal)).GetProperty("code").GetString());

        // 5) Número correcto: activa y devuelve el usuario.
        var bien = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token, documentNumber = "987654", registration = new { password = clave },
        });
        Assert.Equal(HttpStatusCode.OK, bien.StatusCode);
        Assert.Equal("CE987654", (await LeerAsync(bien)).GetProperty("documentUsername").GetString());

        // 6) Entra con su usuario, con separadores y en minúsculas, y también con su correo.
        foreach (var identificador in new[] { "CE987654", "ce-987.654", correo })
        {
            var ingreso = await http.PostAsJsonAsync("/api/auth/login", new { identifier = identificador, password = clave });
            Assert.True(ingreso.StatusCode == HttpStatusCode.OK, $"No entró con «{identificador}»: {await ingreso.Content.ReadAsStringAsync()}");
            Assert.Equal(coopA, (await LeerAsync(ingreso)).GetProperty("activeTenantPublicId").GetGuid());
        }

        // 7) Clave errada e identificador inexistente responden lo mismo, con la pista.
        var errada = await http.PostAsJsonAsync("/api/auth/login", new { identifier = "CE987654", password = "otra-clave-cualquiera" });
        var inexistente = await http.PostAsJsonAsync("/api/auth/login", new { identifier = "PA00000000", password = "otra-clave-cualquiera" });
        Assert.Equal(HttpStatusCode.Unauthorized, errada.StatusCode);
        Assert.Equal(errada.StatusCode, inexistente.StatusCode);
        var cuerpoErrada = await LeerAsync(errada);
        var cuerpoInexistente = await LeerAsync(inexistente);
        Assert.Equal(cuerpoErrada.GetProperty("code").GetString(), cuerpoInexistente.GetProperty("code").GetString());
        Assert.Equal(cuerpoErrada.GetProperty("message").GetString(), cuerpoInexistente.GetProperty("message").GetString());

        // 8) Otra cooperativa invita el mismo documento con otro correo: sale al de la identidad, mismo mensaje.
        const string otroCorreo = "otra.direccion@coop.documento.test";
        var antes = fx.Emails.Sent.Count(m => m.To == correo);
        var segunda = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/invitations", new
        {
            tenantPublicId = coopB, email = otroCorreo, inviteAsTenantAdmin = false,
            documentType = "CE", documentNumber = "987654", documentNumberConfirmation = "987654",
            firstName = "Otro", lastName = "Nombre",
        });
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(mensajeComun, (await LeerAsync(segunda)).GetProperty("message").GetString());
        Assert.Equal(antes + 1, fx.Emails.Sent.Count(m => m.To == correo));
        Assert.DoesNotContain(fx.Emails.Sent, m => m.To == otroCorreo);
    }

    [Fact]
    public async Task El_correo_de_otra_persona_se_rechaza_al_invitar()
    {
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await RegistrarCooperativaAsync(http, maestro, "Coop Documento C", "tenant_doc_c", "900170003");

        var primera = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/invitations", new
        {
            tenantPublicId = coop, email = "duenia.del.correo@coop.documento.test", inviteAsTenantAdmin = false,
            documentType = "CC", documentNumber = "52111222", documentNumberConfirmation = "52111222",
            firstName = "Dueña", lastName = "Correo",
        });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var otra = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/invitations", new
        {
            tenantPublicId = coop, email = "duenia.del.correo@coop.documento.test", inviteAsTenantAdmin = false,
            documentType = "CC", documentNumber = "79333444", documentNumberConfirmation = "79333444",
            firstName = "Otra", lastName = "Persona",
        });
        Assert.NotEqual(HttpStatusCode.OK, otra.StatusCode);
        var cuerpo = await LeerAsync(otra);
        Assert.Equal("Invitation.EmailTaken", cuerpo.GetProperty("code").GetString());
        Assert.Equal("Correo ya registrado.", cuerpo.GetProperty("message").GetString());
    }

    private async Task<Guid> RegistrarCooperativaAsync(HttpClient http, string maestro, string nombre, string esquema, string nit)
    {
        var r = await EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = nombre, schemaName = esquema, nit, legalName = nombre + " SAS",
            contactEmail = $"contacto@{esquema}.test", planType = "Basic", maxUsers = 50, storageLimitMb = 5120,
            firstAdminEmail = $"admin@{esquema}.test",
            firstAdminDocumentType = "CC", firstAdminDocumentNumber = DocumentoDePrueba.Numero($"admin@{esquema}.test"),
            firstAdminDocumentNumberConfirmation = DocumentoDePrueba.Numero($"admin@{esquema}.test"),
            firstAdminFirstName = "Administrador", firstAdminLastName = "De Prueba",
        });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await LeerAsync(r)).GetProperty("tenantPublicId").GetGuid();
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object cuerpo)
    {
        using var req = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new("Bearer", token);
        return await http.SendAsync(req);
    }

    private string TokenDelUltimoCorreo(string correo)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        Assert.NotNull(mensaje);
        var m = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(m.Success, "El correo de invitación no trae token.");
        return m.Groups[1].Value;
    }

    private static async Task<JsonElement> LeerAsync(HttpResponseMessage r) =>
        JsonSerializer.Deserialize<JsonElement>(await r.Content.ReadAsStringAsync(), Json);
}
