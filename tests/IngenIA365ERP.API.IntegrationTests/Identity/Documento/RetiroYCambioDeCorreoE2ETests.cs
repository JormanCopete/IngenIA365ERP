using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T098, US8 y US7). Una persona en dos cooperativas con una sesión abierta en cada una: retirarla
/// de una corta esa sesión y no la otra, y al entrar queda en la que le queda. Después cambia su correo ella
/// misma —step-up, código al correo nuevo, confirmación—: entra con el nuevo, el viejo ya no sirve, al viejo le
/// llega el aviso y las demás sesiones dejan de renovar.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class RetiroYCambioDeCorreoE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Retiro_de_una_cooperativa_y_cambio_de_correo()
    {
        var esc = new Escenario017(fx);
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coopE = await esc.CooperativaAsync(http, maestro, "tenant_f017_ret_e", "900171501");
        var coopF = await esc.CooperativaAsync(http, maestro, "tenant_f017_ret_f", "900171502");

        const string correo = "dos.cooperativas@f017.test";
        await esc.PersonaAsync(http, maestro, coopE, correo, "CC", "63150001");
        await esc.InvitarAsync(http, maestro, coopF, correo, "CC", "63150001");
        var enF = await esc.AceptarAsync(http, correo, "63150001", nueva: false);
        Assert.True(enF.StatusCode == HttpStatusCode.OK, await enF.Content.ReadAsStringAsync());

        var sesionE = await SesionEnAsync(http, correo, coopE);
        var sesionF = await SesionEnAsync(http, correo, coopF);

        // ---- Retiro de E ----
        var miembros = await Escenario017.LeerAsync(await Escenario017.EnviarAsync(http, maestro, HttpMethod.Get, $"/api/tenants/{coopE}/members"));
        var lista = miembros.ValueKind == JsonValueKind.Array ? miembros : miembros.GetProperty("items");
        var membresia = lista.EnumerateArray().Single(m => m.TryGetProperty("email", out var e) && e.GetString() == correo)
            .GetProperty("membershipPublicId").GetGuid();
        var retiro = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Post, $"/api/tenants/{coopE}/members/{membresia}/revoke");
        Assert.True(retiro.IsSuccessStatusCode, await retiro.Content.ReadAsStringAsync());

        var renuevaE = await Escenario017.RenovarAsync(http, sesionE.Refresh);
        Assert.False(renuevaE.IsSuccessStatusCode);
        Assert.Equal("Membership.NotActive", await Escenario017.CodigoAsync(renuevaE));
        var renuevaF = await Escenario017.RenovarAsync(http, sesionF.Refresh);
        Assert.True(renuevaF.StatusCode == HttpStatusCode.OK, await renuevaF.Content.ReadAsStringAsync());
        var refreshF = (await Escenario017.LeerAsync(renuevaF)).GetProperty("refreshToken").GetString()!;

        var ingreso = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, correo));
        Assert.Equal("None", ingreso.GetProperty("challenge").GetString());
        Assert.Equal(coopF, ingreso.GetProperty("activeTenantPublicId").GetGuid());
        var acceso = ingreso.GetProperty("accessToken").GetString()!;

        // ---- Cambio de correo ----
        const string nuevo = "correo.nuevo@f017.test";
        var sinStepUp = await Escenario017.EnviarAsync(http, acceso, HttpMethod.Post, "/api/auth/email-change", new { newEmail = nuevo });
        Assert.Equal("Identity.StepUpRequired", await Escenario017.CodigoAsync(sinStepUp));

        var stepUp = await Escenario017.EnviarAsync(http, acceso, HttpMethod.Post, "/api/auth/step-up", new { password = Escenario017.Clave });
        Assert.True(stepUp.StatusCode == HttpStatusCode.OK, await stepUp.Content.ReadAsStringAsync());
        var tokenStepUp = (await Escenario017.LeerAsync(stepUp)).GetProperty("stepUpToken").GetString()!;

        var pedido = await Escenario017.EnviarAsync(http, tokenStepUp, HttpMethod.Post, "/api/auth/email-change", new { newEmail = nuevo });
        Assert.True(pedido.StatusCode == HttpStatusCode.OK, await pedido.Content.ReadAsStringAsync());
        var mensaje = fx.Emails.Sent.Last(m => m.To == nuevo);
        var codigo = Regex.Match(mensaje.BodyHtml, @">\s*(\d{6})\s*<").Groups[1].Value;
        Assert.Matches("^[0-9]{6}$", codigo);

        var confirmado = await Escenario017.EnviarAsync(http, acceso, HttpMethod.Post, "/api/auth/email-change/confirm", new { code = codigo });
        Assert.True(confirmado.StatusCode == HttpStatusCode.OK, await confirmado.Content.ReadAsStringAsync());

        Assert.Contains(fx.Emails.Sent, m => m.To == correo && m.BodyHtml.Contains(nuevo, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await Escenario017.IngresarAsync(http, nuevo)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Escenario017.IngresarAsync(http, correo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Escenario017.IngresarAsync(http, "63150001")).StatusCode);
        Assert.False((await Escenario017.RenovarAsync(http, refreshF)).IsSuccessStatusCode, "el sello rotó: las demás sesiones no renuevan");
    }

    private static async Task<(string Acceso, string Refresh)> SesionEnAsync(HttpClient http, string correo, Guid cooperativa)
    {
        var login = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, correo));
        Assert.Equal("TenantSelection", login.GetProperty("challenge").GetString());
        var elegida = await Escenario017.EnviarAsync(http, login.GetProperty("challengeToken").GetString()!, HttpMethod.Post,
            "/api/sessions/select-tenant", new { tenantPublicId = cooperativa });
        Assert.True(elegida.StatusCode == HttpStatusCode.OK, await elegida.Content.ReadAsStringAsync());
        var cuerpo = await Escenario017.LeerAsync(elegida);
        return (cuerpo.GetProperty("accessToken").GetString()!, cuerpo.GetProperty("refreshToken").GetString()!);
    }
}
