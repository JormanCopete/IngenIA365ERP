using System.Net;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T095, US3): activar confirmando el documento. Coincide → activa. No coincide → cuenta los
/// intentos y, agotados (3 por defecto), la invitación queda en revisión y ni el número correcto la activa.
/// La corrige quien invitó (sale otra, la anterior queda reemplazada) y con la nueva se activa.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class ActivacionE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Coincide_no_coincide_hasta_revision_y_correccion()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_activa", "900171201");

        // Coincide a la primera.
        await esc.InvitarAsync(http, maestro, coop, "coincide@f017.test", "CC", "63120001");
        var ok = await esc.AceptarAsync(http, "coincide@f017.test", "63.120.001");
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());

        // No coincide: los intentos se agotan y queda en revisión.
        const string correo = "se.equivoca@f017.test";
        var emitida = await esc.InvitarAsync(http, maestro, coop, correo, "CC", "63120002");
        var invitacion = emitida.GetProperty("invitationPublicId").GetGuid();
        for (var i = 0; i < 2; i++)
        {
            var mal = await esc.AceptarAsync(http, correo, "11111111");
            Assert.Equal("Invitation.DocumentMismatch", await Escenario017.CodigoAsync(mal));
        }
        var tercera = await esc.AceptarAsync(http, correo, "11111111");
        Assert.Equal(HttpStatusCode.Conflict, tercera.StatusCode);
        Assert.Equal("Invitation.UnderReview", await Escenario017.CodigoAsync(tercera));

        var correctaPeroTarde = await esc.AceptarAsync(http, correo, "63120002");
        Assert.Equal("Invitation.UnderReview", await Escenario017.CodigoAsync(correctaPeroTarde));

        // Quien invitó la corrige: sale otra y con ella se activa.
        var antes = fx.Emails.Sent.Count(m => m.To == correo);
        var corregida = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/tenants/{coop}/invitations/{invitacion}", new
        {
            email = correo, documentType = "CC", documentNumber = "63120003", documentNumberConfirmation = "63120003",
            firstName = "Se", lastName = "Equivoca",
        });
        Assert.True(corregida.IsSuccessStatusCode, await corregida.Content.ReadAsStringAsync());
        Assert.Equal(antes + 1, fx.Emails.Sent.Count(m => m.To == correo));

        var activa = await esc.AceptarAsync(http, correo, "63120003");
        Assert.True(activa.StatusCode == HttpStatusCode.OK, await activa.Content.ReadAsStringAsync());
        Assert.Equal("63120003", (await Escenario017.LeerAsync(activa)).GetProperty("documentUsername").GetString());
    }
}
