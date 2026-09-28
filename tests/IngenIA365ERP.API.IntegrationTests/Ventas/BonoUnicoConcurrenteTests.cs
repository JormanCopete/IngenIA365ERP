using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T565 (feature 012, I3; US5-9, FR-097, SC-002; contracts/api.md §22.4), en el motor de <c>DB_PROVIDER</c>, en la cooperativa aislada
/// «bonounico»: el mismo número de bono —escrito distinto, pero igual una vez normalizado (mayúsculas, sin espacios ni guiones)— cobrado
/// a la vez en las cajas 1 y 2 confirma una sola venta; la otra responde <c>Payments.VoucherAlreadyUsed</c> con el documento que lo usó.
/// La nota que lo reintegra lo libera (<c>Released</c>) y el número vuelve a servir. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class BonoUnicoConcurrenteTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_mismo_bono_en_dos_cajas_a_la_vez_confirma_una_sola_venta_y_la_nota_lo_libera()
    {
        var esc = await PrepararAsync(fx, "bonounico");
        using var http = fx.CreateClient();
        var sesion1 = await SesionAsync(http, esc.Cajero1.Token, esc.Caja1);
        var sesion2 = await SesionAsync(http, esc.Cajero2.Token, esc.Caja2);

        var venta1 = await NuevaVentaAsync(http, esc.Cajero1.Token, sesion1, esc.Inv.P("P7").CodigoDeBarras);
        var venta2 = await NuevaVentaAsync(http, esc.Cajero2.Token, sesion2, esc.Inv.P("P7").CodigoDeBarras);
        object Bono(string numero) => new { paymentMeansPublicId = esc.Medio("BONOMERC"), amount = 100_000m, reference = numero };

        var carrera = await Task.WhenAll(
            PedirCobrarAsync(http, esc.Cajero1.Token, VentaId(venta1), 100_000m, [Bono("bono 2026-77")]),
            PedirCobrarAsync(http, esc.Cajero2.Token, VentaId(venta2), 100_000m, [Bono("BONO202677")]));

        carrera.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, "un bono de número único se usa una sola vez");
        var ganadora = carrera[0].StatusCode == HttpStatusCode.OK ? VentaId(venta1) : VentaId(venta2);
        var (tokenGanador, sesionGanadora) = ganadora == VentaId(venta1) ? (esc.Cajero1.Token, sesion1) : (esc.Cajero2.Token, sesion2);
        var rechazo = await InventarioE2E.FallaAsync(carrera.Single(r => r.StatusCode != HttpStatusCode.OK), "Payments.VoucherAlreadyUsed");
        var datos = rechazo.GetProperty("data");
        datos.GetProperty("documentPublicId").GetGuid().Should().Be(ganadora, "nombra la venta que lo usó");
        datos.GetProperty("number").ValueKind.Should().NotBe(JsonValueKind.Null);

        // Después, en cualquier caja y con cualquier escritura, sigue usado.
        var perdedora = ganadora == VentaId(venta1) ? (esc.Cajero2.Token, VentaId(venta2)) : (esc.Cajero1.Token, VentaId(venta1));
        await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, perdedora.Item1, perdedora.Item2, 100_000m, [Bono("Bono-2026 77")]),
            "Payments.VoucherAlreadyUsed");

        // La nota que reintegra el bono lo libera.
        var original = await InventarioE2E.GetAsync(http, tokenGanador, $"/api/inventory/sales/documents/{ganadora}");
        var linea = original.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        object Nota(object[] reintegros) => new
        {
            originDocumentPublicId = ganadora, documentTypePublicId = esc.TipoNv, reason = "El cliente devuelve la compra", totalVoid = true, withReturn = true,
            lines = new[] { new { originLinePublicId = linea } }, refunds = reintegros,
        };
        var nota = await InventarioE2E.BorradorAsync(http, tokenGanador, "/api/inventory/sales/credit-notes", Nota([]));
        var idNota = nota.GetProperty("documentPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, tokenGanador, HttpMethod.Put, $"/api/inventory/sales/credit-notes/{idNota}", Nota(
            [new { paymentMeansPublicId = esc.Medio("BONOMERC"), amount = 100_000m, reference = "BONO202677", cashSessionPublicId = sesionGanadora }]));
        var confirmada = await InventarioE2E.MandarAsync(http, tokenGanador, HttpMethod.Post, $"/api/inventory/sales/credit-notes/{idNota}/confirm",
            new { expectedAmountDue = 100_000m });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());

        var liberado = await InventarioE2E.GetAsync(http, tokenGanador, $"/api/inventory/sales/documents/{ganadora}");
        liberado.GetProperty("payments").EnumerateArray().Single(p => p.GetProperty("meansCode").GetString() == "BONOMERC")
            .GetProperty("voucherRedemptionStatus").GetInt32().Should().Be(2, "Released: la nota lo liberó");

        // El número vuelve a servir.
        var otra = await CobrarAsync(http, perdedora.Item1, perdedora.Item2, 100_000m, [Bono("bono-2026-77")]);
        otra.GetProperty("status").GetInt32().Should().Be(2);
    }
}
