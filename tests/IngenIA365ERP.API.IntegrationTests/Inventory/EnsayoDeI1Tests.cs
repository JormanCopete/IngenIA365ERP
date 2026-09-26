using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Security;
using IngenIA365ERP.Application.Inventory.Replenishment;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T442: el ensayo de I1 de punta a punta (quickstart §3 en el orden que cabe sin I2), un solo recorrido por HTTP en la
/// cooperativa aislada «ensayoi1», en el motor de <c>DB_PROVIDER</c>:
/// <list type="number">
/// <item>plantillas en orden (§2.5; los impuestos son la semilla tributaria): grupos, unidades, categorías, productos (lo hace
/// <see cref="EscenarioDeInventario"/>);</item>
/// <item>saldo inicial de PV1 todavía <c>NotActivated</c>, aprobado por otra persona, y activación de las tres bodegas fuera de
/// producción con la diferencia aceptada (§3.5, §4.2 en su parte de I1);</item>
/// <item>compra directa (§3.6), traslado en dos pasos PRIN → PV2 (§3.9), conteo en PV1 con su ajuste aprobado en dos niveles
/// (§3.10, §3.13), reorden y quiebre con la revisión disparada a mano (§3.12);</item>
/// <item>cierre del mes del corte y del anterior y reapertura del último (§3.11);</item>
/// <item>informes de I1 en <c>json</c> y <c>xlsx</c> (§3.14);</item>
/// <item>al final, integridad del kardex sin diferencias (SC-006) y de la auditoría sin incidentes (SC-012).</item>
/// </list>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class EnsayoDeI1Tests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_ensayo_de_I1_de_punta_a_punta()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "ensayoi1", activar: false);
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        var corte = esc.Corte.ToString("yyyy-MM-dd");
        var hoy = InventarioE2E.HoyEnColombia;

        // (2) Saldo inicial de PV1 y activación bodega por bodega.
        var saldo = await InventarioE2E.RevisarYAplicarAsync(http, esc.Admin, "/api/inventory/opening-balances", InventarioE2E.Libro(("Datos",
            ["bodega", "fechaDeCorte", "producto", "ubicacion", "cantidad", "costoUnitario"],
            [["PV1", corte, "P2", null, "30", "1000"], ["PV1", corte, "P4", null, "10", "1000"]])));
        var documentoDeSaldo = saldo.GetProperty("extra").GetProperty("documents")[0].GetProperty("documentPublicId").GetGuid();
        var enviado = await InventarioE2E.ConfirmarAsync(http, esc.Admin, "/api/inventory/opening-balances", documentoDeSaldo);
        var solicitudDeSaldo = enviado.GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitudDeSaldo, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var bodega in new[] { "PV1", "PRIN", "PV2" })
            await EscenarioDeInventario.ActivarAsync(http, esc.Admin, esc.Bodega(bodega), esc.Corte);
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PV1")).Should().Be(30m);

        // (3a) Compra directa de 3 cajas de P1 en PRIN.
        var proveedor = await Accounting.ContabilidadE2E.CrearPersonaAsync(http, esc.Admin, "ProveedorEnsayo");
        var compra = await InventarioE2E.MandarAsync(http, u.Comprador.Token, HttpMethod.Post, "/api/inventory/purchases/direct", new
        {
            receipt = new
            {
                documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = new[] { new { productPublicId = esc.P("P1").Id, unitPublicId = esc.P("P1").Caja!.Value, quantity = 3m, unitPrice = 15_600m } },
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new { prefix = "EN", number = "1", issueDate = hoy.ToString("yyyy-MM-dd"), paymentForm = "Cash", isElectronic = false },
            },
        });
        compra.StatusCode.Should().Be(HttpStatusCode.Created, await compra.Content.ReadAsStringAsync());
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PRIN")).Should().Be(36m);

        // (3b) Traslado en dos pasos PRIN → PV2.
        var traslado = (await InventarioE2E.BorradorAsync(http, u.BodegaA.Token, "/api/inventory/transfers", new
        {
            documentTypePublicId = esc.Tipos["TRD"], warehousePublicId = esc.Bodega("PRIN"), destinationWarehousePublicId = esc.Bodega("PV2"),
            lines = esc.Lineas(new EscenarioDeInventario.Linea("P1", 10)),
        })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, u.BodegaA.Token, HttpMethod.Post, $"/api/inventory/transfers/{traslado}/dispatch", new { });
        var lineaDespachada = (await InventarioE2E.GetAsync(http, u.BodegaB.Token, $"/api/inventory/transfers/{traslado}")).GetProperty("lines")[0]
            .GetProperty("dispatchLinePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, u.BodegaB.Token, HttpMethod.Post, $"/api/inventory/transfers/{traslado}/receive",
            new { lines = new[] { new { dispatchLinePublicId = lineaDespachada, receivedQuantity = 10m } } });
        (await esc.FisicaAsync(http, esc.Admin, "P1", "PV2")).Should().Be(10m);

        // (3c) Conteo de P2 en PV1 y su ajuste aprobado en dos niveles (el tipo de faltante de conteo con una versión de dos niveles).
        await InventarioE2E.PoliticaAsync(http, esc.Admin, esc.Tipos["CONN"], esc.Corte.AddDays(1),
            (0m, "Inventory.Counts.Approve"), (0m, "Inventory.Approvals.Management"));
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Put, "/api/inventory/reorder-policies", new
        {
            productPublicId = esc.P("P2").Id, warehousePublicId = esc.Bodega("PV1"), minimum = 10m, maximum = 50m, reorderPoint = 15m,
        });
        var conteo = (await InventarioE2E.BorradorAsync(http, esc.Admin, "/api/inventory/counts", new
        {
            documentTypePublicId = esc.Tipos["CON"], warehousePublicId = esc.Bodega("PV1"), kind = "Cyclic", scope = "Selection",
            productPublicIds = new[] { esc.P("P2").Id }, blind = false, counterUserPublicIds = new[] { u.BodegaA.PublicId },
        })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/counts/{conteo}/open", new { });
        foreach (var ronda in new[] { 1, 2 })
        {
            await InventarioE2E.ExitoAsync(http, u.BodegaA.Token, HttpMethod.Post, $"/api/inventory/counts/{conteo}/captures", new
            {
                round = ronda, reads = new[] { new { barcode = esc.P("P2").CodigoDeBarras, quantity = 8m, readAt = DateTime.UtcNow } },
            });
            var cierre = await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/counts/{conteo}/close", new { });
            if (ronda == 1) await InventarioE2E.FallaAsync(cierre, "Inventory.Count.RecountRequired");
            else cierre.StatusCode.Should().Be(HttpStatusCode.OK, await cierre.Content.ReadAsStringAsync());
        }
        var ajuste = (await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/counts/{conteo}/adjustment", new { }))
            .GetProperty("documents").EnumerateArray().Single();
        var solicitud = ajuste.GetProperty("approvalRequestPublicId").GetGuid();
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, solicitud, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PV1")).Should().Be(30m, "con un nivel aprobado sigue en aprobación");
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Jefe.Token, solicitud, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await esc.FisicaAsync(http, esc.Admin, "P2", "PV1")).Should().Be(8m);

        // (3d) Reorden y quiebre: la revisión disparada a mano levanta las dos.
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, TareaDeRevisionDeReorden.NombreDeLaTarea);
        var alertas = (await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/alerts?status=Pending&pageSize=100")).GetProperty("items").EnumerateArray()
            .Select(a => a.GetProperty("typeCode").GetString()).ToList();
        alertas.Should().Contain(["Inventario.Reorden", "Inventario.Quiebre"]);

        // (4) Cierre del mes del corte y del anterior; reapertura del último.
        var mesAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mesDelCorte = mesAnterior.AddMonths(-1);
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/periods/{mesDelCorte.Year}/{mesDelCorte.Month}/close", new { acknowledgeWarnings = true });
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/periods/{mesAnterior.Year}/{mesAnterior.Month}/close", new { acknowledgeWarnings = true });
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/periods/{mesAnterior.Year}/{mesAnterior.Month}/reopen",
            new { reason = "Reapertura del ensayo" });

        // (5) Informes de I1 en json y xlsx.
        var rango = $"from={esc.Corte:yyyy-MM-dd}&to={hoy:yyyy-MM-dd}";
        foreach (var (vista, filtros) in new[]
                 {
                     ("kardex", $"product={esc.P("P1").Id}&{rango}"), ("valuation", $"asOf={hoy:yyyy-MM-dd}&includeTransit=true"), ("stock", ""),
                     ("documents", rango), ("count-differences", rango), ("reorder-alerts", ""),
                 })
            foreach (var formato in new[] { "json", "xlsx" })
                (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Get, $"/api/reports/inventory/{vista}?format={formato}&{filtros}"))
                    .StatusCode.Should().Be(HttpStatusCode.OK, $"{vista} en {formato}");

        // (6) Integridad del kardex y de la auditoría.
        var kardex = await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/integrity/verify", new { });
        kardex.GetProperty("incidents").GetArrayLength().Should().Be(0, "SC-006");
        await fx.ReenviarAuditoriaAsync(esc.Coop.TenantPublicId);
        var auditoria = await InventarioE2E.ExitoAsync(http, u.Auditor.Token, HttpMethod.Post, "/api/audit/integrity/verify",
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        auditoria.GetProperty("incidents").GetArrayLength().Should().Be(0, "SC-012");
    }
}
