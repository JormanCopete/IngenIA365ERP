using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T122 (FR-092, FR-093; quickstart §2.2 y §3.1), en el motor de <c>DB_PROVIDER</c>: lo que la API deja ver del retiro del
/// módulo heredado (fase 2) y de la base de permisos de la fase 3. Las rutas heredadas ya no existen —ni con alias—; los
/// códigos nuevos llegan a los permisos efectivos del administrador de la cooperativa; los ocho perfiles sugeridos se
/// listan y crear un rol desde uno deja un rol editable.
///
/// <para>
/// <b>Escrita, no corrida</b>: se corre por primera vez tras el par <c>PlataformaParaInventario</c> (T186), en los dos
/// motores. Dos precisiones frente al texto de la tarea: (1) los permisos efectivos se leen con el administrador de la
/// cooperativa y no con el maestro, porque <c>GET /api/admin/permissions/mine</c> del maestro responde
/// <c>isGlobalMasterAdmin</c> sin códigos y exige cooperativa activa; (2) <c>GET /api/inventory/products</c> vuelve a
/// existir con el catálogo de US1 (T230, con <c>Inventory.Catalog.View</c>): cuando aparezca, esa línea de
/// <see cref="RutasHeredadas"/> se retira y la prueba de su permiso la hace US1.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class RetiroDelModuloHeredadoTests(CentralIdentityApiFixture fx)
{
    /// <summary>Las rutas del módulo heredado que la fase 2 retiró sin alias (contracts/api.md §29).</summary>
    public static TheoryData<string, string> RutasHeredadas => new()
    {
        { "GET", "/api/inventory/products" },
        { "GET", "/api/inventory/invoices" },
        { "POST", "/api/inventory/invoices" },
        { "GET", "/api/reports/inventory/valuation/pdf" },
        { "GET", "/api/reports/inventory/invoice/3f2504e0-4f89-11d3-9a0c-0305e82c3301/pdf" },
    };

    private static readonly string[] PerfilesDeInventario =
    [
        "inventario.administrador", "inventario.jefe", "inventario.bodeguero", "inventario.comprador",
        "inventario.cajero", "inventario.aprobador", "inventario.contador", "inventario.auditor",
    ];

    [Theory]
    [MemberData(nameof(RutasHeredadas))]
    public async Task Las_rutas_heredadas_ya_no_existen(string metodo, string ruta)
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "heredado");
        using var http = fx.CreateClient();

        var resp = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, new HttpMethod(metodo), ruta, metodo == "POST" ? new { } : null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{metodo} {ruta} se retiró sin alias: «{await resp.Content.ReadAsStringAsync()}»");
    }

    [Fact]
    public async Task Los_permisos_efectivos_del_administrador_incluyen_los_codigos_nuevos()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "heredado");
        using var http = fx.CreateClient();

        var permisos = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/permissions/mine");
        var codigos = permisos.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()).ToList();

        codigos.Should().Contain([
            "Inventory.Reports.View", "Inventory.Reports.Export", "Inventory.Reports.ExportPersonalData",
            "Inventory.DocumentTypes.View", "Inventory.DocumentTypes.Manage", "Inventory.Catalog.View",
            "Core.Taxes.View", "Core.Taxes.Manage", "Accounting.InventoryRules.View", "AuditLog.VerifyIntegrity",
        ]);
    }

    [Fact]
    public async Task Los_ocho_perfiles_sugeridos_se_listan_y_crear_desde_uno_deja_un_rol_editable()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "heredado");
        using var http = fx.CreateClient();

        var plantillas = await InventarioE2E.GetAsync(http, coop.TokenAdmin, "/api/admin/roles/templates?module=Inventory");
        plantillas.EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().BeEquivalentTo(PerfilesDeInventario);

        using var peticion = InventarioE2E.ConClave(new HttpRequestMessage(HttpMethod.Post, "/api/admin/roles/from-template")
        {
            Content = JsonContent.Create(new { templateKey = "inventario.bodeguero", code = "BODEGA_E2E", name = "Bodega (e2e)" }),
        });
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
        var alta = await http.SendAsync(peticion);
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var creado = await InventarioE2E.LeerAsync(alta);
        creado.GetProperty("omitted").GetArrayLength().Should().Be(0, "con el catálogo completo ningún código de la plantilla falta");
        creado.GetProperty("permissionCodes").GetArrayLength().Should().BePositive();

        var rol = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"/api/admin/roles/{creado.GetProperty("rolePublicId").GetGuid()}");
        rol.GetProperty("isBuiltIn").GetBoolean().Should().BeFalse("un rol creado desde un perfil sugerido es de la cooperativa y se edita");
    }
}
