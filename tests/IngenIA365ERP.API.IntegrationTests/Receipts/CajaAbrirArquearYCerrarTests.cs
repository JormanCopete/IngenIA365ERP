using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0087 (US2 «Independent Test»; quickstart recorrido A pasos 1, 7–9; SC-004, SC-005): abrir con 500.000 de base, retirar a la bóveda con doble
/// custodia, cerrar contando con un faltante sobre la tolerancia —queda «cerrada con diferencia por aprobar»—, que otra persona la apruebe con motivo, y que el
/// cierre del día sólo proceda con todas las sesiones cerradas y deje su informe. Los recibos (1.200.000 en efectivo y 800.000 por transferencia) llegan con la
/// fase 6 y se suman a este recorrido allí. Necesita Recaudo activado (fase 19): se escribe ahora y se corre en la fase 22. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class CajaAbrirArquearYCerrarTests(CentralIdentityApiFixture fx)
{
    private static async Task<Guid> DenominacionAsync(HttpClient http, string token, decimal valor)
    {
        var lista = await InventarioE2E.GetAsync(http, token, "/api/core/cash-denominations");
        var ya = lista.EnumerateArray().FirstOrDefault(d => d.GetProperty("value").GetDecimal() == valor);
        if (ya.ValueKind == JsonValueKind.Object) return ya.GetProperty("cashDenominationPublicId").GetGuid();
        var r = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, "/api/core/cash-denominations",
            new { currency = "COP", kind = "Bill", value = valor, displayOrder = 1, validFrom = "2020-01-01" });
        return (await RecaudoYTesoreriaE2E.LeerAsync(r)).GetProperty("cashDenominationPublicId").GetGuid();
    }

    [Fact]
    public async Task Abre_retira_a_la_boveda_cierra_con_diferencia_que_otra_persona_aprueba_y_cierra_el_dia()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "caja");
        using var http = fx.CreateClient();
        var t = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var efectivo = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "EFCAJA", "Cash", hoy.AddDays(-1));
        var transferencia = await ConfiguracionDeRecaudoE2E.MedioAsync(http, t, "TRCAJA", "Transfer", hoy.AddDays(-1));
        var (punto, caja) = await ConfiguracionDeRecaudoE2E.PuntoConCajaAsync(http, t, coop.SucursalPrincipal, "CAJAE2E", efectivo, transferencia);
        var b50 = await DenominacionAsync(http, t, 50_000m);
        var b1 = await DenominacionAsync(http, t, 1_000m);

        var cajera = await CarteraE2E.UsuarioPorCorreoAsync(http, t, coop.CorreoAdmin!);
        await CarteraE2E.AsociarPersonaAsync(http, t, cajera, coop.CorreoAdmin!, await CarteraE2E.PersonaAsync(http, t, "Carla", "Cajera"));
        (await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/cash-boxes/{caja}/cashiers", new { userPublicId = cajera }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var boveda = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/vaults",
            new { branchPublicId = coop.SucursalPrincipal, mode = "Vault" }));
        var bovedaId = boveda.GetProperty("publicId").GetGuid();

        await RecaudoYTesoreriaE2E.RegistrarYActivarAsync(coop);

        // 1. Abrir con 500.000 contados por denominaciones.
        var sesion = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/receipts/sessions",
            new { cashBoxPublicId = caja, openingBase = new[] { new { denominationPublicId = b50, quantity = 10 } } }));
        var sesionId = sesion.GetProperty("publicId").GetGuid();
        sesion.GetProperty("openingBase").GetDecimal().Should().Be(500_000m);
        (await InventarioE2E.GetAsync(http, t, $"/api/receipts/sessions/{sesionId}/expected")).GetProperty("hidden").GetBoolean().Should().BeTrue();

        // 7. Retirar a la bóveda sin segundo custodio: la doble custodia lo rechaza.
        var retiro = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/sessions/{sesionId}/movements",
            new { kind = "WithdrawToVault", denominations = new[] { new { denominationPublicId = b50, quantity = 2 } }, reason = "Traslado" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(retiro)).Should().Be("Receipts.Movement.DoubleCustodyRequired");

        // 8. Cerrar contando 499.000: un faltante sobre la tolerancia queda por aprobar.
        var conteo = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/sessions/{sesionId}/count",
            new
            {
                means = new object[]
                {
                    new { paymentMeansPublicId = efectivo, denominations = new[] { new { denominationPublicId = b50, quantity = 9 }, new { denominationPublicId = b1, quantity = 49 } }, reason = "No cuadra" },
                },
            }));
        conteo.GetProperty("sessionStatus").GetString().Should().Be("ClosedWithDifferencePending");
        var linea = conteo.GetProperty("lines").EnumerateArray().First(l => l.GetProperty("pendingApproval").GetBoolean()).GetProperty("publicId").GetGuid();

        // La misma persona no aprueba su diferencia.
        var propia = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/sessions/{sesionId}/differences/{linea}/approve", new { reason = "Yo" });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(propia)).Should().Be("Receipts.Count.SelfApproval");

        // El cierre del día no procede con la diferencia por aprobar.
        var dia = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/receipts/collection-points/{punto}/days/{hoy:yyyy-MM-dd}/close", new { });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(dia)).Should().Be("Receipts.Day.DifferencesPending");

        // 9. El informe del cierre de la sesión sale en PDF.
        var informe = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/receipts/sessions/{sesionId}/close-report")
        {
            Headers = { Authorization = new("Bearer", t) },
        });
        informe.StatusCode.Should().Be(HttpStatusCode.OK);
        informe.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        _ = bovedaId;
    }
}
