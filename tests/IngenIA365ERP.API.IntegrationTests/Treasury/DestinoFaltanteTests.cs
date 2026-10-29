using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, D-a (decisión del dueño del 2026-10-27; FR-229): una orden de transferencia que llega a Tesorería sin cuenta destino (<c>DestinationMissing</c>)
/// la resuelve Tesorería misma. Sobre Recaudo real: un saldo a favor del pagador se devuelve por dos órdenes de transferencia sin cuenta; a la primera Tesorería le
/// completa la cuenta con la registrada y aprobada del beneficiario y queda lista; la segunda la pasa a cheque y entra a un lote de forma «cheques». Sin el permiso
/// <c>Treasury.Orders.ResolveDestination</c> la ruta responde 404 (FR-017); con una cuenta de otra persona, no se completa.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class DestinoFaltanteTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Tesoreria_completa_la_cuenta_destino_o_gira_por_cheque_la_orden_que_llego_sin_ella()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "destino");
        var (sinCuenta, porCheque) = await DosDevolucionesSinCuentaAsync(http, c);
        foreach (var o in new[] { sinCuenta, porCheque })
        {
            o.GetProperty("status").GetInt32().Should().Be(2, "llega pendiente");
            o.GetProperty("pendingReasons").EnumerateArray().Select(m => m.GetString()).Should().Contain("Treasury.Order.DestinationMissing");
        }

        // El jefe (aprobador de pagos: Treasury.Orders.View, sin ResolveDestination) no puede resolver: 404 como la inexistente.
        var cuenta = await TesoreriaE2E.CuentaDestinoAprobadaAsync(http, c.Escenario, c.Persona, "0123456789");
        var sinPermiso = await CarteraE2E.ConClaveAsync(http, c.Escenario.Usuarios.Jefe.Token, HttpMethod.Post,
            $"/api/treasury/payment-orders/{Id(sinCuenta)}/destination", new { personBankAccountPublicId = cuenta, reason = "Sin permiso (e2e)" });
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Una cuenta de otra persona no completa el destino.
        var (otra, _) = await DesembolsoE2E.DeudorAsync(http, c.Token, "OtraDestino");
        var ajena = await TesoreriaE2E.CuentaDestinoAprobadaAsync(http, c.Escenario, otra, "0222333444");
        var rechazo = await CarteraE2E.ConClaveAsync(http, c.Token, HttpMethod.Post, $"/api/treasury/payment-orders/{Id(sinCuenta)}/destination",
            new { personBankAccountPublicId = ajena, reason = "Cuenta de otra persona (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(rechazo)).Should().Be("Treasury.Order.DestinationAccountInvalid");

        // Completar con la del beneficiario: la orden queda lista para un lote por archivo.
        var completada = await CarteraE2E.ConClaveAsync(http, c.Token, HttpMethod.Post, $"/api/treasury/payment-orders/{Id(sinCuenta)}/destination",
            new { personBankAccountPublicId = cuenta, reason = "El pagador entregó su certificación (e2e)" });
        completada.StatusCode.Should().Be(HttpStatusCode.OK, await completada.Content.ReadAsStringAsync());
        var lista = await RecaudoYTesoreriaE2E.LeerAsync(completada);
        lista.GetProperty("status").GetInt32().Should().Be(3);
        lista.GetProperty("form").GetInt32().Should().Be(1);
        lista.GetProperty("pendingReasons").GetArrayLength().Should().Be(0);
        lista.GetProperty("destinationAccountNumberMasked").GetString().Should().EndWith("6789");

        // Girarla por cheque: queda lista, sin cuenta destino, y entra a un lote de forma «cheques».
        var pasada = await CarteraE2E.ConClaveAsync(http, c.Token, HttpMethod.Post, $"/api/treasury/payment-orders/{Id(porCheque)}/pay-by-check",
            new { reason = "El pagador pidió cheque (e2e)" });
        pasada.StatusCode.Should().Be(HttpStatusCode.OK, await pasada.Content.ReadAsStringAsync());
        var cheque = await RecaudoYTesoreriaE2E.LeerAsync(pasada);
        cheque.GetProperty("status").GetInt32().Should().Be(3);
        cheque.GetProperty("form").GetInt32().Should().Be(2);
        var cuentaDePagos = await RecaudoYTesoreriaE2E.CuentaDePagosAsync(http, c.Token);
        var lote = await TesoreriaE2E.LoteAsync(http, c.Token, cuentaDePagos, [Id(porCheque)], forma: 3);
        lote.GetProperty("orderCount").GetInt32().Should().Be(1);

        // Ya resuelta, no se vuelve a resolver.
        var otraVez = await CarteraE2E.ConClaveAsync(http, c.Token, HttpMethod.Post, $"/api/treasury/payment-orders/{Id(sinCuenta)}/pay-by-check",
            new { reason = "Otra vez (e2e)" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Treasury.Order.DestinationNotMissing");
    }

    private static Guid Id(JsonElement orden) => orden.GetProperty("publicId").GetGuid();

    /// <summary>Un pago de Recaudo reversado desde Cartera deja un saldo a favor del pagador; se devuelve en dos órdenes de transferencia sin cuenta destino.</summary>
    private async Task<(JsonElement Primera, JsonElement Segunda)> DosDevolucionesSinCuentaAsync(HttpClient http, CajaE2E.Caja c)
    {
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 60_000m)], [CajaE2E.Medio(c.Efectivo, 60_000m)]);
        var recibo = await CajaE2E.ReciboAsync(http, c, op);
        var operacion = recibo.GetProperty("applications").EnumerateArray().Single().GetProperty("moduleVoucherPublicId").GetGuid();
        const string motivo = "Pago mal aplicado (e2e)";
        var jefe = c.Escenario.Usuarios.Jefe.Token;
        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{c.CreditoA}/operations/{operacion}/reversal-preview",
            new { reason = motivo });
        var reverso = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{c.CreditoA}/operations/{operacion}/reverse",
            new { reason = motivo, expectedPreviewSha256 = previa.GetProperty("previewSha256").GetString() });
        reverso.StatusCode.Should().Be(HttpStatusCode.Created, await reverso.Content.ReadAsStringAsync());
        await CarteraE2E.DespacharAsync(fx, c.Escenario.Coop.TenantPublicId, 3);

        var aviso = (await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/module-reversals?state=3")).EnumerateArray().Single();
        await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post,
            $"/api/receipts/module-reversals/{aviso.GetProperty("publicId").GetGuid()}/confirm-cause", new { cause = "MoneyReceived", note = "Visto en el arqueo" }));
        var saldo = (await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/payer-credit-balances?state=1")).EnumerateArray().Single().GetProperty("publicId").GetGuid();
        foreach (var valor in new[] { 25_000m, 35_000m })
            await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post,
                $"/api/receipts/payer-credit-balances/{saldo}/refund-by-order", new { amount = valor, form = "Transfer" }));
        var ordenes = (await InventarioE2E.GetAsync(http, c.Token, "/api/treasury/payment-orders")).EnumerateArray()
            .Where(o => o.GetProperty("orderKind").GetString() == "PayerCreditBalanceRefund").ToList();
        return (ordenes.Single(o => o.GetProperty("netAmount").GetDecimal() == 25_000m), ordenes.Single(o => o.GetProperty("netAmount").GetDecimal() == 35_000m));
    }
}
