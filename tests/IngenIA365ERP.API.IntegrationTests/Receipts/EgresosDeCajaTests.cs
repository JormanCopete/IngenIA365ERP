using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, R1, T0370 (US4 «Independent Test»; FR-051 a FR-056, FR-101): por HTTP, contra Cartera real, la caja no entrega más efectivo del que tiene (antes de
/// pedir nada al módulo), Cartera decide si hay saldo a favor que devolver, los egresos varios están apagados por defecto y nada de eso deja un egreso. Escrita para
/// la corrida de cierre de R1; sin correr. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class EgresosDeCajaTests(CentralIdentityApiFixture fx)
{
    private static async Task<HttpResponseMessage> EgresarAsync(HttpClient http, CajaE2E.Caja c, object cuerpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/receipts/cash-disbursements")
        {
            Headers = { Authorization = new("Bearer", c.Token) },
            Content = System.Net.Http.Json.JsonContent.Create(cuerpo),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task La_caja_no_entrega_mas_de_lo_que_tiene_ni_lo_que_el_modulo_no_autoriza()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "egresos");

        var sinEfectivo = await EgresarAsync(http, c, new
        {
            sessionPublicId = c.Sesion, kind = "LendingCreditBalanceRefund", beneficiaryPersonPublicId = c.Persona, amount = 600_000m, productPublicId = c.CreditoA,
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinEfectivo)).Should().Be("Receipts.Disbursement.InsufficientCash");

        var sinSaldo = await EgresarAsync(http, c, new
        {
            sessionPublicId = c.Sesion, kind = "LendingCreditBalanceRefund", beneficiaryPersonPublicId = c.Persona, amount = 10_000m, productPublicId = c.CreditoA,
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinSaldo)).Should().Be("Receipts.Disbursement.ModuleRejected", "el crédito no tiene saldo a favor");

        var vario = await EgresarAsync(http, c, new
        {
            sessionPublicId = c.Sesion, kind = "MiscExpense", beneficiaryPersonPublicId = c.Persona, amount = 10_000m, miscConceptPublicId = Guid.NewGuid(),
            reason = "Papelería",
        });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(vario)).Should().Be("Receipts.Disbursement.MiscExpensesDisabled");

        var lista = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/cash-disbursements?session={c.Sesion}");
        lista.GetArrayLength().Should().Be(0, "nada de eso entregó dinero");
        var esperado = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/sessions/{c.Sesion}/expected");
        esperado.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        (await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/receipts/cash-disbursements/{Guid.NewGuid()}/document")
        {
            Headers = { Authorization = new("Bearer", c.Token) },
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
