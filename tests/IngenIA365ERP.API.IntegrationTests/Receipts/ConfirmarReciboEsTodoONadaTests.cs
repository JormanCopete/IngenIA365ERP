using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0108 (SC-006): un rechazo en la última aplicación no deja nada —ni recibo, ni operaciones en Cartera, ni número consumido—: el siguiente recibo
/// que sí se confirma lleva el primer número. El rechazo se induce con un crédito que no existe en la tercera aplicación. Se corre en la fase 22. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ConfirmarReciboEsTodoONadaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_rechazo_en_la_tercera_aplicacion_no_deja_nada_ni_consume_numero()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "todoonada");
        var op = (await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = c.Persona }))).GetProperty("publicId").GetGuid();
        var items = await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{op}/items",
            new { items = new[] { CajaE2E.Cuota(c.CreditoA, 100_000m), CajaE2E.Cuota(c.CreditoB, 100_000m), CajaE2E.Cuota(Guid.NewGuid(), 100_000m) } });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(items)).Should().Be("Receipts.Receipt.ItemRejected");

        var recibos = await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/receipts?session={c.Sesion}");
        recibos.GetArrayLength().Should().Be(0);

        var buena = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m)], [CajaE2E.Medio(c.Efectivo, 50_000m)]);
        var recibo = await RecaudoYTesoreriaE2E.LeerAsync(await CajaE2E.ConfirmarAsync(http, c, buena, Guid.NewGuid()));
        recibo.GetProperty("number").GetString().Should().EndWith("000001", "un rechazo no consumió número");
    }
}
