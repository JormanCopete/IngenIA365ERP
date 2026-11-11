using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// Feature 020 (T182; R1; FR-001 a FR-004): la puerta del módulo. Sin Transporte encendido, sus rutas responden 403 <c>Module.NotEnabled</c>
/// —también al administrador, que tiene todos los permisos— y el menú no lo muestra (<c>enabledModules</c>). Encendido por el maestro y
/// aprovisionado, abre; apagado, vuelve a cerrar sin borrar nada.
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class ModuloHabilitadoTests(ApiConProveedorFalsoFixture fx)
{
    [Fact]
    public async Task Sin_el_modulo_las_rutas_responden_403_y_el_menu_no_lo_muestra()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trnsinmodulo");
        using var http = fx.CreateClient();

        var r = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/transport/vehicles", null);

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await TransporteE2E.CodigoAsync(r)).Should().Be("Module.NotEnabled");
        var permisos = await TransporteE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/permissions/mine");
        Modulos(permisos).Should().NotContain(TransporteE2E.CodigoDelModulo);
    }

    [Fact]
    public async Task Encendido_abre_y_apagado_vuelve_a_cerrar()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trnconmodulo");
        using var http = fx.CreateClient();

        await TransporteE2E.HabilitarAsync(fx, http, coop.TenantPublicId);
        var abierto = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/transport/vehicles", null);
        abierto.StatusCode.Should().Be(HttpStatusCode.OK, await abierto.Content.ReadAsStringAsync());
        Modulos(await TransporteE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/permissions/mine")).Should().Contain(TransporteE2E.CodigoDelModulo);

        await TransporteE2E.HabilitarAsync(fx, http, coop.TenantPublicId, habilitado: false);
        var cerrado = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/transport/vehicles", null);
        cerrado.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Los_informes_de_Transporte_tambien_estan_detras_de_la_puerta()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trnsinmodulo");
        using var http = fx.CreateClient();

        var r = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/reports/transport/vehiculos", null);

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static IReadOnlyList<string> Modulos(System.Text.Json.JsonElement permisos) =>
        permisos.TryGetProperty("enabledModules", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Array
            ? m.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
            : [];
}
