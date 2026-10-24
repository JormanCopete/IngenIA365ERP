using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0108 (SC-032; FR-170): dos cajas cobran a la vez créditos de la misma persona y ninguna se interbloquea —el cerrojo del pagador serializa y
/// el orden fijo evita el ciclo—; las dos terminan con su recibo. Se corre en la fase 22. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class DosCajasMismaPersonaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Dos_cajas_sobre_la_misma_persona_sin_interbloqueos()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "doscajas");
        // La segunda caja del mismo punto, con otro cajero (otro usuario de la cooperativa).
        var caja2 = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, "/api/receipts/cash-boxes",
            new { collectionPointPublicId = c.Punto, code = "C2", name = "Caja 2", baseMode = "DailyBase", baseAmount = 500_000m, isMainCashBox = false }));
        var (token2, usuario2) = await OtroCajeroAsync(http, c);
        (await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja2.GetProperty("publicId").GetGuid()}/cashiers",
            new { userPublicId = usuario2 })).StatusCode.Should().Be(HttpStatusCode.OK);
        var sesion2 = (await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, token2, HttpMethod.Post, "/api/receipts/sessions",
            new { cashBoxPublicId = caja2.GetProperty("publicId").GetGuid(), openingBase = new[] { new { denominationPublicId = c.Billete, quantity = 10 } } })))
            .GetProperty("publicId").GetGuid();

        var op1 = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 100_000m)], [CajaE2E.Medio(c.Efectivo, 100_000m)]);
        var c2 = c with { Token = token2, Sesion = sesion2 };
        var op2 = await CajaE2E.OperacionAsync(http, c2, [CajaE2E.Cuota(c.CreditoB, 100_000m)], [CajaE2E.Medio(c.Efectivo, 100_000m)]);

        var respuestas = await Task.WhenAll(CajaE2E.ConfirmarAsync(http, c, op1, Guid.NewGuid()), CajaE2E.ConfirmarAsync(http, c2, op2, Guid.NewGuid()));

        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    /// <summary>
    /// Otro usuario de la cooperativa con persona: el jefe del escenario de Cartera. La fase 22 le da el rol de cajero de Recaudo antes de correr esta prueba
    /// (los roles de Recaudo los siembra la API al arrancar).
    /// </summary>
    private static Task<(string Token, Guid Usuario)> OtroCajeroAsync(HttpClient http, CajaE2E.Caja c)
    {
        _ = http;
        var jefe = c.Escenario.Usuarios.Jefe;
        return Task.FromResult((jefe.Token, jefe.PublicId));
    }
}
