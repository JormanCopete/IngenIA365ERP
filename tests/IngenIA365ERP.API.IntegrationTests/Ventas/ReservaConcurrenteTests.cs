using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T871 (feature 012, I6, US14; FR-004, T15), en el motor de <c>DB_PROVIDER</c>, en la cooperativa aislada «reservai6» (la de ventas de I3):
/// la reserva toma la fila exclusiva de <c>INV_StockBalances</c> en el orden canónico del cerrojo. Con 5 disponibles, dos pedidos
/// simultáneos de 3: uno confirma y el otro recibe <c>Inventory.Stock.Insufficient</c> con el disponible real; un pedido y una venta POS
/// simultáneos del último disponible nunca dejan el disponible negativo. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ReservaConcurrenteTests(CentralIdentityApiFixture fx)
{
    private const string Pedidos = "/api/inventory/sales/orders";

    [Fact]
    public async Task Dos_pedidos_por_las_mismas_unidades_y_un_pedido_contra_el_POS_nunca_dejan_el_disponible_negativo()
    {
        var esc = await PrepararAsync(fx, "reservai6");
        using var http = fx.CreateClient();
        var inv = esc.Inv;
        var sup = esc.Supervisor.Token;

        // P6 queda con 5 en PV1.
        await inv.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PV1", [new("P6", 495)]);
        (await DisponibleAsync(http, inv)).Should().Be((5m, 0m, 5m));

        // (1) Dos pedidos de 3 confirmados a la vez: uno reserva y el otro ve el disponible real.
        var a = await PedidoAsync(http, inv, sup, 3m);
        var b = await PedidoAsync(http, inv, sup, 3m);
        var respuestas = await Task.WhenAll(
            InventarioE2E.PedirConfirmarAsync(http, sup, Pedidos, a),
            InventarioE2E.PedirConfirmarAsync(http, sup, Pedidos, b));
        respuestas.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, string.Join(" | ", await Task.WhenAll(respuestas.Select(r => r.Content.ReadAsStringAsync()))));
        var perdedora = respuestas.Single(r => r.StatusCode != HttpStatusCode.OK);
        var error = await InventarioE2E.FallaAsync(perdedora, "Inventory.Stock.Insufficient");
        error.ToString().Should().Contain("\"available\"");
        Disponibles(error).Should().Contain(2m, "el disponible real después de la reserva ganadora");
        (await DisponibleAsync(http, inv)).Should().Be((5m, 3m, 2m));

        // (2) Un pedido de 2 y una venta POS de 2 por las 2 que quedan, a la vez: a lo sumo una pasa y el disponible no baja de cero.
        var pedido = await PedidoAsync(http, inv, sup, 2m);
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);
        var venta = await NuevaVentaAsync(http, cajero, sesion, inv.P("P6").CodigoDeBarras);
        venta = await LeerCodigoAsync(http, cajero, VentaId(venta), inv.P("P6").CodigoDeBarras);
        var carrera = await Task.WhenAll(
            InventarioE2E.PedirConfirmarAsync(http, sup, Pedidos, pedido),
            PedirCobrarAsync(http, cajero, VentaId(venta), AmountDue(venta), [esc.Efectivo(AmountDue(venta))]));
        var textos = await Task.WhenAll(carrera.Select(r => r.Content.ReadAsStringAsync()));
        carrera.Count(r => r.StatusCode == HttpStatusCode.OK).Should().BeLessThanOrEqualTo(1, string.Join(" | ", textos));
        carrera.Should().Contain(r => r.StatusCode == HttpStatusCode.UnprocessableEntity, string.Join(" | ", textos));
        textos.Where((_, i) => carrera[i].StatusCode != HttpStatusCode.OK).Should()
            .OnlyContain(t => t.Contains("Inventory.Stock.Insufficient"), "quien pierde la carrera lo sabe por la existencia");
        var (fisica, reservada, disponible) = await DisponibleAsync(http, inv);
        disponible.Should().BeGreaterThanOrEqualTo(0m, $"física {fisica}, reservada {reservada}");
        (fisica - reservada).Should().Be(disponible);
    }

    private static async Task<Guid> PedidoAsync(HttpClient http, EscenarioDeInventario inv, string token, decimal cantidad)
    {
        var pedido = await InventarioE2E.BorradorAsync(http, token, Pedidos, new
        {
            documentTypePublicId = inv.Tipos["PED"], warehousePublicId = inv.Bodega("PV1"), payments = Array.Empty<object>(),
            lines = new[] { new { productPublicId = inv.P("P6").Id, unitPublicId = inv.P("P6").Unidad, quantity = cantidad } },
        });
        return ElectronicInvoicing.EscenarioDeFacturacionElectronica.Id(pedido);
    }

    private static async Task<(decimal Fisica, decimal Reservada, decimal Disponible)> DisponibleAsync(HttpClient http, EscenarioDeInventario inv)
    {
        var e = await inv.ExistenciaAsync(http, inv.Admin, "P6");
        var fila = e.GetProperty("byWarehouse").EnumerateArray().Single(w => w.GetProperty("warehouse").GetProperty("publicId").GetGuid() == inv.Bodega("PV1"));
        return (fila.GetProperty("physical").GetDecimal(), fila.GetProperty("reserved").GetDecimal(), fila.GetProperty("available").GetDecimal());
    }

    /// <summary>Todos los números <c>available</c> del sobre (el dato puede venir en la raíz de <c>data</c> o por línea).</summary>
    private static IEnumerable<decimal> Disponibles(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.NameEquals("available") && p.Value.ValueKind == JsonValueKind.Number) yield return p.Value.GetDecimal();
                    foreach (var d in Disponibles(p.Value)) yield return d;
                }
                break;
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray())
                    foreach (var d in Disponibles(x)) yield return d;
                break;
        }
    }
}
