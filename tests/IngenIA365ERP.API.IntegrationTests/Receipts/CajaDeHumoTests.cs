using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0183c (excepción declarada a «e2e una sola vez», riesgo R1 del analyze; FR-042, FR-170; SC-006, SC-007): la e2e de humo de la caja, en la colección
/// «Recaudo y Tesorería e2e», en una cooperativa aislada con la configuración mínima y la <b>activación mínima</b> de T0183b (Recaudo y Tesorería reales registrados
/// por su activación, sin nada a mano): abrir una sesión, un recibo de dos cuotas de crédito en efectivo y transferencia, la misma clave repetida devuelve el mismo
/// recibo, y cerrar la sesión contando lo esperado. La cuota de aportes entra cuando el escenario de la caja se componga con el de Captaciones (fase 22). Escrita
/// en la fase 10; el dueño pidió no correrla todavía (se corre con Docker en los dos motores antes de T0184).
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class CajaDeHumoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_sesion_un_recibo_multiproducto_la_misma_clave_y_el_cierre()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "humo");
        var estado = await InventarioE2E.GetAsync(http, c.Token, "/api/receipts/activation/requirements");
        estado.GetProperty("status").GetString().Should().Be("Active");
        estado.GetProperty("isMinimal").GetBoolean().Should().BeTrue();

        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 150_000m), CajaE2E.Cuota(c.CreditoB, 100_000m)],
            [CajaE2E.Medio(c.Efectivo, 200_000m), CajaE2E.Medio(c.Transferencia, 50_000m, "TR-HUMO")]);
        var clave = Guid.NewGuid();
        var primero = await CajaE2E.ConfirmarAsync(http, c, op, clave);
        primero.StatusCode.Should().Be(HttpStatusCode.OK, await primero.Content.ReadAsStringAsync());
        var numero = (await RecaudoYTesoreriaE2E.LeerAsync(primero)).GetProperty("number").GetString();
        var repetido = await CajaE2E.ConfirmarAsync(http, c, op, clave);
        (await RecaudoYTesoreriaE2E.LeerAsync(repetido)).GetProperty("number").GetString().Should().Be(numero);
        (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/receipts?session={c.Sesion}")).GetArrayLength().Should().Be(1);

        // Cerrar contando lo esperado: base 500.000 + 200.000 en efectivo (4 billetes de 50.000 más los 10 de la base) y la transferencia por referencia.
        var conteo = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/sessions/{c.Sesion}/count",
            new
            {
                means = new object[]
                {
                    new { paymentMeansPublicId = c.Efectivo, denominations = new[] { new { denominationPublicId = c.Billete, quantity = 14 } } },
                    new { paymentMeansPublicId = c.Transferencia, counted = 50_000m, references = new[] { "TR-HUMO" } },
                },
            }));
        conteo.GetProperty("sessionStatus").GetString().Should().Be("Closed");
    }
}
