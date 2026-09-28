using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T421 (quickstart §3.13 paso 5; US12-3; FR-012; api.md §7), en el motor de <c>DB_PROVIDER</c>, sobre el escenario aislado
/// «parametros»: <c>Existencias.StockNegativoPermitido = true</c> desde el primer día de M+2 no cambia los documentos de hoy
/// (siguen rechazando el negativo) y el historial muestra el valor nuevo con su autor y su motivo; <c>Costeo.Metodo</c> a mitad
/// de período responde <c>Parameters.RequiresPeriodStart</c>; un valor no admitido, <c>Parameters.ValueNotAllowed</c> con
/// <c>data.allowed</c>; y una clave <c>Costeo.*</c> sin <c>Inventory.Costing.Manage</c>, <c>Parameters.PermissionRequired</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ParametrosConVigenciaTests(CentralIdentityApiFixture fx)
{
    private const string Parametros = "/api/inventory/parameters/INV";

    [Fact]
    public async Task Una_vigencia_futura_no_cambia_los_documentos_de_hoy_y_queda_en_el_historial()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "parametros");
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mMas2 = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(2);

        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Parametros}/Existencias.StockNegativoPermitido/versions", new
        {
            scopeKind = "None", value = "true", validFrom = mMas2.ToString("yyyy-MM-dd"), reason = "Temporada de alta rotación",
        });

        var salida = await esc.AjusteAsync(http, esc.Admin, "AJN", "PRIN", [new("P1", 1)], causa: "MERMA");
        await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", salida), "Inventory.Stock.Insufficient");

        var historial = await InventarioE2E.GetAsync(http, esc.Admin, $"{Parametros}/Existencias.StockNegativoPermitido/history");
        var filas = (historial.ValueKind == System.Text.Json.JsonValueKind.Array ? historial : historial.GetProperty("items")).EnumerateArray().ToList();
        var nueva = filas.Single(f => f.GetProperty("validFrom").GetString() == mMas2.ToString("yyyy-MM-dd"));
        nueva.GetProperty("value").GetString().Should().Be("true");
        nueva.GetProperty("reason").GetString().Should().Be("Temporada de alta rotación");
        nueva.GetProperty("createdBy").ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task El_costeo_exige_inicio_de_periodo_valor_admitido_y_su_permiso()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "parametros");
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mitad = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1).AddDays(14);

        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Parametros}/Costeo.Metodo/versions", new
        {
            scopeKind = "None", value = "PromedioPonderado", validFrom = mitad.ToString("yyyy-MM-dd"), reason = "A mitad de mes",
        }), "Parameters.RequiresPeriodStart");

        var noAdmitido = await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"{Parametros}/Costeo.Ambito/versions", new
        {
            scopeKind = "None", value = "Sucursal", validFrom = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1).ToString("yyyy-MM-dd"), reason = "No existe",
        }), "Parameters.ValueNotAllowed");
        noAdmitido.GetProperty("data").GetProperty("allowed").EnumerateArray().Select(v => v.GetString()).Should().Contain(["Cooperativa", "Bodega"]);

        await InventarioE2E.RolAsync(http, esc.Coop, "ENSPARAM", null, ["Inventory.Parameters.View", "Inventory.Parameters.Manage"]);
        var (token, _) = await InventarioE2E.UsuarioAsync(fx, http, esc.Coop, $"parametros.{esc.Coop.TenantPublicId:N}@coop.inventario.test", "ENSPARAM");
        var sinPermiso = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Parametros}/Costeo.Ambito/versions", new
        {
            scopeKind = "None", value = "Bodega", validFrom = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1).ToString("yyyy-MM-dd"), reason = "Sin permiso de costeo",
        });
        (await InventarioE2E.FallaAsync(sinPermiso, "Parameters.PermissionRequired")).GetProperty("data").ToString().Should().Contain("Inventory.Costing.Manage");
    }

    /// <summary>
    /// Desde I3 hay tipos fiscales operables (la factura de venta): dejar sin paso la cadena de ventas los nombra y exige la
    /// confirmación explícita (<c>Inventory.PostingMode.FiscalRequiresConfirmation</c>, FR-075, quickstart §2.6); con ella, se
    /// registra. Hasta I3 la prueba estaba omitida porque ningún tipo fiscal alcanzaba la regla por HTTP.
    /// </summary>
    [Fact]
    public async Task NoPasa_en_la_cadena_de_ventas_exige_confirmar_los_tipos_fiscales()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "parametros");
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mMas2 = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(2);

        var alta = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/document-types", new
        {
            code = "FVE", name = "Factura electrónica de venta", @class = "SalesInvoice",
            requiresCounterparty = true, requiresCostCenter = false, requiresReason = false, requiresExternalReference = false,
        });
        alta.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());

        var sinConfirmar = await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post,
            $"{Parametros}/Contabilidad.ModoDePaso/versions", new
            {
                scopeKind = "DocumentType", chain = "Sales", value = "NoPasa", validFrom = mMas2.ToString("yyyy-MM-dd"), reason = "Ventas sin contabilidad",
            }), "Inventory.PostingMode.FiscalRequiresConfirmation");
        sinConfirmar.GetProperty("data").GetProperty("fiscalDocumentTypes").EnumerateArray()
            .Select(t => t.GetProperty("code").GetString()).Should().Contain("FVE");

        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"{Parametros}/Contabilidad.ModoDePaso/versions", new
        {
            scopeKind = "DocumentType", chain = "Sales", value = "NoPasa", validFrom = mMas2.ToString("yyyy-MM-dd"), reason = "Ventas sin contabilidad",
            confirmFiscalWithoutPosting = true,
        });
    }
}
