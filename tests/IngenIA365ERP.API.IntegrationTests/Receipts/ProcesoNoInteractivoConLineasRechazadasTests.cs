using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US6, T0189 (SC-030; FR-171): en una corrida no interactiva una línea que el contrato rechaza no tumba las demás; queda rechazada con su motivo y se
/// reprocesa con su misma clave, sin duplicar el recibo. Escrita en la fase 11; se corre en la fase 22 en los dos motores.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ProcesoNoInteractivoConLineasRechazadasTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_rechazada_no_tumba_las_demas_y_se_reprocesa_con_su_clave()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "corrida");
        var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, c.Token, c.Escenario, "RECCOR", 1, c.Escenario.CuentaDeOrigen);
        var hoy = c.Escenario.Hoy;
        await ExtractoE2E.CargarAsync(http, c.Token, cuenta, string.Join("\r\n", "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            $"{hoy:yyyy-MM-dd};40000.00;A;;;Abono;", $"{hoy:yyyy-MM-dd};50000.00;B;;;Abono;", $"{hoy:yyyy-MM-dd};60000.00;C;;;Abono;"));
        var abonos = (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/bank-items?account={cuenta}")).GetProperty("items").EnumerateArray().ToList();

        Guid? corrida = null;
        foreach (var a in abonos)
        {
            var valor = a.GetProperty("amount").GetDecimal();
            // La línea de 50.000 va a una obligación que no existe: el contrato la rechaza.
            var producto = valor == 50_000m ? Guid.NewGuid() : c.CreditoA;
            var r = await ExtractoE2E.IdentificarAsync(http, c.Token, a.GetProperty("publicId").GetGuid(),
                [new { target = "Loan", beneficiaryPersonPublicId = c.Persona, amount = valor, productPublicId = producto }], corrida);
            corrida = r.GetProperty("run").GetProperty("publicId").GetGuid();
        }

        var run = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/processing-runs/{corrida}");
        run.GetProperty("applied").GetInt32().Should().Be(2);
        run.GetProperty("rejected").GetInt32().Should().Be(1);
        var rechazada = run.GetProperty("items").EnumerateArray().Single(l => l.GetProperty("outcome").GetInt32() == 3);

        var reproceso = await InventarioE2E.ExitoAsync(http, c.Token, HttpMethod.Post,
            $"/api/receipts/processing-runs/{corrida}/lines/{rechazada.GetProperty("publicId").GetGuid()}/retry");
        reproceso.GetProperty("rejected").GetInt32().Should().Be(1, "sigue rechazada: la obligación no existe, y repetir no duplica nada");
        reproceso.GetProperty("applied").GetInt32().Should().Be(2);
    }
}
