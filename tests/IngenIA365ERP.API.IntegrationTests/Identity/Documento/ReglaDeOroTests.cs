using System.Net;
using System.Net.Http.Json;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T097, regla de oro, y US5 de punta a punta). La cuenta «de antes» es el primer administrador
/// de una cooperativa, cuya invitación no trae documento: entra con correo y clave como siempre, su sesión
/// renueva y nada le pide el documento mientras la cooperativa no fije fecha. Fijada la fecha, la renovación
/// se corta (sin invalidar la familia), el ingreso da el desafío <c>document-required</c>, registra su
/// documento con él y vuelve a entrar, ahora también con su usuario.
/// <para>
/// No aplica las migraciones a mitad de la prueba: la fixture arranca con todas aplicadas. Que la migración no
/// rota sellos ni toca datos de antes lo cubren <c>MigracionIdempotenteTests</c> y
/// <c>LasMigracionesDe017SonAditivas</c>.
/// </para>
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class ReglaDeOroTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_cuenta_de_antes_entra_igual_y_registra_su_documento_cuando_se_exige()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        const string correo = "legado@f017-oro.test";
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_oro", "900171401", correo);

        // La cuenta de antes: sin documento.
        var aceptada = await esc.AceptarAsync(http, correo, null);
        Assert.True(aceptada.StatusCode == HttpStatusCode.OK, await aceptada.Content.ReadAsStringAsync());
        var sesion = await Escenario017.LeerAsync(aceptada);
        var refresh = sesion.GetProperty("refreshToken").GetString()!;

        // Renueva, y entra con correo y clave a su cooperativa, como siempre.
        var renovada = await Escenario017.RenovarAsync(http, refresh);
        Assert.True(renovada.StatusCode == HttpStatusCode.OK, await renovada.Content.ReadAsStringAsync());
        refresh = (await Escenario017.LeerAsync(renovada)).GetProperty("refreshToken").GetString()!;
        var ingreso = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, correo));
        Assert.Equal("None", ingreso.GetProperty("challenge").GetString());
        Assert.Equal(coop, ingreso.GetProperty("activeTenantPublicId").GetGuid());
        var me = await Escenario017.LeerAsync(await Escenario017.EnviarAsync(http, ingreso.GetProperty("accessToken").GetString()!, HttpMethod.Get, "/api/auth/me"));
        Assert.True(me.GetProperty("documentPending").GetBoolean());

        // La cooperativa fija la fecha: hoy.
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var politica = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/tenants/{coop}/identity-policy", new
        {
            tiposAceptados = new[] { "CC", "CE", "PPT", "PEP", "TI", "PA" }, documentoExigidoDesde = hoy, intentosAlActivar = 3,
        });
        Assert.True(politica.IsSuccessStatusCode, await politica.Content.ReadAsStringAsync());

        // La renovación se corta con su código; el ingreso da sólo el desafío.
        var cortada = await Escenario017.RenovarAsync(http, refresh);
        Assert.Equal(HttpStatusCode.Forbidden, cortada.StatusCode);
        Assert.Equal("Identity.Document.Required", await Escenario017.CodigoAsync(cortada));

        var desafio = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, correo));
        Assert.Equal("DocumentRequired", desafio.GetProperty("challenge").GetString());
        Assert.False(desafio.TryGetProperty("accessToken", out var acceso) && acceso.ValueKind == System.Text.Json.JsonValueKind.String);
        var tokenDeDesafio = desafio.GetProperty("challengeToken").GetString()!;

        // El token del desafío no sirve para nada más.
        var otraRuta = await Escenario017.EnviarAsync(http, tokenDeDesafio, HttpMethod.Post, "/api/sessions/select-tenant", new { tenantPublicId = coop });
        Assert.False(otraRuta.IsSuccessStatusCode);

        var registrado = await Escenario017.EnviarAsync(http, tokenDeDesafio, HttpMethod.Post, "/api/auth/document", new
        {
            documentType = "CC", documentNumber = "63.140.001", documentNumberConfirmation = "63140001",
        });
        Assert.True(registrado.StatusCode == HttpStatusCode.OK, await registrado.Content.ReadAsStringAsync());
        Assert.Equal("63140001", (await Escenario017.LeerAsync(registrado)).GetProperty("documentUsername").GetString());

        // Vuelve a entrar, con correo y con su usuario.
        foreach (var identificador in new[] { correo, "63140001" })
        {
            var r = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, identificador));
            Assert.Equal("None", r.GetProperty("challenge").GetString());
            Assert.Equal(coop, r.GetProperty("activeTenantPublicId").GetGuid());
        }
    }
}
