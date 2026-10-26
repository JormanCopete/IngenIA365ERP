using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0108 (SC-007; FR-172): tres repeticiones de la confirmación con la misma <c>Idempotency-Key</c> devuelven el mismo recibo y no cobran dos veces;
/// la misma clave con otra operación responde <c>409 Operation.KeyReused</c>. Se corre en la fase 22. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ReintentoConLaMismaClaveTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Tres_repeticiones_devuelven_el_mismo_recibo()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "reintento");
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m)], [CajaE2E.Medio(c.Efectivo, 50_000m)]);
        var clave = Guid.NewGuid();

        var numeros = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            var resp = await CajaE2E.ConfirmarAsync(http, c, op, clave);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
            numeros.Add((await RecaudoYTesoreriaE2E.LeerAsync(resp)).GetProperty("number").GetString());
        }
        numeros.Distinct().Should().HaveCount(1);
        (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/receipts?session={c.Sesion}")).GetArrayLength().Should().Be(1);

        var otra = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoB, 100_000m)], [CajaE2E.Medio(c.Efectivo, 100_000m)]);
        var reutilizada = await CajaE2E.ConfirmarAsync(http, c, otra, clave);
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(reutilizada)).Should().Be("Operation.KeyReused");
    }
}
