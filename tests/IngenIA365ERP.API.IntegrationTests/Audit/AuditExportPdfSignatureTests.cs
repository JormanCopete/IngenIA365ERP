using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T084 — El PDF exportado lleva HMAC-SHA256 y el verificador SaaS lo confirma.
///
/// <para>
/// <b>Qué estaba roto.</b> Esta prueba pegaba con un cliente <b>anónimo</b> a un
/// endpoint autenticado, recibía 401, y lo afirmaba como «estado RED esperado»
/// antes de hacer <c>return</c>. Se contaba como <b>pasada</b>. Los dos
/// endpoints llevaban meses existiendo —<c>AuditLogModule</c> y
/// <c>AuditVerificationModule</c>—, así que lo viejo era la prueba: la
/// verificación HMAC de la exportación <b>no se había ejecutado nunca</b>.
/// </para>
///
/// <para>
/// Y estaba mal de una segunda forma que el 401 tapaba: enviaba el HMAC como
/// parte del multipart, cuando el verificador lo lee de las <b>cabeceras</b>
/// <c>X-Audit-Hmac</c> y <c>X-Audit-Key-Version</c>. Aunque se hubiera
/// autenticado, habría fallado.
/// </para>
///
/// <para>
/// Hacen falta dos identidades y no una: exportar es de la cooperativa
/// (<c>AuditLog.Export</c>) y verificar es de la plataforma
/// (<c>Saas.AuditLog.Verify</c>). Que el verificador viva fuera del alcance de
/// quien exporta es justamente lo que hace que la firma signifique algo.
/// </para>
/// </summary>
public class AuditExportPdfSignatureTests(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private const string CorreoAdmin = "auditor@coop.firma.test";
    private const string ClaveAdmin = "Auditor-Firma-2026!";

    [Fact]
    public async Task El_pdf_exportado_pasa_la_verificacion_de_firma()
    {
        using var http = fx.CreateClient();

        // Dos identidades, cada una con lo suyo.
        var tokenMaestro = await fx.IniciarSesionMaestroAsync(http);
        await RegistrarCooperativaAsync(http, tokenMaestro);
        var tokenAdmin = await AceptarInvitacionAsync(http);

        // 1) Exportar. El rango es ancho a propósito: lo que importa es que haya
        //    bytes que firmar, y el alta de la cooperativa más la aceptación de
        //    la invitación ya dejaron eventos en esta colección.
        var desde = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");
        var hasta = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        using var exportReq = new HttpRequestMessage(
            HttpMethod.Get, $"/api/audit/logs/export.pdf?from={desde}&to={hasta}");
        exportReq.Headers.Authorization = new("Bearer", tokenAdmin);
        var exportResp = await http.SendAsync(exportReq);

        // Sin rama de escape: si esto no es 200, la prueba falla y se ve. Era el
        // `return` de aquí lo que la mantenía verde sin comprobar nada.
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "el export existe y el admin de la cooperativa tiene AuditLog.Export; " +
            $"respondió {(int)exportResp.StatusCode} con «{await exportResp.Content.ReadAsStringAsync()}»");

        exportResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var hmac = exportResp.Headers.GetValues("X-Audit-Hmac").FirstOrDefault();
        var versionClave = exportResp.Headers.GetValues("X-Audit-Key-Version").FirstOrDefault();
        hmac.Should().NotBeNullOrEmpty();
        versionClave.Should().NotBeNullOrEmpty();

        var pdf = await exportResp.Content.ReadAsByteArrayAsync();
        pdf.Should().NotBeEmpty();

        // 2) Verificar, con el maestro: el HMAC va en cabeceras, no en el cuerpo.
        var verificacion = await VerificarAsync(http, tokenMaestro, pdf, hmac!, versionClave!);

        verificacion.GetProperty("valid").GetBoolean().Should().BeTrue(
            "el PDF no se tocó entre exportar y verificar");
        verificacion.GetProperty("computedHmac").GetString().Should().Be(hmac);
        verificacion.GetProperty("keyVersion").GetString().Should().Be(versionClave);
    }

    [Fact]
    public async Task Un_pdf_alterado_no_pasa_la_verificacion()
    {
        // La otra mitad, y la que de verdad demuestra que la firma sirve: si un
        // byte cambiado siguiera dando valid=true, el HMAC sería decorativo.
        using var http = fx.CreateClient();

        var tokenMaestro = await fx.IniciarSesionMaestroAsync(http);
        await RegistrarCooperativaAsync(http, tokenMaestro);
        var tokenAdmin = await AceptarInvitacionAsync(http);

        var desde = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");
        var hasta = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        using var exportReq = new HttpRequestMessage(
            HttpMethod.Get, $"/api/audit/logs/export.pdf?from={desde}&to={hasta}");
        exportReq.Headers.Authorization = new("Bearer", tokenAdmin);
        var exportResp = await http.SendAsync(exportReq);
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var hmac = exportResp.Headers.GetValues("X-Audit-Hmac").First();
        var versionClave = exportResp.Headers.GetValues("X-Audit-Key-Version").First();
        var pdf = await exportResp.Content.ReadAsByteArrayAsync();

        // Un solo byte, en medio del documento.
        var alterado = (byte[])pdf.Clone();
        alterado[alterado.Length / 2] ^= 0xFF;

        var verificacion = await VerificarAsync(http, tokenMaestro, alterado, hmac, versionClave);

        verificacion.GetProperty("valid").GetBoolean().Should().BeFalse(
            "cambiar un byte del PDF tiene que invalidar la firma");
        verificacion.GetProperty("computedHmac").GetString().Should().NotBe(hmac);
    }

    // === Ayudantes (mismo patrón que AuditPerformanceTests) ===

    private static async Task<JsonElement> VerificarAsync(
        HttpClient http, string tokenMaestro, byte[] pdf, string hmac, string versionClave)
    {
        using var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(pdf);
        parte.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        contenido.Add(parte, "file", "audit-export.pdf");

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/audit/verify")
        {
            Content = contenido,
        };
        req.Headers.Authorization = new("Bearer", tokenMaestro);
        req.Headers.Add("X-Audit-Hmac", hmac);
        req.Headers.Add("X-Audit-Key-Version", versionClave);

        var resp = await http.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"el verificador respondió «{await resp.Content.ReadAsStringAsync()}»");

        return await LeerJsonAsync(resp);
    }

    private async Task RegistrarCooperativaAsync(HttpClient http, string tokenMaestro)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Firma de Auditoria",
                schemaName = "tenant_auditoria_firma",
                subdomain = "tenant_auditoria_firma",
                nit = "900999222",
                legalName = "Coop. Firma de Auditoria SAS",
                contactEmail = "contacto@coop.firma.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = CorreoAdmin,
            }),
        };
        req.Headers.Authorization = new("Bearer", tokenMaestro);
        var resp = await http.SendAsync(req);

        // Idempotente entre los dos [Fact] de esta clase: comparten fixture, así
        // que el segundo encuentra la cooperativa ya creada.
        if (resp.StatusCode == HttpStatusCode.OK) return;
        resp.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity },
            $"alta de cooperativa: «{await resp.Content.ReadAsStringAsync()}»");
    }

    private async Task<string> AceptarInvitacionAsync(HttpClient http)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == CorreoAdmin);

        // En el segundo [Fact] la invitación ya se aceptó: se entra por login.
        if (mensaje is null)
        {
            return await LoginAsync(http, CorreoAdmin, ClaveAdmin);
        }

        var token = Regex.Match(mensaje.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(token.Success, "El correo de invitación no contiene token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = token.Groups[1].Value,
            registration = new { password = ClaveAdmin },
        });

        if (resp.StatusCode != HttpStatusCode.OK)
        {
            return await LoginAsync(http, CorreoAdmin, ClaveAdmin);
        }

        var acceso = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(acceso));
        return acceso!;
    }

    private static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login de {email}: «{await resp.Content.ReadAsStringAsync()}»");
        var token = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(raw).RootElement.Clone();
    }
}
