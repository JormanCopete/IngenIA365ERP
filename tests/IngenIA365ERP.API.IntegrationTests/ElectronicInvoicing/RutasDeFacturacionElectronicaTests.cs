using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4, sección API (T744–T751): las rutas de <c>/api/electronic-invoicing</c> están publicadas y cableadas —la raíz de
/// composición registra el canal <c>SIMULADO</c> (T749)—, piden su permiso, exigen <c>Idempotency-Key</c> al escribir, el enlace de un
/// artefacto que no existe es el mismo 404 y la vista <c>dian-documents</c> aparece en el registro de informes. No recorre el ciclo de
/// emisión: eso lo hacen las e2e de la historia sobre <c>CanalSimulado</c> (T765). En una cooperativa aislada del mismo host.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class RutasDeFacturacionElectronicaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Las_rutas_de_facturacion_electronica_estan_publicadas_con_su_permiso_y_su_clave()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "rutasfe");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;

        // La configuración muestra el canal simulado: AddElectronicInvoicing está en la raíz de composición.
        var configuracion = await InventarioE2E.GetAsync(http, admin, "/api/electronic-invoicing/settings");
        configuracion.GetProperty("availableChannels").EnumerateArray()
            .Select(c => c.GetProperty("channelCode").GetString()).Should().Contain("SIMULADO");

        // Las consultas responden.
        foreach (var ruta in new[]
                 {
                     "/api/electronic-invoicing/readiness", "/api/electronic-invoicing/resolutions", "/api/electronic-invoicing/documents",
                     "/api/electronic-invoicing/contingencies", "/api/inventory/purchases/support-documents",
                     "/api/reports/inventory/dian-documents?format=json",
                 })
        {
            var r = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Get, ruta);
            r.StatusCode.Should().Be(HttpStatusCode.OK, $"{ruta}: «{await r.Content.ReadAsStringAsync()}»");
        }

        // Una escritura sin Idempotency-Key no llega al handler.
        var sinClave = await InventarioE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/electronic-invoicing/contingencies",
            new { type = "Issuer03", reason = "sin internet" });
        sinClave.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.CodigoDeErrorAsync(sinClave)).Should().Be("Operation.KeyRequired");

        // El enlace de un documento que no existe es el mismo 404 que lo inexistente.
        var enlace = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post,
            $"/api/electronic-invoicing/documents/{Guid.NewGuid()}/download-link?artifact=GraphicRepresentation");
        enlace.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // La vista aparece en el registro del centro de informes.
        var vistas = await InventarioE2E.GetAsync(http, admin, "/api/reports/inventory");
        vistas.EnumerateArray().Select(v => v.GetProperty("key").GetString()).Should().Contain("dian-documents");
    }
}
