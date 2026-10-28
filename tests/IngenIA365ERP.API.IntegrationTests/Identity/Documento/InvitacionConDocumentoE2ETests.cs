using System.Net;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T094, US2) por la ruta del administrador de la cooperativa:
/// <list type="bullet">
///   <item>sin documento la invitación no sale (D-18: 400 con el motivo);</item>
///   <item>a un documento nuevo sale al correo digitado;</item>
///   <item>al documento de quien ya tiene cuenta en otra cooperativa sale a SU correo, con la plantilla de
///         «te invitan a una nueva cooperativa» (T047), y el administrador ve el mismo mensaje;</item>
///   <item>con el correo de otra persona se rechaza («Correo ya registrado»).</item>
/// </list>
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class InvitacionConDocumentoE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_invitacion_del_administrador_con_documento()
    {
        var esc = new Escenario017(fx);
        using var http = fx.CreateClient();
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coopA = await esc.CooperativaAsync(http, maestro, "tenant_f017_inv_a", "900171101", "admin@f017-inv-a.test");
        var coopB = await esc.CooperativaAsync(http, maestro, "tenant_f017_inv_b", "900171102");

        // El primer administrador de A acepta (su invitación no trae documento) y queda con sesión en A.
        var aceptada = await esc.AceptarAsync(http, "admin@f017-inv-a.test", null);
        Assert.True(aceptada.StatusCode == HttpStatusCode.OK, await aceptada.Content.ReadAsStringAsync());
        var admin = (await Escenario017.LeerAsync(aceptada)).GetProperty("accessToken").GetString()!;
        var ruta = $"/api/tenants/{coopA}/invitations";

        // 1) Sin documento no sale (D-18).
        var sinDocumento = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, ruta, new { email = "sin.doc@f017.test" });
        Assert.Equal(HttpStatusCode.BadRequest, sinDocumento.StatusCode);
        Assert.Contains("documento", await sinDocumento.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fx.Emails.Sent, m => m.To == "sin.doc@f017.test");

        // 2) Documento nuevo: sale al correo digitado.
        var nueva = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, ruta,
            DocumentoDePrueba.Invitacion("nueva.persona@f017.test"));
        Assert.True(nueva.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, await nueva.Content.ReadAsStringAsync());
        var mensaje = (await Escenario017.LeerAsync(nueva)).GetProperty("message").GetString();
        Assert.Contains(fx.Emails.Sent, m => m.To == "nueva.persona@f017.test");

        // 3) Alguien que ya tiene cuenta en B: el administrador de A digita otro correo; sale al de la cuenta.
        await esc.PersonaAsync(http, maestro, coopB, "ya.existe@f017.test", "CC", "63110001");
        var antes = fx.Emails.Sent.Count(m => m.To == "ya.existe@f017.test");
        var existente = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, ruta, new
        {
            email = "otro.correo.digitado@f017.test", documentType = "CC", documentNumber = "63.110.001",
            documentNumberConfirmation = "63110001", firstName = "Nombre", lastName = "Digitado",
        });
        Assert.True(existente.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, await existente.Content.ReadAsStringAsync());
        Assert.Equal(mensaje, (await Escenario017.LeerAsync(existente)).GetProperty("message").GetString());
        Assert.Equal(antes + 1, fx.Emails.Sent.Count(m => m.To == "ya.existe@f017.test"));
        Assert.DoesNotContain(fx.Emails.Sent, m => m.To == "otro.correo.digitado@f017.test");
        var aviso = fx.Emails.Sent.Last(m => m.To == "ya.existe@f017.test");
        Assert.Contains("Ya tienes cuenta", aviso.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Digitado", aviso.BodyHtml, StringComparison.Ordinal);

        // 4) El correo de otra persona con otro documento: «Correo ya registrado».
        var ajeno = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, ruta, new
        {
            email = "ya.existe@f017.test", documentType = "CC", documentNumber = "63110002",
            documentNumberConfirmation = "63110002", firstName = "Otra", lastName = "Persona",
        });
        Assert.Equal("Invitation.EmailTaken", await Escenario017.CodigoAsync(ajeno));
    }
}
