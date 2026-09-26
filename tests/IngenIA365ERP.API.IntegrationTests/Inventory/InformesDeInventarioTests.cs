using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T950 (quickstart §3.14; US17-1, US17-3; FR-086, FR-087), en el motor de <c>DB_PROVIDER</c>, sobre el escenario aislado
/// «informes»: <c>documents</c> y <c>reorder-alerts</c> salen en <c>json</c> con <c>Inventory.Reports.View</c> y en <c>xlsx</c> y
/// <c>pdf</c> sólo con <c>Inventory.Reports.Export</c> (sin él, 404), cada exportación con su evento
/// <c>Inventory.Report.Exported</c>; quien sólo tiene PV2 sólo ve PV2; <c>documents</c> con contraparte exportado sin
/// <c>Inventory.Reports.ExportPersonalData</c> es 404; y el valorizado a un corte pasado coincide con lo que fijó el cierre en
/// <c>INV_PeriodClosingBalances</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class InformesDeInventarioTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Ver_exportar_alcance_datos_personales_y_valorizado_de_un_corte_cerrado()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "informes");
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mesAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mesDelCorte = mesAnterior.AddMonths(-1);
        var finDelMesAnterior = mesAnterior.AddMonths(1).AddDays(-1);

        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10, 1_000m)], fecha: mesAnterior.AddDays(4));
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV2", [new("P2", 4, 500m)], fecha: mesAnterior.AddDays(5));
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorInforme");
        var compra = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/purchases/direct", new
        {
            receipt = new
            {
                documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = new[] { new { productPublicId = esc.P("P3").Id, unitPublicId = esc.P("P3").Unidad, quantity = 2m, unitPrice = 600m } },
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new { prefix = "IN", number = "77", issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            },
        });
        compra.StatusCode.Should().Be(HttpStatusCode.Created, await compra.Content.ReadAsStringAsync());
        var rango = $"from={mesAnterior:yyyy-MM-dd}&to={hoy:yyyy-MM-dd}";

        // (1) json con Reports.View; archivo sólo con Reports.Export (aprobador tiene Inventory.*.View, no Export).
        (await InventarioE2E.MandarAsync(http, u.Aprobador.Token, HttpMethod.Get, $"/api/reports/inventory/documents?format=json&{rango}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var formato in new[] { "xlsx", "pdf" })
        {
            (await InventarioE2E.MandarAsync(http, u.Aprobador.Token, HttpMethod.Get, $"/api/reports/inventory/reorder-alerts?format={formato}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, $"exportar a {formato} exige Inventory.Reports.Export");
            (await InventarioE2E.MandarAsync(http, u.Jefe.Token, HttpMethod.Get, $"/api/reports/inventory/reorder-alerts?format={formato}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // (2) Con contraparte, exportar exige además ExportPersonalData (jefe exporta pero no tiene datos personales).
        var sinAjustes = $"/api/reports/inventory/documents?format=xlsx&{rango}";
        (await InventarioE2E.MandarAsync(http, u.Jefe.Token, HttpMethod.Get, sinAjustes)).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "la compra trae la contraparte");
        (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Get, sinAjustes)).StatusCode.Should().Be(HttpStatusCode.OK);

        // (3) Cada exportación deja su evento.
        await fx.ReenviarAuditoriaAsync(esc.Coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var auditoria = await InventarioE2E.GetAsync(http, esc.Admin,
            $"/api/audit/logs?action=Inventory.Report.Exported&from={DateTime.UtcNow.AddHours(-1):O}&to={DateTime.UtcNow.AddMinutes(1):O}&pageSize=50");
        auditoria.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(3, "dos de reorden y una de documentos");

        // (4) Quien sólo tiene PV2 sólo ve PV2.
        await InventarioE2E.RolAsync(http, esc.Coop, "ENSINFB", null, ["Inventory.Reports.View"]);
        var (soloPv2, usuario) = await InventarioE2E.UsuarioAsync(fx, http, esc.Coop, $"informes.b.{esc.Coop.TenantPublicId:N}@coop.inventario.test", "ENSINFB");
        await InventarioE2E.AlcanceAsync(http, esc.Admin, usuario, esc.Bodega("PV2"));
        var documentos = await InventarioE2E.InformeAsync(http, soloPv2, "documents", rango);
        documentos.Filas.Should().NotBeEmpty();
        documentos.Filas.Should().OnlyContain(f => Accounting.ContabilidadE2E.Tabla.Texto(f, documentos.Indice("Bodega")).Contains("PV2"));

        // (5) El valorizado de un corte cerrado coincide con INV_PeriodClosingBalances.
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/periods/{mesDelCorte.Year}/{mesDelCorte.Month}/close", new { acknowledgeWarnings = true });
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/periods/{mesAnterior.Year}/{mesAnterior.Month}/close", new { acknowledgeWarnings = true });
        var fijado = Convert.ToDecimal(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            """SELECT COALESCE(SUM(b."Value"), 0) FROM dbo."INV_PeriodClosingBalances" b JOIN dbo."INV_Periods" p ON p."Id" = b."PeriodId" WHERE b."Superseded" = false AND b."IsDeleted" = false AND p."Year" = """ + mesAnterior.Year + """ AND p."Month" = """ + mesAnterior.Month,
            "SELECT COALESCE(SUM(b.[Value]), 0) FROM [dbo].[INV_PeriodClosingBalances] b JOIN [dbo].[INV_Periods] p ON p.[Id] = b.[PeriodId] WHERE b.[Superseded] = 0 AND b.[IsDeleted] = 0 AND p.[Year] = "
                + mesAnterior.Year + " AND p.[Month] = " + mesAnterior.Month));
        fijado.Should().Be(12_000m, "10 × 1.000 en PRIN y 4 × 500 en PV2");
        var valorizado = await InventarioE2E.InformeAsync(http, esc.Admin, "valuation", $"asOf={finDelMesAnterior:yyyy-MM-dd}");
        valorizado.Numero(valorizado.Totales!.Value, "Valor").Should().Be(fijado, "se reconstruye desde el kardex y coincide con lo fijado (US17-1)");
    }
}
