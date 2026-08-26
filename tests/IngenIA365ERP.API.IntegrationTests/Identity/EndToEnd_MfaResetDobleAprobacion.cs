using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using OtpNet;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// La prueba que faltaba, y por eso el defecto llegó a producción.
///
/// <para>
/// El reseteo con doble aprobación limpiaba <c>MfaSecret</c> sobre
/// <c>SEC_Users</c> —el modelo de Fase 0, que ya nadie lee— mientras el login
/// verifica contra <c>ADM_CentralUsers</c>. Dos administradores aprobaban, salía
/// el correo diciendo «tu segundo factor fue restablecido», y la persona seguía
/// bloqueada fuera de su cuenta. La prueba unitaria no lo cazó porque afirmaba
/// justamente la mutación equivocada.
/// </para>
///
/// <para>
/// Lo que hace falta para no repetirlo es cerrar el circuito completo: aprobar
/// de verdad y después <b>volver a la puerta de entrada</b> a comprobar que
/// cambió. Si el reset no toca la identidad central, el login sigue devolviendo
/// <c>MfaRequired</c> y esta prueba se pone roja.
/// </para>
///
/// <para>
/// De paso cubre el otro agujero que estas rutas tenían: exigían sólo estar
/// autenticado, sin permiso. Mientras el reset no hacía nada daba igual quién
/// aprobara; en cuanto funciona, sin permiso bastarían dos cuentas cualesquiera
/// de la cooperativa para dejar a quien fuera sin segundo factor.
/// </para>
/// </summary>
public sealed class EndToEnd_MfaResetDobleAprobacion(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Clave = "Coop-Strong-Pwd-2026!";
    private const string CorreoAna = "ana.admin@coop.resetmfa.test";      // primera admin y objetivo
    private const string CorreoBeto = "beto.solicita@coop.resetmfa.test";  // solicitante
    private const string CorreoCarlos = "carlos.aprueba@coop.resetmfa.test";
    private const string CorreoDiana = "diana.aprueba@coop.resetmfa.test";

    [Fact]
    public async Task Dos_aprobaciones_restablecen_el_segundo_factor_que_el_login_verifica()
    {
        using var http = fx.CreateClient();

        // 1) Master registra la cooperativa con Ana como primera administradora.
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        using var registerReq = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Reset MFA Test",
                schemaName = "tenant_reset_mfa",
                nit = "900555444",
                legalName = "Coop. Reset MFA SAS",
                contactEmail = "contacto@coop.resetmfa.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = CorreoAna,
            }),
        };
        registerReq.Headers.Authorization = new("Bearer", masterToken);
        var registerResp = await http.SendAsync(registerReq);
        Assert.Equal(HttpStatusCode.OK, registerResp.StatusCode);
        var tenantPublicId = (await LeerJsonAsync(registerResp)).GetProperty("tenantPublicId").GetGuid();

        // 2) Ana acepta y queda con sesión operativa (CompanyAdmin).
        var tokenAna = await AceptarInvitacionAsync(http, CorreoAna);

        // 3) Ana invita a los otros tres. Todavía sin política de MFA, así que
        //    aceptan sin segundo factor y salen con sesión directa.
        foreach (var correo in new[] { CorreoBeto, CorreoCarlos, CorreoDiana })
        {
            await InvitarAsync(http, tokenAna, tenantPublicId, correo);
        }

        var tokenBeto = await AceptarInvitacionAsync(http, CorreoBeto);
        var tokenCarlos = await AceptarInvitacionAsync(http, CorreoCarlos);
        var tokenDiana = await AceptarInvitacionAsync(http, CorreoDiana);

        // 4) Recién provisionados entran como ReadOnly, que NO trae los permisos
        //    de reset. Es el momento exacto para comprobar la guardia: la cola de
        //    aprobación responde 404 indistinguible, no 403.
        using var colaSinPermisoReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/mfa/reset/requests");
        colaSinPermisoReq.Headers.Authorization = new("Bearer", tokenCarlos);
        var colaSinPermisoResp = await http.SendAsync(colaSinPermisoReq);
        Assert.Equal(HttpStatusCode.NotFound, colaSinPermisoResp.StatusCode);

        // 5) Ana los promueve a CompanyAdmin, que sí trae Security.MfaReset.*.
        var idRolCompanyAdmin = await PublicIdDelRolAsync(http, tokenAna, "CompanyAdmin");
        var usuarios = await ListarUsuariosAsync(http, tokenAna);

        foreach (var correo in new[] { CorreoBeto, CorreoCarlos, CorreoDiana })
        {
            await AsignarRolAsync(http, tokenAna, usuarios[correo], idRolCompanyAdmin);
        }

        // 6) Ana inscribe su segundo factor: es quien va a perder el teléfono.
        var secretoAna = await InscribirMfaAsync(http, tokenAna);

        // Con MFA inscrito, su login pasa a pedir el segundo factor.
        Assert.Equal("MfaRequired", (await LoginAsync(http, CorreoAna)).GetProperty("challenge").GetString());

        // 7) La cooperativa activa la política de MFA obligatorio, para que tras
        //    el reset el login exija re-inscripción y no una sesión directa.
        using var policyReq = new HttpRequestMessage(
            HttpMethod.Put, $"/api/tenants/{tenantPublicId}/mfa-policy")
        {
            Content = JsonContent.Create(new { isRequired = true }),
        };
        policyReq.Headers.Authorization = new("Bearer", tokenAna);
        Assert.True((await http.SendAsync(policyReq)).IsSuccessStatusCode);

        // 8) Beto solicita el reset del segundo factor de Ana.
        using var solicitarReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/reset/request")
        {
            Content = JsonContent.Create(new
            {
                targetUserPublicId = usuarios[CorreoAna],
                reason = "Ana perdió el teléfono y no conserva los códigos de respaldo.",
                evidenceAttachmentPublicId = (Guid?)null,
            }),
        };
        solicitarReq.Headers.Authorization = new("Bearer", tokenBeto);
        var solicitarResp = await http.SendAsync(solicitarReq);
        Assert.True(solicitarResp.StatusCode == HttpStatusCode.OK,
            $"Solicitud de reset: {(int)solicitarResp.StatusCode} {await solicitarResp.Content.ReadAsStringAsync()}");
        var solicitudPublicId = (await LeerJsonAsync(solicitarResp))
            .GetProperty("requestPublicId").GetGuid();

        // 9) Beto no puede aprobar lo que él mismo pidió: la doble aprobación
        //    exige dos personas distintas, y ninguna puede ser el solicitante.
        var betoSeAprueba = await AprobarAsync(http, tokenBeto, solicitudPublicId);
        Assert.False(betoSeAprueba.Status == HttpStatusCode.OK,
            "El solicitante no debería poder aprobar su propia solicitud.");
        Assert.Equal("Auth.MfaResetCannotApproveOwnRequest",
            betoSeAprueba.Body.GetProperty("code").GetString());

        // 10) Primera aprobación: sella al aprobador y NO ejecuta todavía.
        var primera = await AprobarAsync(http, tokenCarlos, solicitudPublicId);
        Assert.Equal(HttpStatusCode.OK, primera.Status);
        Assert.Equal("Approved", primera.Body.GetProperty("status").GetString());

        // Con una sola aprobación el segundo factor sigue en pie.
        Assert.Equal("MfaRequired", (await LoginAsync(http, CorreoAna)).GetProperty("challenge").GetString());

        // 11) Segunda aprobación, de otra persona: ejecuta.
        var segunda = await AprobarAsync(http, tokenDiana, solicitudPublicId);
        Assert.Equal(HttpStatusCode.OK, segunda.Status);
        Assert.Equal("Executed", segunda.Body.GetProperty("status").GetString());

        // 12) LA COMPROBACIÓN QUE FALTABA. Volver a la puerta de entrada.
        //
        //     Si el reset no hubiera tocado ADM_CentralUsers —el defecto— aquí
        //     seguiría llegando "MfaRequired" con el secreto viejo todavía
        //     válido, y Ana seguiría afuera pese al correo que le dijo que ya
        //     podía entrar.
        var loginTrasReset = await LoginAsync(http, CorreoAna);
        Assert.Equal("MfaEnrollmentRequired", loginTrasReset.GetProperty("challenge").GetString());
        Assert.Equal("mfa-enroll", loginTrasReset.GetProperty("challengeTokenPurpose").GetString());

        // 13) Y el secreto viejo ya no sirve para nada: no hay desafío que pasar.
        Assert.False(loginTrasReset.TryGetProperty("challengeToken", out var t) &&
                     string.IsNullOrWhiteSpace(t.GetString()));
        Assert.Equal(6, new Totp(Base32Encoding.ToBytes(secretoAna)).ComputeTotp().Length);

        // 14) La pantalla de usuarios lo refleja, porque lee de la identidad
        //     central y no de las columnas huérfanas de SEC_Users.
        using var usuariosReq = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users?pageSize=200");
        usuariosReq.Headers.Authorization = new("Bearer", tokenCarlos);
        var usuariosResp = await http.SendAsync(usuariosReq);
        Assert.Equal(HttpStatusCode.OK, usuariosResp.StatusCode);
        var filaAna = (await LeerJsonAsync(usuariosResp)).GetProperty("items").EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == CorreoAna);
        Assert.False(filaAna.GetProperty("isMfaEnabled").GetBoolean());
    }

    // ---------- Auxiliares ----------

    private async Task InvitarAsync(HttpClient http, string tokenAdmin, Guid tenantPublicId, string correo)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/tenants/{tenantPublicId}/invitations/")
        {
            Content = JsonContent.Create(new { email = correo }),
        };
        req.Headers.Authorization = new("Bearer", tokenAdmin);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"No se pudo invitar a {correo}: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    private async Task<string> AceptarInvitacionAsync(HttpClient http, string correo)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        Assert.NotNull(mensaje);
        var match = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(match.Success, $"El correo de invitación a {correo} no trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = match.Groups[1].Value,
            registration = new { password = Clave },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await LeerJsonAsync(resp);
        var token = json.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token),
            $"{correo} no obtuvo sesión al aceptar la invitación: {json}");
        return token!;
    }

    private static async Task<JsonElement> LoginAsync(HttpClient http, string correo)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email = correo, password = Clave });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await LeerJsonAsync(resp);
    }

    private static async Task<Guid> PublicIdDelRolAsync(HttpClient http, string token, string code)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/roles?pageSize=200");
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await LeerJsonAsync(resp)).GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("code").GetString() == code)
            .GetProperty("publicId").GetGuid();
    }

    private static async Task<Dictionary<string, Guid>> ListarUsuariosAsync(HttpClient http, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users?pageSize=200");
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await LeerJsonAsync(resp)).GetProperty("items").EnumerateArray()
            .Where(u => u.GetProperty("email").ValueKind == JsonValueKind.String)
            .ToDictionary(
                u => u.GetProperty("email").GetString()!,
                u => u.GetProperty("publicId").GetGuid());
    }

    private static async Task AsignarRolAsync(
        HttpClient http, string tokenAdmin, Guid usuarioPublicId, Guid rolPublicId)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/users/{usuarioPublicId}/roles")
        {
            Content = JsonContent.Create(new { rolePublicId = rolPublicId }),
        };
        req.Headers.Authorization = new("Bearer", tokenAdmin);
        var resp = await http.SendAsync(req);
        Assert.True(resp.IsSuccessStatusCode,
            $"No se pudo asignar el rol: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>Inscribe MFA con el token de sesión y devuelve el secreto base32.</summary>
    private static async Task<string> InscribirMfaAsync(HttpClient http, string token)
    {
        using var beginReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
        beginReq.Headers.Authorization = new("Bearer", token);
        var beginResp = await http.SendAsync(beginReq);
        Assert.Equal(HttpStatusCode.OK, beginResp.StatusCode);
        var secreto = (await LeerJsonAsync(beginResp)).GetProperty("secretBase32").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secreto));

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(secreto)).ComputeTotp(),
            }),
        };
        confirmReq.Headers.Authorization = new("Bearer", token);
        var confirmResp = await http.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        return secreto!;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> AprobarAsync(
        HttpClient http, string token, Guid solicitudPublicId)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/auth/mfa/reset/{solicitudPublicId}/approve");
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
