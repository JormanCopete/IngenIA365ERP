using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T422 (FR-031, FR-092; api.md §31; contracts/plantillas.md §9), en el motor de <c>DB_PROVIDER</c>, en la cooperativa aislada
/// «vendedores»: el rol de vendedor se da a una persona del maestro y escribe a la vez la fila y la marca
/// <c>IsSalesperson</c>; retirarlo exige motivo y apaga la marca; darlo otra vez restaura la misma fila
/// (<c>salespersonPublicId</c> igual, <c>restored</c>); con el rol vivo, <c>Inventory.Salesperson.AlreadyActive</c>; la plantilla
/// con datos sin <c>Inventory.Reports.ExportPersonalData</c> es 404; y la importación revisa sin guardar, aplica todo o nada y
/// rechaza la fila de una persona que no existe sin crearla.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class VendedoresTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/inventory/salespeople";

    [Fact]
    public async Task Dar_retirar_y_restaurar_el_rol_sobre_la_misma_fila()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "vendedores");
        using var http = fx.CreateClient();
        var persona = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, coop.TokenAdmin, "Vendedora");

        var alta = await InventarioE2E.MandarAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new { personPublicId = persona, appliesCommission = true });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var creada = await InventarioE2E.LeerAsync(alta);
        var vendedor = creada.GetProperty("salespersonPublicId").GetGuid();
        creada.GetProperty("restored").GetBoolean().Should().BeFalse();
        (await EsVendedorAsync(http, coop.TokenAdmin, persona)).Should().BeTrue("la fila y la marca se escriben juntas");

        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new { personPublicId = persona }),
            "Inventory.Salesperson.AlreadyActive");

        (await InventarioE2E.MandarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Ruta}/{vendedor}/retire", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "retirar exige motivo");
        await InventarioE2E.ExitoAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Ruta}/{vendedor}/retire", new { reason = "Dejó la cooperativa" });
        (await EsVendedorAsync(http, coop.TokenAdmin, persona)).Should().BeFalse();

        var otraVez = await InventarioE2E.ExitoAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new { personPublicId = persona });
        otraVez.GetProperty("restored").GetBoolean().Should().BeTrue();
        otraVez.GetProperty("salespersonPublicId").GetGuid().Should().Be(vendedor, "se restaura la misma fila: los documentos que la citan siguen apuntando a ella");
        (await EsVendedorAsync(http, coop.TokenAdmin, persona)).Should().BeTrue();

        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, coop.TokenAdmin, HttpMethod.Post, Ruta, new { personPublicId = Guid.NewGuid() }),
            "Core.Person.NotFound", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task La_plantilla_con_datos_exige_el_permiso_de_datos_personales_y_la_importacion_es_todo_o_nada()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "vendedores");
        using var http = fx.CreateClient();
        var persona = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, coop.TokenAdmin, "Importada");
        var documento = await Accounting.ContabilidadE2E.DocumentoDeAsync(http, coop.TokenAdmin, persona);

        await InventarioE2E.RolAsync(http, coop, "ENSVENDV", null, ["Inventory.Salespeople.View"]);
        var (lector, _) = await InventarioE2E.UsuarioAsync(fx, http, coop, $"vendedores.lector.{coop.TenantPublicId:N}@coop.inventario.test", "ENSVENDV");
        (await InventarioE2E.MandarAsync(http, lector, HttpMethod.Get, $"{Ruta}/template.xlsx")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await InventarioE2E.MandarAsync(http, lector, HttpMethod.Get, $"{Ruta}/template.xlsx?withData=true")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "con datos personales exige además Inventory.Reports.ExportPersonalData");

        string[] encabezados = ["documento", "nombre", "tipoDeVendedor", "aplicaComision", "activo"];
        var conError = InventarioE2E.Libro(("Datos", encabezados, [[documento, null, "1", "no", "sí"], ["999999999999", null, null, null, "sí"]]));
        var revision = await InventarioE2E.LeerAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "review", conError));
        revision.GetProperty("valid").GetBoolean().Should().BeFalse();
        revision.GetProperty("errors").EnumerateArray().Should().ContainSingle().Which.GetProperty("row").GetInt32().Should().Be(3);
        await InventarioE2E.FallaAsync(await InventarioE2E.ImportarAsync(http, coop.TokenAdmin, Ruta, "apply", conError), "Import.Invalid");
        (await EsVendedorAsync(http, coop.TokenAdmin, persona)).Should().BeFalse("con un error no se guarda nada");

        await InventarioE2E.RevisarYAplicarAsync(http, coop.TokenAdmin, Ruta, InventarioE2E.Libro(("Datos", encabezados, [[documento, null, "1", "no", "sí"]])));
        (await EsVendedorAsync(http, coop.TokenAdmin, persona)).Should().BeTrue();
        var lista = await InventarioE2E.GetAsync(http, coop.TokenAdmin, $"{Ruta}?search={documento}");
        lista.GetProperty("items").EnumerateArray().Should().ContainSingle();
    }

    private static async Task<bool> EsVendedorAsync(HttpClient http, string token, Guid persona) =>
        (await InventarioE2E.GetAsync(http, token, $"/api/core/people/{persona}")).GetProperty("isSalesperson").GetBoolean();
}
