using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// Feature 020 (T185; SC-006; FR-042, FR-057): la clave que la cooperativa pone a un conector no se vuelve a ver en ninguna parte: ni en la
/// lista de conectores, ni en la auditoría (que sí registra que se pusieron credenciales, con los nombres de los campos), ni en la
/// exportación de envíos. Recorre lo que hace la cooperativa: el maestro define y habilita, la réplica copia, el administrador pone las
/// credenciales.
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class CredencialesNoSeVenTests(ApiConProveedorFalsoFixture fx)
{
    private const string Secreto = "Clave-De-Prueba-Que-No-Debe-Verse-7Q2";

    [Fact]
    public async Task La_clave_del_conector_no_aparece_en_la_lista_ni_en_la_auditoria_ni_en_los_informes()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trncredenciales");
        using var http = fx.CreateClient();
        await TransporteE2E.HabilitarAsync(fx, http, coop.TenantPublicId);
        var conector = await TransporteE2E.ConectorAsync(fx, http, "CREDE2E", "https://proveedor.example.com", TransporteE2E.DefinicionDeReferencia);
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        var habilitar = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/saas/connectors/{conector}/tenants/{coop.TenantPublicId}",
            new { enabled = true, reason = "Prueba e2e" });
        habilitar.StatusCode.Should().Be(HttpStatusCode.NoContent, await habilitar.Content.ReadAsStringAsync());
        await fx.CorrerTareaAsync(coop.TenantPublicId, "conectores.replicar");

        var poner = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "usuario-e2e", ["password"] = Secreto } });
        poner.StatusCode.Should().Be(HttpStatusCode.NoContent, await poner.Content.ReadAsStringAsync());

        var lista = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/core/connectors", null);
        var textoDeLaLista = await lista.Content.ReadAsStringAsync();
        textoDeLaLista.Should().Contain("CREDE2E").And.Contain("\"hasCredentials\":true").And.NotContain(Secreto).And.NotContain("usuario-e2e");

        // La auditoría registra el cambio (con los nombres de los campos) y nunca el valor. Mongo escribe por tandas: se espera a que llegue.
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        string auditoria = string.Empty;
        for (var i = 0; i < 20 && !auditoria.Contains("SetConnectorCredentials", StringComparison.OrdinalIgnoreCase); i++)
        {
            var r = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/audit/logs?pageSize=200", null);
            auditoria = await r.Content.ReadAsStringAsync();
            if (!auditoria.Contains("SetConnectorCredentials", StringComparison.OrdinalIgnoreCase)) await Task.Delay(500);
        }
        auditoria.Should().NotContain(Secreto);

        var envios = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/reports/transport/envios?format=json", null);
        envios.StatusCode.Should().Be(HttpStatusCode.OK, await envios.Content.ReadAsStringAsync());
        (await envios.Content.ReadAsStringAsync()).Should().NotContain(Secreto);
    }

    [Fact]
    public async Task Un_campo_vacio_conserva_el_anterior_y_un_campo_ajeno_se_rechaza()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trncredenciales2");
        using var http = fx.CreateClient();
        var conector = await TransporteE2E.ConectorAsync(fx, http, "CREDE2F", "https://proveedor.example.com", TransporteE2E.DefinicionDeReferencia);
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/saas/connectors/{conector}/tenants/{coop.TenantPublicId}",
            new { enabled = true, reason = "Prueba e2e" });
        await fx.CorrerTareaAsync(coop.TenantPublicId, "conectores.replicar");

        var faltan = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "usuario-e2e" } });
        (await TransporteE2E.CodigoAsync(faltan)).Should().Be("Connector.CredentialMissing");

        var completas = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "usuario-e2e", ["password"] = Secreto } });
        completas.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var soloUsuario = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "otro-usuario", ["password"] = "" } });
        soloUsuario.StatusCode.Should().Be(HttpStatusCode.NoContent, "la clave vacía conserva la anterior");

        var ajeno = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["pin"] = "1234" } });
        (await TransporteE2E.CodigoAsync(ajeno)).Should().Be("Connector.UnknownCredentialField");
    }
}
