using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T279 (quickstart §3.8; US3-1, US3-2; casos dorados 01 y 06), en el motor de <c>DB_PROVIDER</c>: sobre una bodega activa,
/// entradas de 10 × 1.000 y 10 × 1.300 con <c>unitCost</c> (<c>Inventory.Adjustments.SetUnitCost</c>) dejan el promedio en
/// 1.150 en la existencia y en el kardex, y una salida de 5 sale a 1.150; con <c>Costeo.Ambito = Bodega</c> (cooperativa aislada,
/// fijado desde el primer día del mes antes de cualquier movimiento) cada bodega lleva su promedio. La reproducción con
/// compra, venta y devolución la cubren US9, US5 y US10.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CostoPromedioTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Dos_entradas_promedian_1150_y_la_salida_sale_a_ese_costo()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "costo");
        using var http = fx.CreateClient();

        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P4", 10, 1_000m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P4", 10, 1_300m)]);

        var existencia = await esc.ExistenciaAsync(http, esc.Admin, "P4");
        existencia.GetProperty("costState").GetProperty("averageCost").GetDecimal().Should().Be(1_150m);
        existencia.GetProperty("totals").GetProperty("value").GetDecimal().Should().Be(23_000m);

        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJN", "PV1", [new("P4", 5)]);

        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        var kardex = await InventarioE2E.InformeAsync(http, esc.Admin, "kardex", $"product={esc.P("P4").Id}&warehouse={esc.Bodega("PV1")}&from={hoy}&to={hoy}");
        var salida = kardex.Filas.Single(f => kardex.Numero(f, "Salida") == 5m);
        kardex.Numero(salida, "Costo unitario").Should().Be(1_150m, "la salida sale al promedio vigente");
        kardex.Numero(salida, "Costo promedio").Should().Be(1_150m);
        kardex.Numero(kardex.Filas[^1], "Saldo (cantidad)").Should().Be(15m);
        kardex.Numero(kardex.Filas[^1], "Saldo (valor)").Should().Be(17_250m);
    }

    [Fact]
    public async Task Con_ambito_bodega_cada_bodega_lleva_su_promedio()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "costobodega");
        using var http = fx.CreateClient();
        var hoy = InventarioE2E.HoyEnColombia;
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, "/api/inventory/parameters/INV/Costeo.Ambito/versions", new
        {
            scopeKind = "None", value = "Bodega", validFrom = new DateOnly(hoy.Year, hoy.Month, 1).ToString("yyyy-MM-dd"), reason = "Costo por bodega en el ensayo",
        });

        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 10, 1_000m)]);
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P1", 10, 1_600m)]);

        (await PromedioAsync(http, esc, "P1", "PRIN")).Should().Be(1_000m);
        (await PromedioAsync(http, esc, "P1", "PV1")).Should().Be(1_600m, "con ámbito bodega los promedios no se mezclan");
    }

    private static async Task<decimal> PromedioAsync(HttpClient http, EscenarioDeInventario esc, string producto, string bodega)
    {
        var filas = await InventarioE2E.GetAsync(http, esc.Admin, $"/api/inventory/stock?productPublicId={esc.P(producto).Id}&warehousePublicId={esc.Bodega(bodega)}");
        return filas.GetProperty("items").EnumerateArray().Single().GetProperty("averageCost").GetDecimal();
    }
}
