using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US5, T0173 (US5 «Independent Test»; quickstart.md recorrido C) sobre Cartera real: anular un recibo de dos aplicaciones deja los dos reversos en Cartera,
/// el documento de anulación numerado y lo esperado de la sesión rebajado; un pago de Recaudo reversado desde Cartera deja la aplicación reversada, una persona
/// confirma «dinero recibido», nace un saldo a favor y se devuelve por orden de pago a Tesorería. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class RecaudoRecibeReversosDeCarteraTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Anular_un_recibo_reversa_sus_dos_aplicaciones_y_baja_lo_esperado()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "anulacion");
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 100_000m), CajaE2E.Cuota(c.CreditoB, 120_000m)], [CajaE2E.Medio(c.Efectivo, 220_000m)]);
        var recibo = await RecaudoYTesoreriaE2E.LeerAsync(await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid()));
        var reciboId = recibo.GetProperty("publicId").GetGuid();
        var esperadoAntes = await EfectivoEsperadoAsync(http, c);

        var pedida = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/receipts/{reciboId}/voiding",
            new { reason = "Aplicado al crédito equivocado" }));
        var anulacion = pedida.GetProperty("publicId").GetGuid();
        var aprobada = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Escenario.Usuarios.Jefe.Token, HttpMethod.Post,
            $"/api/receipts/voidings/{anulacion}/approve", new { reason = "Revisado" }));

        aprobada.GetProperty("status").GetInt32().Should().Be(2);
        aprobada.GetProperty("number").GetString().Should().NotBeNullOrEmpty();
        (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/receipts/{reciboId}")).GetProperty("status").GetInt32().Should().Be(3);
        (await EfectivoEsperadoAsync(http, c)).Should().Be(esperadoAntes - 220_000m);
        var documento = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/receipts/voidings/{anulacion}/document")
        {
            Headers = { Authorization = new("Bearer", c.Token) },
        });
        documento.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task El_reverso_de_Cartera_se_confirma_y_el_saldo_a_favor_se_devuelve_por_orden_de_pago()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "reverso");
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 90_000m)], [CajaE2E.Medio(c.Efectivo, 90_000m)]);
        var recibo = await RecaudoYTesoreriaE2E.LeerAsync(await CajaE2E.ConfirmarAsync(http, c, op, Guid.NewGuid()));
        var operacion = recibo.GetProperty("applications").EnumerateArray().Single().GetProperty("moduleVoucherPublicId").GetGuid();

        // Cartera reversa el pago desde la pantalla del crédito.
        const string motivo = "Pago mal aplicado (e2e)";
        var jefe = c.Escenario.Usuarios.Jefe.Token;
        var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{c.CreditoA}/operations/{operacion}/reversal-preview",
            new { reason = motivo });
        var reverso = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{DesembolsoE2E.Creditos}/{c.CreditoA}/operations/{operacion}/reverse",
            new { reason = motivo, expectedPreviewSha256 = previa.GetProperty("previewSha256").GetString() });
        reverso.StatusCode.Should().Be(HttpStatusCode.Created, await reverso.Content.ReadAsStringAsync());
        await CarteraE2E.DespacharAsync(fx, c.Escenario.Coop.TenantPublicId, 3);

        var pendientes = await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/module-reversals?state=3");
        var aviso = pendientes.EnumerateArray().Single();
        var confirmado = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post,
            $"/api/receipts/module-reversals/{aviso.GetProperty("publicId").GetGuid()}/confirm-cause", new { cause = "MoneyReceived", note = "Visto en el arqueo" }));
        confirmado.GetProperty("cause").GetInt32().Should().Be(1);

        var saldos = await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/payer-credit-balances?state=1");
        var saldo = saldos.EnumerateArray().Single();
        saldo.GetProperty("balance").GetDecimal().Should().Be(90_000m);
        var devuelto = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post,
            $"/api/receipts/payer-credit-balances/{saldo.GetProperty("publicId").GetGuid()}/refund-by-order", new { amount = 90_000m, form = "Transfer" }));
        devuelto.GetProperty("balance").GetDecimal().Should().Be(0m);
        var ordenes = await InventarioE2E.GetAsync(http, c.Token, "/api/treasury/payment-orders");
        ordenes.EnumerateArray().Should().Contain(o => o.GetProperty("orderKind").GetString() == "PayerCreditBalanceRefund" && o.GetProperty("netAmount").GetDecimal() == 90_000m);
    }

    private static async Task<decimal> EfectivoEsperadoAsync(HttpClient http, CajaE2E.Caja c)
    {
        var esperado = await InventarioE2E.GetAsync(http, c.Escenario.Usuarios.Jefe.Token, $"/api/receipts/sessions/{c.Sesion}/expected");
        return esperado.GetProperty("means").EnumerateArray().Where(m => m.GetProperty("paymentMeansPublicId").GetGuid() == c.Efectivo)
            .Sum(m => m.GetProperty("expected").GetDecimal());
    }
}
