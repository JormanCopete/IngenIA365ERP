using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// 2026-11-25 (pedido del dueño), de punta a punta por la API: la lista de vehículos filtra por servicio en el servidor y trae sus servicios; la
/// exportación descarga lo filtrado; la revisión del período avisa de un mes anterior y de suscripciones que empiezan después del día 1;
/// una suscripción que todavía no empieza se quita (y deja de contar), una facturada no se quita y se termina con la fecha que se elija; y
/// abrir otra corrida del mismo período con una en curso responde <c>Billing.RunInProgress</c>, con su último avance para «Continuar».
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class SuscripcionesYFiltrosTests(ApiConProveedorFalsoFixture fx)
{
    [Fact]
    public async Task Filtros_por_servicio_quitar_terminar_y_avisos_antes_de_facturar()
    {
        var v = await EscenarioDeVentas.PrepararAsync(fx, "trnsuscripciones");
        var coop = v.Inv.Coop;
        var t = v.Inv.Admin;
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mes = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = hoy.ToString("yyyyMM");
        var anterior = mes.AddMonths(-1).ToString("yyyyMM");

        await TransporteE2E.HabilitarAsync(fx, http, coop.TenantPublicId);
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(
            ("Productos", ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"],
                [["CADMON", "CUOTA DE ADMINISTRACION", "Service", "GENERAL", null, "UND", "ABARROTES", "Excluded", null, "COMPRAS"]])));
        var producto = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "CADMON").GetProperty("publicId").GetGuid();
        var unidad = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{producto}")).GetProperty("baseUnit").GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{v.ListaGeneral}/items", new
        {
            items = new[] { new { productPublicId = producto, unitPublicId = unidad, price = 73_425m } }, reason = "Cuota de administración",
        });
        var servicio = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/services",
            new { productPublicId = producto, priority = 1, isActive = true })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing-policies", new
        {
            validFrom = mes.AddMonths(-1).ToString("yyyy-MM-dd"), proration = "FullMonth", salesDocumentTypeCode = "RV", warehouseCode = "PV1",
            salesPointCode = "PV1", dueDay = 0,
        });

        var dueno = await ContabilidadE2E.CrearPersonaAsync(http, t, "Propietario Suscripciones");
        var a = await VehiculoAsync(http, t, dueno, "SUS101", "S101");
        var b = await VehiculoAsync(http, t, dueno, "SUS102", "S102");
        var c = await VehiculoAsync(http, t, dueno, "SUS103", "S103");
        var d = await VehiculoAsync(http, t, dueno, "SUS104", "S104");
        await SuscribirAsync(http, t, a, servicio, mes);
        var deC = await SuscribirAsync(http, t, c, servicio, mes.AddMonths(1));
        var deD = await SuscribirAsync(http, t, d, servicio, mes.AddDays(9));
        _ = b;

        // 1. Filtros por servicio, en el servidor, y la columna de servicios (la de C empieza el mes que viene).
        (await InternosAsync(http, t, $"service={servicio}&serviceFilter=Subscribed")).Should().Equal("S101", "S103", "S104");
        (await InternosAsync(http, t, $"service={servicio}&serviceFilter=NotSubscribed")).Should().Equal("S102");
        (await InternosAsync(http, t, "serviceFilter=None")).Should().Equal("S102");
        var lista = await TransporteE2E.GetAsync(http, t, "/api/transport/vehicles?q=SUS103");
        var deLaFila = lista.GetProperty("items").EnumerateArray().Single().GetProperty("services").EnumerateArray().Single();
        deLaFila.GetProperty("upcoming").GetBoolean().Should().BeTrue();
        lista.GetProperty("totalCount").GetInt32().Should().Be(1);

        // 2. La exportación descarga lo filtrado (el informe de vehículos con los mismos filtros).
        var informe = await TransporteE2E.GetAsync(http, t, $"/api/reports/transport/vehiculos?service={servicio}&serviceFilter=Subscribed");
        informe.GetProperty("filas").GetArrayLength().Should().Be(3);
        var excel = await http.SendAsync(Pedido(t, $"/api/reports/transport/vehiculos?format=xlsx&serviceFilter=None"));
        excel.StatusCode.Should().Be(HttpStatusCode.OK);
        excel.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheet");

        // 3. Avisos antes de facturar: el mes anterior y la suscripción de D, que empieza el día 10.
        (await RevisarAsync(http, t, anterior)).GetProperty("beforeCurrentMonth").GetBoolean().Should().BeTrue();
        var revision = await RevisarAsync(http, t, periodo);
        revision.GetProperty("startedAfterFirstDay").GetInt32().Should().Be(1);
        revision.GetProperty("notices").GetArrayLength().Should().Be(1);

        // 4. Quitar las que no se facturaron: la de C (todavía no empieza) y la de D; D deja de contar en la revisión.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{c}/subscriptions/{deC}/remove", new { reason = "Se cargó por error" });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{d}/subscriptions/{deD}/remove", new { reason = "El mes ya estaba cobrado" });
        (await RevisarAsync(http, t, periodo)).GetProperty("notices").GetArrayLength().Should().Be(0);
        (await InternosAsync(http, t, "serviceFilter=None")).Should().Equal("S102", "S103", "S104");
        (await TransporteE2E.GetAsync(http, t, $"/api/transport/vehicles/{c}")).GetProperty("subscriptions").GetArrayLength().Should().Be(0);

        // 5. Una facturada no se quita: se termina con la fecha que se elija, con motivo.
        await EscenarioDeVentas.AbrirAsync(http, t, v.Caja1);
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing/single", new { vehiclePublicId = a, period = periodo, paymentMode = "Cash" });
        var deA = (await TransporteE2E.GetAsync(http, t, $"/api/transport/vehicles/{a}")).GetProperty("subscriptions").EnumerateArray().Single();
        deA.GetProperty("billed").GetBoolean().Should().BeTrue();
        var suscripcionDeA = deA.GetProperty("publicId").GetGuid();
        var quitarFacturada = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{a}/subscriptions/{suscripcionDeA}/remove",
            new { reason = "No debe poder" });
        (await TransporteE2E.CodigoAsync(quitarFacturada)).Should().Be("Subscription.Billed");
        var sinMotivo = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{a}/subscriptions/{suscripcionDeA}/end",
            new { to = mes.AddMonths(2).AddDays(-1).ToString("yyyy-MM-dd") });
        sinMotivo.IsSuccessStatusCode.Should().BeFalse("terminar exige motivo");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{a}/subscriptions/{suscripcionDeA}/end",
            new { to = mes.AddMonths(2).AddDays(-1).ToString("yyyy-MM-dd"), reason = "Se retira de la administración" });
        var terminada = (await TransporteE2E.GetAsync(http, t, $"/api/transport/vehicles/{a}")).GetProperty("subscriptions").EnumerateArray().Single();
        DateOnly.Parse(terminada.GetProperty("to").GetString()!).Should().Be(mes.AddMonths(2).AddDays(-1));
        terminada.GetProperty("reason").GetString().Should().Be("Se retira de la administración");

        // 6. Una corrida en curso del período: abrir otra responde Billing.RunInProgress, y la en curso dice su último avance.
        var corrida = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing/runs", new { period = periodo, paymentMode = "Cash" });
        var otra = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/transport/billing/runs", new { period = periodo, paymentMode = "Cash" });
        (await TransporteE2E.CodigoAsync(otra)).Should().Be("Billing.RunInProgress");
        var enCurso = (await TransporteE2E.GetAsync(http, t, $"/api/transport/billing/runs?period={periodo}")).EnumerateArray()
            .Single(r => r.GetProperty("publicId").GetGuid() == corrida.GetProperty("publicId").GetGuid());
        enCurso.GetProperty("lastActivityAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        var avance = corrida;
        for (var i = 0; i < 10 && avance.GetProperty("status").GetInt32() is 1 or 2; i++)
            avance = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/billing/runs/{corrida.GetProperty("publicId").GetGuid()}/next", null);
        avance.GetProperty("status").GetInt32().Should().BeOneOf(3, 4);
        avance.GetProperty("alreadyBilledCount").GetInt32().Should().Be(1, "el vehículo A ya tenía su factura del período");
    }

    private static async Task<Guid> VehiculoAsync(HttpClient http, string t, Guid dueno, string placa, string interno)
    {
        var alta = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/vehicles", new
        {
            vehicle = new { plate = placa, internalNumber = interno, joinedOn = "2026-01-01" }, ownerPersonPublicId = dueno,
        });
        return alta.GetProperty("vehiclePublicId").GetGuid();
    }

    private static async Task<Guid> SuscribirAsync(HttpClient http, string t, Guid vehiculo, Guid servicio, DateOnly desde)
    {
        var r = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions",
            new { servicePublicId = servicio, from = desde.ToString("yyyy-MM-dd") });
        r.IsSuccessStatusCode.Should().BeTrue(await r.Content.ReadAsStringAsync());
        return (await TransporteE2E.GetAsync(http, t, $"/api/transport/vehicles/{vehiculo}")).GetProperty("subscriptions").EnumerateArray().Single()
            .GetProperty("publicId").GetGuid();
    }

    private static async Task<List<string>> InternosAsync(HttpClient http, string t, string filtros) =>
        (await TransporteE2E.GetAsync(http, t, $"/api/transport/vehicles?q=SUS&{filtros}&pageSize=50")).GetProperty("items").EnumerateArray()
        .Select(i => i.GetProperty("internalNumber").GetString()!).ToList();

    private static Task<JsonElement> RevisarAsync(HttpClient http, string t, string periodo) =>
        TransporteE2E.GetAsync(http, t, $"/api/transport/billing/check?period={periodo}");

    private static HttpRequestMessage Pedido(string token, string url)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return pedido;
    }
}
