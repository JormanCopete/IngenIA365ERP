using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// Feature 020 (T181, recorrido de contado; R10, R14, R18): de punta a punta por la API, contra la base real y con el
/// <see cref="ProveedorFalso"/> como sistema externo. El maestro habilita Transporte y define el conector; la cooperativa pone credenciales,
/// configura el servicio (un producto de Comercial), registra el vehículo, lo suscribe y lo factura <b>de contado</b> en la caja abierta:
/// la factura sale por Comercial, el cargo queda pagado en la misma unidad de trabajo, nace un solo aviso y el despachador lo entrega por el
/// consumidor de conectores. Un 500 del sistema externo deja el envío reintentando y cada reintento lleva el mismo identificador externo.
/// La ruta a crédito (Cartera y Recaudo) usa el mismo emisor y el mismo consumidor desde <c>ApplyChargeAllocationsCommand</c>.
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class RecorridoDeContadoTests(ApiConProveedorFalsoFixture fx)
{
    private const decimal PrecioRadio = 50_000m;

    [Fact]
    public async Task Facturar_de_contado_avisa_una_sola_vez_y_los_reintentos_llevan_el_mismo_identificador()
    {
        var v = await EscenarioDeVentas.PrepararAsync(fx, "trncontado");
        var coop = v.Inv.Coop;
        var t = v.Inv.Admin;
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        var mes = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = hoy.ToString("yyyyMM");

        // 1. Transporte encendido y el producto del servicio en Comercial, con su precio.
        await TransporteE2E.HabilitarAsync(fx, http, coop.TenantPublicId);
        await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/products", InventarioE2E.Libro(
            ("Productos", ["codigo", "nombre", "tipo", "categoria", "marca", "unidadBase", "grupoContable", "tratamientoIva", "tarifaIva", "conceptoRetencion"],
                [["RADIO", "SERVICIO DE RADIO", "Service", "GENERAL", null, "UND", "ABARROTES", "Excluded", null, "COMPRAS"]])));
        var radio = (await InventarioE2E.GetAsync(http, t, "/api/inventory/products?pageSize=50")).GetProperty("items").EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "RADIO").GetProperty("publicId").GetGuid();
        var unidad = (await InventarioE2E.GetAsync(http, t, $"/api/inventory/products/{radio}")).GetProperty("baseUnit").GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{v.ListaGeneral}/items", new
        {
            items = new[] { new { productPublicId = radio, unitPublicId = unidad, price = PrecioRadio } }, reason = "Precio del servicio de radio",
        });

        // 2. El conector: el maestro lo define y lo habilita; la cooperativa recibe la copia y pone sus credenciales.
        var conector = await TransporteE2E.ConectorAsync(fx, http, "RADIOE2E", "https://proveedor.example.com", TransporteE2E.DefinicionDeReferencia);
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        (await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/saas/connectors/{conector}/tenants/{coop.TenantPublicId}",
            new { enabled = true, reason = "Prueba e2e" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await fx.CorrerTareaAsync(coop.TenantPublicId, "conectores.replicar");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "usuario-e2e", ["password"] = "clave-e2e" } });

        // 3. El servicio, la política de facturación, los vehículos y sus suscripciones.
        var servicio = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/services",
            new { productPublicId = radio, priority = 1, connectorPublicId = conector, isActive = true })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing-policies", new
        {
            validFrom = mes.ToString("yyyy-MM-dd"), proration = "FullMonth", salesDocumentTypeCode = "RV", warehouseCode = "PV1", salesPointCode = "PV1", dueDay = 0,
        });
        var dueno = await ContabilidadE2E.CrearPersonaAsync(http, t, "Propietario Contado");
        var a = await VehiculoSuscritoAsync(http, t, dueno, servicio, "TRN101", "0101", "101", mes);
        var b = await VehiculoSuscritoAsync(http, t, dueno, servicio, "TRN102", "0102", "102", mes);

        // 4. Caja abierta y factura de contado del vehículo A: un cargo pagado y un solo aviso, que sale al primer intento.
        await EscenarioDeVentas.AbrirAsync(http, t, v.Caja1);
        var factura = await FacturarAsync(http, t, a, periodo);
        factura.GetProperty("total").GetDecimal().Should().Be(PrecioRadio);
        var despachador = new ConductorDelDespachador(fx);
        await despachador.PasadasAsync(coop.TenantPublicId);

        var envioA = await EnvioDeAsync(http, t, "TRN101");
        envioA.GetProperty("status").GetString().Should().Be("Sent");
        var idA = envioA.GetProperty("externalId").GetGuid().ToString("D");
        fx.Proveedor.ExternalIds.Count(x => x == idA).Should().Be(1);

        // Facturar otra vez el mismo período no duplica la factura ni el aviso: lo rechaza diciendo por qué.
        var otraVez = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/transport/billing/single",
            new { vehiclePublicId = a, period = periodo, paymentMode = "Cash" });
        otraVez.IsSuccessStatusCode.Should().BeFalse();
        (await TransporteE2E.CodigoAsync(otraVez)).Should().Be("Billing.PeriodAlreadyBilled");
        await despachador.PasadasAsync(coop.TenantPublicId);
        fx.Proveedor.ExternalIds.Count(x => x == idA).Should().Be(1);

        // 5. Vehículo B: el sistema externo responde 500 dos veces; el envío reintenta y cada intento lleva el mismo identificador.
        fx.Proveedor.Responder(500, 500);
        await FacturarAsync(http, t, b, periodo);
        await despachador.PasadaAsync(coop.TenantPublicId);
        var envioB = await EnvioDeAsync(http, t, "TRN102");
        envioB.GetProperty("status").GetString().Should().Be("Retrying");
        for (var i = 0; i < 2; i++)
        {
            (await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/transport/notifications/{envioB.GetProperty("publicId").GetGuid()}/retry"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            await despachador.PasadaAsync(coop.TenantPublicId);
        }
        envioB = await EnvioDeAsync(http, t, "TRN102");
        envioB.GetProperty("status").GetString().Should().Be("Sent");
        var idB = envioB.GetProperty("externalId").GetGuid().ToString("D");
        fx.Proveedor.ExternalIds.Count(x => x == idB).Should().Be(3);
        envioB.GetProperty("attempts").GetInt32().Should().Be(3);

        // El detalle muestra los intentos con el cuerpo saneado (sin documento ni nombre del propietario).
        var detalle = await InventarioE2E.GetAsync(http, t, $"/api/transport/notifications/{envioB.GetProperty("publicId").GetGuid()}");
        detalle.GetProperty("attempts").EnumerateArray().Where(x => x.GetProperty("kind").GetString() == "Operation").Should().HaveCount(3);
        detalle.ToString().Should().NotContain("clave-e2e").And.NotContain("token-de-prueba");
    }

    private static async Task<Guid> VehiculoSuscritoAsync(HttpClient http, string t, Guid dueno, Guid servicio, string placa, string interno, string radio,
        DateOnly desde)
    {
        var alta = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/vehicles", new
        {
            vehicle = new { plate = placa, internalNumber = interno, radioCode = radio, joinedOn = desde.AddMonths(-1).ToString("yyyy-MM-dd") },
            ownerPersonPublicId = dueno,
        });
        var vehiculo = alta.GetProperty("vehiclePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/transport/vehicles/{vehiculo}/subscriptions",
            new { servicePublicId = servicio, from = desde.ToString("yyyy-MM-dd") });
        return vehiculo;
    }

    private static Task<JsonElement> FacturarAsync(HttpClient http, string t, Guid vehiculo, string periodo) =>
        InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/billing/single", new { vehiclePublicId = vehiculo, period = periodo, paymentMode = "Cash" });

    private static async Task<JsonElement> EnvioDeAsync(HttpClient http, string t, string placa)
    {
        var tablero = await InventarioE2E.GetAsync(http, t, "/api/transport/notifications");
        return tablero.GetProperty("items").EnumerateArray().Single(e => e.GetProperty("plate").GetString() == placa);
    }
}
