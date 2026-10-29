using System.Net;
using System.Text.Json;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T096, US6): corrección, actualización y fusión de punta a punta.
/// <list type="bullet">
///   <item>el administrador corrige a quien sólo está en su cooperativa; cuando la persona entra a otra,
///         ya no puede (<c>Identity.Document.PlatformOnly</c>) y el maestro sí;</item>
///   <item>el historial conserva cada cambio y el usuario viejo deja de servir para entrar;</item>
///   <item>el administrador no ve las rutas de identidades del maestro ni fija claves;</item>
///   <item>la fusión junta las cooperativas en la identidad destino, deshabilita la otra y se niega si las
///         dos están en una misma cooperativa.</item>
/// </list>
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class CorreccionYFusionE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Correccion_por_el_administrador_y_por_el_maestro_con_historial()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coopA = await esc.CooperativaAsync(http, maestro, "tenant_f017_corr_a", "900171301", "admin@f017-corr-a.test");
        var coopB = await esc.CooperativaAsync(http, maestro, "tenant_f017_corr_b", "900171302");
        var admin = (await Escenario017.LeerAsync(await esc.AceptarAsync(http, "admin@f017-corr-a.test", null)))
            .GetProperty("accessToken").GetString()!;

        const string correo = "corregible@f017.test";
        await esc.PersonaAsync(http, maestro, coopA, correo, "TI", "1063130001");

        // El administrador lo actualiza (de TI a CC): sólo está en su cooperativa.
        var usuario = await UsuarioDeLaCooperativaAsync(http, admin, correo);
        var actualizada = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/correct-document", new
        {
            documentType = "CC", documentNumber = "1063130001", documentNumberConfirmation = "1.063.130.001",
            reason = "Update", notes = "Cumplió la mayoría de edad",
        });
        Assert.True(actualizada.IsSuccessStatusCode, await actualizada.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Escenario017.IngresarAsync(http, "1063130001")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Escenario017.IngresarAsync(http, "TI1063130001")).StatusCode);
        Assert.Contains(fx.Emails.Sent, m => m.To == correo && m.BodyHtml.Contains("1063130001", StringComparison.Ordinal));

        // El administrador no usa las rutas del maestro ni fija claves.
        var rutaDelMaestro = await Escenario017.EnviarAsync(http, admin, HttpMethod.Get, "/api/saas/identities?q=1063130001");
        Assert.Equal("Saas.MasterOnly", await Escenario017.CodigoAsync(rutaDelMaestro));
        var fijarClave = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/reset-password",
            new { newPassword = "Otra-Clave-Cualquiera-1" });
        Assert.Equal(HttpStatusCode.NotFound, fijarClave.StatusCode);

        // La persona entra también a B: el administrador de A ya no corrige; el maestro sí.
        await esc.InvitarAsync(http, maestro, coopB, correo, "CC", "1063130001");
        var enB = await esc.AceptarAsync(http, correo, "1063130001", nueva: false);
        Assert.True(enB.StatusCode == HttpStatusCode.OK, await enB.Content.ReadAsStringAsync());
        var bloqueada = await Escenario017.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/correct-document", new
        {
            documentType = "CC", documentNumber = "1063130009", documentNumberConfirmation = "1063130009", reason = "Correction",
        });
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("Identity.Document.PlatformOnly", await Escenario017.CodigoAsync(bloqueada));

        var identidad = await IdentidadAsync(http, maestro, "1063130001");
        var porMaestro = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Post, $"/api/saas/identities/{identidad}/correct-document", new
        {
            documentType = "CC", documentNumber = "1063130009", documentNumberConfirmation = "1063130009",
            reason = "Correction", notes = "Se digitó mal",
        });
        Assert.True(porMaestro.IsSuccessStatusCode, await porMaestro.Content.ReadAsStringAsync());

        // El historial conserva los dos cambios, con lo anterior.
        var detalle = await Escenario017.LeerAsync(await Escenario017.EnviarAsync(http, maestro, HttpMethod.Get, $"/api/saas/identities/{identidad}"));
        var historial = detalle.GetProperty("history").EnumerateArray().ToList();
        Assert.Contains(historial, h => h.GetProperty("reason").GetString() == "Update" && h.GetProperty("oldUsername").GetString() == "TI1063130001");
        Assert.Contains(historial, h => h.GetProperty("reason").GetString() == "Correction" && h.GetProperty("newUsername").GetString() == "1063130009");
        Assert.Equal(HttpStatusCode.OK, (await Escenario017.IngresarAsync(http, "1063130009")).StatusCode);
    }

    [Fact]
    public async Task La_fusion_junta_cooperativas_y_se_niega_si_comparten_una()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coopC = await esc.CooperativaAsync(http, maestro, "tenant_f017_fus_c", "900171311");
        var coopD = await esc.CooperativaAsync(http, maestro, "tenant_f017_fus_d", "900171312");

        // Dos cuentas de la misma persona: una con cédula en C, otra con cédula de extranjería en D.
        await esc.PersonaAsync(http, maestro, coopC, "una.cuenta@f017.test", "CC", "63130101");
        await esc.PersonaAsync(http, maestro, coopD, "otra.cuenta@f017.test", "CE", "63130102");
        var destino = await IdentidadAsync(http, maestro, "63130101");
        var origen = await IdentidadAsync(http, maestro, "CE63130102");

        var fusion = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/identities/merge", new
        {
            sourceId = origen, targetId = destino, reason = "Dos correos de la misma persona",
        });
        Assert.True(fusion.IsSuccessStatusCode, await fusion.Content.ReadAsStringAsync());
        Assert.Equal("Done", (await Escenario017.LeerAsync(fusion)).GetProperty("status").GetString());

        // El destino ve las dos cooperativas; el origen ya no entra.
        var ingreso = await Escenario017.LeerAsync(await Escenario017.IngresarAsync(http, "una.cuenta@f017.test"));
        Assert.Equal("TenantSelection", ingreso.GetProperty("challenge").GetString());
        var cooperativas = ingreso.GetProperty("activeTenants").EnumerateArray().Select(t => t.GetProperty("tenantPublicId").GetGuid()).ToList();
        Assert.Contains(coopC, cooperativas);
        Assert.Contains(coopD, cooperativas);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Escenario017.IngresarAsync(http, "otra.cuenta@f017.test")).StatusCode);

        // Dos cuentas en una misma cooperativa: no se fusionan.
        await esc.PersonaAsync(http, maestro, coopC, "comparte.uno@f017.test", "CC", "63130201");
        await esc.PersonaAsync(http, maestro, coopC, "comparte.dos@f017.test", "CC", "63130202");
        var compartida = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/identities/merge", new
        {
            sourceId = await IdentidadAsync(http, maestro, "63130202"),
            targetId = await IdentidadAsync(http, maestro, "63130201"),
            reason = "Prueba",
        });
        Assert.Equal(HttpStatusCode.Conflict, compartida.StatusCode);
        Assert.Equal("Identity.Merge.SharedTenant", await Escenario017.CodigoAsync(compartida));
    }

    private static async Task<Guid> IdentidadAsync(HttpClient http, string maestro, string q)
    {
        var r = await Escenario017.EnviarAsync(http, maestro, HttpMethod.Get, $"/api/saas/identities?q={Uri.EscapeDataString(q)}");
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var lista = (await Escenario017.LeerAsync(r)).EnumerateArray().ToList();
        Assert.Single(lista);
        return lista[0].GetProperty("id").GetGuid();
    }

    private static async Task<Guid> UsuarioDeLaCooperativaAsync(HttpClient http, string admin, string correo)
    {
        var r = await Escenario017.EnviarAsync(http, admin, HttpMethod.Get, $"/api/admin/users?search={Uri.EscapeDataString(correo)}");
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var cuerpo = await Escenario017.LeerAsync(r);
        var items = cuerpo.ValueKind == JsonValueKind.Array ? cuerpo : cuerpo.GetProperty("items");
        return items.EnumerateArray().Single(u => u.GetProperty("email").GetString() == correo).GetProperty("publicId").GetGuid();
    }
}
