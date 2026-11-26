using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// 2026-11-18 (feature 020, pedido del dueño): la radio se vende en el POS con la placa o el lateral del vehículo en la línea y el aviso al
/// sistema externo depende del interruptor <c>Transporte.AvisoAlSistemaExterno</c>, apagado por defecto. Por HTTP y contra la base real:
/// <list type="number">
/// <item>sin la placa, la venta no se cobra (<c>Inventory.Line.ReferenceRequired</c>), y una placa que no está en el parque tampoco
/// (<c>Inventory.Line.ReferenceInvalid</c>);</item>
/// <item>con el aviso apagado, la venta con el lateral se cobra, se guarda la placa y no sale nada al proveedor;</item>
/// <item>un número que no es de ningún vehículo del parque es el lateral de un vehículo externo (2026-11-25, #199): la venta se cobra, la línea
/// guarda ese lateral y el cargo nace sin vehículo («Externo, lateral …»);</item>
/// <item>encendido, la siguiente venta avisa una vez, con el lateral del vehículo y el valor de la línea; lo vendido antes no se avisa.</item>
/// </list>
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class VentaConPlacaTests(ApiConProveedorFalsoFixture fx)
{
    private const decimal PrecioRadio = 45_000m;

    [Fact]
    public async Task La_radio_vendida_en_el_POS_con_el_lateral_avisa_solo_con_el_interruptor_encendido()
    {
        var v = await EscenarioDeVentas.PrepararAsync(fx, "trnplaca");
        var coop = v.Inv.Coop;
        var t = v.Inv.Admin;
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;

        // Transporte encendido, el producto RADIO con su precio y el conector con credenciales.
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
        var conector = await TransporteE2E.ConectorAsync(fx, http, "PLACAE2E", "https://proveedor.example.com", TransporteE2E.DefinicionDeReferencia);
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        (await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/saas/connectors/{conector}/tenants/{coop.TenantPublicId}",
            new { enabled = true, reason = "Prueba e2e" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await fx.CorrerTareaAsync(coop.TenantPublicId, "conectores.replicar");
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/core/connectors/{conector}/credentials",
            new { fields = new Dictionary<string, string> { ["username"] = "usuario-e2e", ["password"] = "clave-e2e" } });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/services",
            new { productPublicId = radio, priority = 1, connectorPublicId = conector, isActive = true });

        // Dos vehículos; el lateral es el código radio-móvil (el que va al proveedor).
        var dueno = await ContabilidadE2E.CrearPersonaAsync(http, t, "Propietario Placa");
        await VehiculoAsync(http, t, dueno, "PLA101", "1101", "701", hoy);
        await VehiculoAsync(http, t, dueno, "PLA102", "1102", "702", hoy);

        var productos = await InventarioE2E.GetAsync(http, t, "/api/inventory/sales/line-references/products");
        productos.EnumerateArray().Select(p => p.GetProperty("productPublicId").GetGuid()).Should().Contain(radio);
        var resuelta = await InventarioE2E.GetAsync(http, t, $"/api/inventory/sales/line-references/resolve?product={radio}&q=701");
        resuelta.GetProperty("valor").GetString().Should().Be("PLA101");

        var sesion = await EscenarioDeVentas.AbrirAsync(http, t, v.Caja1);
        var despachador = new ConductorDelDespachador(fx);
        var antes = fx.Proveedor.Cuerpos.Count;

        // 1. Sin la placa no se cobra; con una placa que no está en el parque, tampoco (un número sí: es el lateral de un externo, paso 2b).
        var venta = await EscenarioDeVentas.NuevaVentaAsync(http, t, sesion);
        var id = EscenarioDeVentas.VentaId(venta);
        venta = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/pos/drafts/{id}/lines",
            new { productPublicId = radio, unitPublicId = unidad, quantity = 1m });
        var linea = venta.GetProperty("lines").EnumerateArray().Single().GetProperty("linePublicId").GetGuid();
        var sinPlaca = await EscenarioDeVentas.PedirCobrarAsync(http, t, id, PrecioRadio, [v.Efectivo(PrecioRadio)]);
        sinPlaca.IsSuccessStatusCode.Should().BeFalse();
        (await TransporteE2E.CodigoAsync(sinPlaca)).Should().Be("Inventory.Line.ReferenceRequired");

        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Patch, $"/api/inventory/pos/drafts/{id}/lines/{linea}", new { itemReference = "ZZZ999" });
        var inexistente = await EscenarioDeVentas.PedirCobrarAsync(http, t, id, PrecioRadio, [v.Efectivo(PrecioRadio)]);
        inexistente.IsSuccessStatusCode.Should().BeFalse("una placa fuera del parque se sigue rechazando");
        (await TransporteE2E.CodigoAsync(inexistente)).Should().Be("Inventory.Line.ReferenceInvalid");

        // 2. Con el lateral y el aviso apagado (el defecto): se cobra, la línea guarda la placa y no sale nada.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Patch, $"/api/inventory/pos/drafts/{id}/lines/{linea}", new { itemReference = "701" });
        await EscenarioDeVentas.CobrarAsync(http, t, id, PrecioRadio, [v.Efectivo(PrecioRadio)]);
        await despachador.PasadasAsync(coop.TenantPublicId);
        fx.Proveedor.Cuerpos.Count.Should().Be(antes, "con el aviso apagado no se avisa");
        (await InventarioE2E.GetAsync(http, t, "/api/transport/notifications")).GetProperty("items").EnumerateArray()
            .Should().NotContain(e => e.GetProperty("plate").GetString() == "PLA101");

        // 2b. Un número que no es de ningún vehículo del parque es el lateral de un vehículo externo (#199): se resuelve como «fuera del parque»,
        // la venta se cobra con ese lateral y el cargo nace sin vehículo. Con el aviso apagado no sale nada.
        var externo = await InventarioE2E.GetAsync(http, t, $"/api/inventory/sales/line-references/resolve?product={radio}&q=999");
        externo.GetProperty("valor").GetString().Should().Be("999");
        externo.GetProperty("etiqueta").GetString().Should().Be("lateral 999 · fuera del parque");
        var deExterno = await EscenarioDeVentas.NuevaVentaAsync(http, t, sesion);
        var deExternoId = EscenarioDeVentas.VentaId(deExterno);
        deExterno = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/pos/drafts/{deExternoId}/lines",
            new { productPublicId = radio, unitPublicId = unidad, quantity = 1m });
        var lineaDeExterno = deExterno.GetProperty("lines").EnumerateArray().Single().GetProperty("linePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Patch, $"/api/inventory/pos/drafts/{deExternoId}/lines/{lineaDeExterno}", new { itemReference = "999" });
        await EscenarioDeVentas.CobrarAsync(http, t, deExternoId, PrecioRadio, [v.Efectivo(PrecioRadio)]);
        await despachador.PasadasAsync(coop.TenantPublicId);
        var cargos = (await InventarioE2E.GetAsync(http, t, "/api/transport/charges")).EnumerateArray().ToList();
        var cargoExterno = cargos.Should().ContainSingle(c => c.GetProperty("plate").GetString() == "Externo, lateral 999").Subject;
        cargoExterno.GetProperty("vehiclePublicId").ValueKind.Should().Be(JsonValueKind.Null, "el externo no es un vehículo del parque");
        cargoExterno.GetProperty("amount").GetDecimal().Should().Be(PrecioRadio);
        fx.Proveedor.Cuerpos.Count.Should().Be(antes, "con el aviso apagado tampoco se avisa al externo");

        // 3. Encendido: la siguiente venta avisa una vez, con el lateral y el valor; lo de antes no.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/parameters/TRN/Transporte.AvisoAlSistemaExterno/versions", new
        {
            scopeKind = "None", value = "true", validFrom = hoy.ToString("yyyy-MM-dd"), reason = "Aprobación del aviso al proveedor",
        });
        var otra = await EscenarioDeVentas.NuevaVentaAsync(http, t, sesion);
        var otraId = EscenarioDeVentas.VentaId(otra);
        otra = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"/api/inventory/pos/drafts/{otraId}/lines",
            new { productPublicId = radio, unitPublicId = unidad, quantity = 1m });
        var otraLinea = otra.GetProperty("lines").EnumerateArray().Single().GetProperty("linePublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Patch, $"/api/inventory/pos/drafts/{otraId}/lines/{otraLinea}", new { itemReference = "pla-102" });
        await EscenarioDeVentas.CobrarAsync(http, t, otraId, PrecioRadio, [v.Efectivo(PrecioRadio)]);
        await despachador.PasadasAsync(coop.TenantPublicId);

        var nuevos = fx.Proveedor.Cuerpos.Skip(antes).ToList();
        nuevos.Should().ContainSingle("sólo la venta con el aviso encendido avisa");
        using var cuerpo = JsonDocument.Parse(nuevos[0]);
        cuerpo.RootElement.GetProperty("movil").GetString().Should().Be("702");
        cuerpo.RootElement.GetProperty("valor").GetDecimal().Should().Be(PrecioRadio);
        var envios = (await InventarioE2E.GetAsync(http, t, "/api/transport/notifications")).GetProperty("items").EnumerateArray().ToList();
        envios.Should().ContainSingle(e => e.GetProperty("plate").GetString() == "PLA102").Which.GetProperty("status").GetString().Should().Be("Sent");
    }

    private static async Task VehiculoAsync(HttpClient http, string t, Guid dueno, string placa, string interno, string lateral, DateOnly hoy) =>
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/transport/vehicles", new
        {
            vehicle = new { plate = placa, internalNumber = interno, radioCode = lateral, joinedOn = hoy.AddMonths(-1).ToString("yyyy-MM-dd") },
            ownerPersonPublicId = dueno,
        });
}
