using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// 2026-11-25 (pedido del dueño: «facturar hoy y poder hacerlo varias veces, poder anularla y facturar sin restricción de forma masiva»), de punta a
/// punta por la API sobre la cooperativa de ventas a crédito con Cartera activada (<see cref="VentasConCarteraE2E"/>, una propia): una línea de
/// servicio sin interés, como la cuota de administración; dos vehículos de la misma asociada facturados a crédito en una corrida, cada factura
/// una obligación de Cartera.
/// <list type="bullet">
/// <item>Una <b>nota crédito total</b> sobre la factura del primero, hecha en Comercial, llega a Transporte (<c>NotaCreditoEmitida</c>) y anula su
/// cargo.</item>
/// <item><b>«Anular la corrida»</b> anula la otra con su documento contrario y deja su cargo anulado.</item>
/// <item>Después Cartera queda sin saldo y los asientos de la asociada en <b>cero neto</b> por cuenta.</item>
/// <item><b>Volver a facturar</b> el mismo período emite dos facturas nuevas, sin otro bloqueo, y lo vivo no se cobra dos veces.</item>
/// </list>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class AnularYVolverAFacturarTests(CentralIdentityApiFixture fx)
{
    private const decimal Cuota = 150_000m;

    [Fact]
    public async Task Facturar_anular_con_nota_y_por_corrida_y_volver_a_facturar_el_mismo_periodo()
    {
        var esc = await VentasConCarteraE2E.EscenarioAsync(fx, "trn_anular");
        var t = esc.Admin;
        using var http = fx.CreateClient();
        var hoy = esc.Hoy;
        var mes = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = hoy.ToString("yyyyMM");

        // 1. Transporte, la línea de servicio de Cartera (sin interés, de un solo pago) y el servicio por Comercial.
        await TransporteE2E.HabilitarAsync(fx, http, esc.TenantPublicId);
        var version = LineasE2E.VersionFija(hoy, "CONSUMO", "SinLibranza", 0m, "Cuota de administración (e2e)");
        version["allowsCommercialOrigin"] = true;
        version["interestTaxConcept"] = new { taxed = false, supportAttachmentPublicId = Guid.NewGuid() };
        version["amounts"] = new { min = 1m, max = 30_000_000m };
        version["terms"] = new { minMonths = 1, maxMonths = 12 };
        version["periodicities"] = new[] { "Monthly", "SinglePayment" };
        version["amortization"] = new { systems = new[] { "LevelPayment", "Bullet" } };
        var linea = await LineasE2E.CrearExitoAsync(http, t, "ADMTRN", "Cuota de administración", version);
        var marca = await CarteraE2E.ConClaveAsync(http, t, HttpMethod.Put, $"{LineasE2E.Lineas}/{linea}/service", new { isService = true, reason = "Servicio de Transporte (e2e)" });
        marca.IsSuccessStatusCode.Should().BeTrue($"marcar la línea de servicio: «{await marca.Content.ReadAsStringAsync()}»");

        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(
            ("Productos", ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"],
                [["ADMTRN", "CUOTA DE ADMINISTRACION", "Service", "GENERAL", null, "UND", "ABARROTES", "Excluded", null, "COMPRAS"]])));
        var producto = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "ADMTRN").GetProperty("publicId").GetGuid();
        var unidad = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{producto}")).GetProperty("baseUnit").GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{esc.Ventas.ListaGeneral}/items", new
        {
            items = new[] { new { productPublicId = producto, unitPublicId = unidad, price = Cuota } }, reason = "Cuota de administración",
        });
        var servicio = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/services", new
        {
            productPublicId = producto, priority = 1, creditLineCode = "ADMTRN", associateCreditMeansCode = "CREDASOC", isActive = true,
        })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing-policies", new
        {
            validFrom = mes.ToString("yyyy-MM-dd"), proration = "FullMonth", salesDocumentTypeCode = "RV", warehouseCode = "PV1", salesPointCode = "PV1", dueDay = 0,
        });
        var a = await VehiculoAsync(http, t, esc.Ximena, servicio, "ANU101", "A101", mes);
        var b = await VehiculoAsync(http, t, esc.Ximena, servicio, "ANU102", "A102", mes);

        // 2. La corrida a crédito: dos facturas, dos obligaciones en Cartera.
        var corrida = await CorrerAsync(http, t, periodo);
        corrida.GetProperty("invoicesCount").GetInt32().Should().Be(2, corrida.ToString());
        await VentasConCarteraE2E.DespacharAsync(fx, esc);
        (await SaldoDeCreditosAsync(http, t, esc.Ximena)).Should().Be(2 * Cuota, "cada factura de servicios es una obligación de Cartera");
        var facturaDeA = await FacturaVivaAsync(esc, a);

        // 3. Nota crédito total sobre la factura de A, hecha en Comercial: llega a Transporte y anula su cargo.
        var nota = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/sales/credit-notes", new
        {
            originDocumentPublicId = facturaDeA, documentTypePublicId = esc.Ventas.TipoNv, reason = "Se facturó por error (e2e)", totalVoid = true, withReturn = false,
            lines = Array.Empty<object>(), refunds = Array.Empty<object>(),
        });
        var idNota = nota.GetProperty("documentPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/sales/credit-notes/{idNota}/confirm",
            new { expectedAmountDue = nota.GetProperty("totals").GetProperty("amountDue").GetDecimal() });
        await VentasConCarteraE2E.DespacharAsync(fx, esc);
        (await EstadosDeCargosAsync(http, t, a)).Should().Equal(["Voided"], "la nota de anulación total anula el cargo en Transporte");

        // 4. «Anular la corrida»: la de B, con su documento contrario; la de A ya no cuenta.
        var anulacion = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post,
            $"/api/transport/billing/runs/{corrida.GetProperty("publicId").GetGuid()}/void", new { reason = "Se vuelve a facturar el período (e2e)" });
        anulacion.GetProperty("voidedInvoices").GetInt32().Should().Be(1, anulacion.ToString());
        anulacion.GetProperty("omitted").GetArrayLength().Should().Be(0, anulacion.ToString());
        anulacion.GetProperty("pending").GetInt32().Should().Be(0);
        await VentasConCarteraE2E.DespacharAsync(fx, esc);
        (await EstadosDeCargosAsync(http, t, b)).Should().Equal(["Voided"]);

        // 5. Cartera sin saldo y los asientos de la asociada en cero neto por cuenta.
        (await SaldoDeCreditosAsync(http, t, esc.Ximena)).Should().Be(0m, "la nota y la anulación ajustan las dos obligaciones");
        var porCuenta = await VentasConCarteraE2E.EnLaBaseAsync(fx, esc, async db =>
        {
            var persona = await db.People.AsNoTracking().Where(p => p.PublicId == esc.Ximena).Select(p => p.Id).SingleAsync();
            return await db.JournalEntries.AsNoTracking().Where(j => j.PersonId == persona && j.IsPosted && !j.IsDeleted)
                .GroupBy(j => j.AccountId).Select(g => new { Cuenta = g.Key, Lineas = g.Count(), Neto = g.Sum(j => j.Debit - j.Credit) }).ToListAsync();
        });
        porCuenta.Should().NotBeEmpty("las facturas y sus anulaciones pasaron a Contabilidad");
        porCuenta.Should().OnlyContain(c => c.Neto == 0m, string.Join("; ", porCuenta.Select(c => $"cuenta {c.Cuenta}: {c.Neto}")));

        // 6. Volver a facturar el mismo período: dos facturas nuevas, sin otro bloqueo.
        var otra = await CorrerAsync(http, t, periodo);
        otra.GetProperty("invoicesCount").GetInt32().Should().Be(2, otra.ToString());
        otra.GetProperty("alreadyBilledCount").GetInt32().Should().Be(0);
        await VentasConCarteraE2E.DespacharAsync(fx, esc);
        (await SaldoDeCreditosAsync(http, t, esc.Ximena)).Should().Be(2 * Cuota);
        (await EstadosDeCargosAsync(http, t, a)).Should().BeEquivalentTo(["Voided", "Open"]);
        (await FacturaVivaAsync(esc, a)).Should().NotBe(facturaDeA, "es otra factura");

        // Y una tercera vez no cobra lo vivo dos veces.
        var tercera = await CorrerAsync(http, t, periodo);
        tercera.GetProperty("invoicesCount").GetInt32().Should().Be(0);
        tercera.GetProperty("alreadyBilledCount").GetInt32().Should().Be(2);
    }

    private static async Task<Guid> VehiculoAsync(HttpClient http, string t, Guid dueno, Guid servicio, string placa, string interno, DateOnly desde)
    {
        var alta = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/vehicles", new
        {
            vehicle = new { plate = placa, internalNumber = interno, joinedOn = desde.AddMonths(-1).ToString("yyyy-MM-dd") }, ownerPersonPublicId = dueno,
        });
        var vehiculo = alta.GetProperty("vehiclePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions",
            new { servicePublicId = servicio, from = desde.ToString("yyyy-MM-dd") });
        return vehiculo;
    }

    /// <summary>Abre una corrida a crédito del período y la avanza por tandas hasta que termina.</summary>
    private static async Task<JsonElement> CorrerAsync(HttpClient http, string t, string periodo)
    {
        var corrida = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing/runs", new { period = periodo, paymentMode = "Credit" });
        var id = corrida.GetProperty("publicId").GetGuid();
        for (var i = 0; i < 20 && corrida.GetProperty("status").GetInt32() is 1 or 2; i++)
            corrida = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/billing/runs/{id}/next", null);
        corrida.GetProperty("status").GetInt32().Should().BeOneOf([3, 4], corrida.ToString());
        corrida.GetProperty("omittedCount").GetInt32().Should().Be(0, corrida.ToString());
        return corrida;
    }

    private static async Task<decimal> SaldoDeCreditosAsync(HttpClient http, string t, Guid persona) =>
        (await CarteraE2E.GetAsync(http, t, $"/api/lending/person-statement?personPublicId={persona}")).GetProperty("totales").GetProperty("saldoDeCreditos").GetDecimal();

    private static async Task<List<string>> EstadosDeCargosAsync(HttpClient http, string t, Guid vehiculo) =>
        (await InventarioE2E.GetAsync(http, t, $"/api/transport/charges?vehicle={vehiculo}")).EnumerateArray()
        .Select(c => c.GetProperty("status").GetInt32() switch { 1 => "Open", 2 => "Paid", 3 => "Voided", var x => x.ToString() }).ToList();

    /// <summary>La factura del cargo vivo del vehículo (el listado de cargos no trae el identificador de la factura).</summary>
    private Task<Guid> FacturaVivaAsync(VentasConCarteraE2E.Escenario esc, Guid vehiculo) =>
        VentasConCarteraE2E.EnLaBaseAsync(fx, esc, db => db.ServiceCharges.AsNoTracking()
            .Where(c => c.Vehicle!.PublicId == vehiculo && c.Status != Domain.Enums.Transport.ChargeStatus.Voided)
            .Select(c => c.InvoicePublicId).SingleAsync());
}
