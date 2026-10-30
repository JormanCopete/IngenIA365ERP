using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Feature 015, US9, T0299/T0300a (FR-147, FR-148, FR-223, FR-224; SC-001, SC-027; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): reemplaza a
/// <c>DoblesDePruebaDeCarteraTests</c>. Desde T0307 (2026-10-26) los dobles y su bandera <c>Lending:TestDoubles:Enabled</c> no existen:
/// <list type="bullet">
/// <item>la activación de Cartera encuentra a <b>Recaudo</b> (<c>REC</c>, recaudador de obligaciones y de cuentas, clase <c>Receipts</c>) y a <b>Tesorería</b>
/// (<c>TES</c>, destino <c>Treasury</c>, nunca recaudador) reales, registrados por su propia activación, y ningún doble vigente;</item>
/// <item>las rutas de los dobles no existen (404) para nadie;</item>
/// <item>Tesorería real recibe la orden de un desembolso por transferencia y su «rechazada» reversa el desembolso desde su origen;</item>
/// <item>sin la fila <c>REC</c> del recaudador, el contrato responde <c>Lending.Contract.CollectorNotRegistered</c>.</item>
/// </list>
/// Escrita y compilando, sin correr: la corre la puerta T0301.
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ParticipantesRealesDeRecaudoYTesoreriaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_activacion_de_Cartera_encuentra_a_Recaudo_y_Tesoreria_reales_y_ningun_doble()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "participantes_reales", primerEjercicioContable: CarteraE2E.Hoy(fx).Year);
        using var http = fx.CreateClient();

        // Los requisitos se leen antes de activar: activada, GET /api/lending/activation ya no los lista (013). Recaudo y Tesorería reales se activan primero, por
        // el mismo camino que ActivarCarteraAsync; la segunda vez no hacen nada. Hasta la corrida del 2026-10-26 la prueba los buscaba después de activar.
        await CarteraE2E.RecaudoYTesoreriaRealesAsync(fx, http, coop, CarteraE2E.Hoy(fx).AddDays(-1));
        var activacion = await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/lending/activation");
        foreach (var codigo in new[] { "ReceiptsCollectorRegistered", "TreasuryDestinationRegistered" })
            activacion.GetProperty("requirements").EnumerateArray().Single(r => r.GetProperty("code").GetString() == codigo)
                .GetProperty("met").GetBoolean().Should().BeTrue($"{codigo}: Recaudo y Tesorería reales (SC-001)");
        await CarteraE2E.ActivarCarteraAsync(fx, coop);

        var registro = (await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/core/integration-participants")).EnumerateArray()
            .Where(p => p.GetProperty("validTo").ValueKind == JsonValueKind.Null).ToList();
        registro.Should().Contain(p => p.GetProperty("code").GetString() == "REC" && (p.GetProperty("role").ToString() == "ObligationsCollector"
            || p.GetProperty("role").ToString() == "1"), "Recaudo es el recaudador real");
        registro.Should().Contain(p => p.GetProperty("code").GetString() == "TES" && p.GetProperty("destination").GetString() == "Treasury",
            "Tesorería es el destino real de los giros");
        registro.Should().NotContain(p => p.GetProperty("isTestDouble").GetBoolean(), "ningún doble vigente (FR-223)");

        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var estado = await CarteraE2E.GetAsync(http, coop.TokenAdmin, $"/api/{modulo}/activation/requirements");
            estado.GetProperty("status").GetString().Should().Be("Active");
            estado.GetProperty("isMinimal").GetBoolean().Should().BeFalse("la activación completa, con el cuadre provisional");
        }

        (await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/lending/activation")).GetProperty("status").GetString().Should().Be("Activated");
    }

    [Fact]
    public async Task Las_rutas_de_los_dobles_no_existen()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        foreach (var token in new[] { esc.Coop.TokenAdmin, esc.Usuarios.Jefe.Token })
        foreach (var ruta in new[] { "/api/lending/test-doubles/", "/api/lending/test-doubles/collector/reversals", "/api/lending/test-doubles/treasury/orders" })
        {
            using var resp = await CarteraE2E.MandarGetAsync(http, token, ruta);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta}: los dobles se retiraron (T0307)");
        }
    }

    [Fact]
    public async Task Tesoreria_real_recibe_la_orden_y_su_rechazo_reversa_el_desembolso()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "RealRechazo", transferencia: true);
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();

        (await OrdenDeTesoreriaAsync(fx, esc, orden)).ConMensaje.Should().BeTrue("Tesorería real recibió la orden con su OrdenDeGiroEmitida");
        var mensaje = await ResultadoDelGiroAsync(fx, http, esc, orden, "Rejected");
        (await EntregarPorElDespachadorAsync(fx, esc, mensaje)).Should().Be(IngenIA365ERP.Domain.Enums.Integration.DeliveryStatus.Processed);
        (await EstadoAsync(fx, esc, credito)).Should().Be(LoanStatus.PendingDisbursement);
        (await EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .CountAsync(m => m.Type == "DesembolsoReversado" && m.RelatedPublicId == hecho.GetProperty("operationPublicId").GetGuid()))).Should().Be(1);
    }

    [Fact]
    public async Task Sin_la_fila_REC_el_contrato_dice_que_el_recaudador_no_esta_en_el_registro()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "sin_recaudo", primerEjercicioContable: hoy.Year);
        await CarteraE2E.ActivarCarteraAsync(fx, coop);
        using var http = fx.CreateClient();
        var rec = (await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/core/integration-participants")).EnumerateArray()
            .Single(p => p.GetProperty("code").GetString() == "REC" && p.GetProperty("destination").ValueKind != JsonValueKind.String
                         && p.GetProperty("validTo").ValueKind == JsonValueKind.Null);
        var retiro = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/core/integration-participants/{rec.GetProperty("publicId").GetGuid()}/retire",
            new { validTo = hoy.AddDays(-1), reason = "Prueba del recaudador fuera del registro (e2e)" });
        retiro.IsSuccessStatusCode.Should().BeTrue(await retiro.Content.ReadAsStringAsync());
        var persona = await CarteraE2E.PersonaAsync(http, coop.TokenAdmin, "SinRegistro");

        var resp = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, $"{Recaudador}/payments", new
        {
            items = new[] { Pago(Guid.NewGuid(), persona, "FreeAmount", 10_000m, hoy) }, interactive = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        Rechazada((await CarteraE2E.LeerAsync(resp)).GetProperty("payments")[0], "Lending.Contract.CollectorNotRegistered");
    }
}
