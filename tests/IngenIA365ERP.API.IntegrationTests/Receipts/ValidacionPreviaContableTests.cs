using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0255 (quickstart recorrido E; US8-1 y US8-5; FR-143, FR-221; contabilidad.md §6): con la matriz de Recaudo completa al activar y la política
/// <c>Contabilidad.FaltaDeRegla</c> en «Bloquear» (el defecto), se quita la regla de la caja del medio con que paga el cajero —la del rol <c>Caja</c> queda sólo para
/// otro medio—: la caja no confirma (<c>Receipts.Accounting.NotPostable</c>, DT §2) y dice el hecho, el rol, la combinación (el medio y la caja) y quién la completa.
/// Pasar a «Advertir» exige su permiso (sin él, 404) y motivo; queda en la auditoría encadenada, alerta (<c>Recaudo.PoliticaContableEnAdvertir</c>) ese día y cada día
/// mientras siga así; con «Advertir» la caja confirma. Vuelta a «Bloquear», la alerta del día deja de levantarse.
/// El recorrido E de quickstart nombra el medio <b>transferencia</b>: su rol en el recibo (<c>BancoRecaudo</c>) se resuelve por el vínculo de una cuenta propia y no admite
/// regla (contabilidad.md §5), así que aquí se quita la del efectivo, que sí la tiene; la transferencia en la caja queda anotada en el informe de la corrida. (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ValidacionPreviaContableTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Sin_la_regla_del_medio_la_caja_no_confirma_y_advertir_exige_permiso_motivo_y_alerta_cada_dia()
    {
        var hoy = CarteraE2E.Hoy(fx);
        using var http = fx.CreateClient();
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "recaudo_vprevia", hoy.Year);
        await MatricesDeRecaudoYTesoreriaE2E.CargarAsync(http, coop.TokenAdmin, hoy.AddDays(-10));
        var c = await CajaE2E.PreparadaAsync(fx, http, "vprevia");
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;

        // La contadora deja la caja sólo para el efectivo del punto de la activación: la regla general del rol Caja se cierra ayer y entra una del medio EFRYT.
        var reglas = await CarteraE2E.GetAsync(http, admin, "/api/accounting/receipts/rules?role=Caja");
        var general = reglas.EnumerateArray().Single(r => r.GetProperty("dimensions").GetProperty("meansCode").ValueKind == JsonValueKind.Null);
        var cierre = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/receipts/rules/{general.GetProperty("publicId").GetGuid()}/deactivate",
            new { validTo = hoy.AddDays(-1), reason = "La caja se parametriza por medio (e2e)" });
        cierre.IsSuccessStatusCode.Should().BeTrue(await cierre.Content.ReadAsStringAsync());
        var porMedio = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/receipts/rules", new
        {
            factType = "*", role = "Caja", dimensions = new { meansCode = RecaudoYTesoreriaE2E.CodigoDelEfectivo },
            accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, MatricesDeRecaudoYTesoreriaE2E.Caja), validFrom = hoy, reason = "Caja del efectivo del punto (e2e)",
        });
        porMedio.IsSuccessStatusCode.Should().BeTrue(await porMedio.Content.ReadAsStringAsync());

        // En caja, con el efectivo de la caja del escenario (sin regla) y la política en «Bloquear»: no confirma y dice qué falta.
        var operacion = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 40_000m)], [CajaE2E.Medio(c.Efectivo, 40_000m)]);
        var bloqueada = await CajaE2E.ConfirmarAsync(http, c, operacion, Guid.NewGuid());
        bloqueada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await bloqueada.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(bloqueada)).Should().Be("Receipts.Accounting.NotPostable");
        var errores = (await RecaudoYTesoreriaE2E.LeerAsync(bloqueada)).GetProperty("data").GetProperty("errors").EnumerateArray().ToList();
        errores.Should().ContainSingle(e => e.GetProperty("role").GetString() == "Caja", "falta sólo la regla de la caja de ese medio");
        var falta = errores.Single(e => e.GetProperty("role").GetString() == "Caja");
        falta.GetProperty("factType").GetString().Should().Be("ReciboEmitido");
        falta.GetProperty("combination").GetString().Should().Contain("MeansCode=EFCJ").And.Contain("CashBoxCode=", "la combinación dice el medio y la caja");
        falta.GetProperty("whoFixes").GetString().Should().NotBeNullOrWhiteSpace();
        (await InventarioE2E.GetAsync(http, c.Token, $"/api/receipts/receipts?session={c.Sesion}")).GetArrayLength().Should().Be(0, "no se confirmó nada");

        // Pasar a «Advertir» exige su permiso (sin él la ruta no existe) y motivo.
        var (sinPermiso, _) = await ListaDeGoNoGoTests.UsuarioConPermisosAsync(fx, http, coop, "RCDSOLOV", "Receipts.Activation.View");
        var ajeno = await CarteraE2E.ConClaveAsync(http, sinPermiso, HttpMethod.Put, "/api/receipts/accounting-policy", new { policy = "Advertir", reason = "No me toca (e2e)" });
        ajeno.StatusCode.Should().Be(HttpStatusCode.NotFound, await ajeno.Content.ReadAsStringAsync());
        var sinMotivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Put, "/api/receipts/accounting-policy", new { policy = "Advertir", reason = "" });
        ((int)sinMotivo.StatusCode).Should().BeOneOf(400, 422);
        const string Motivo = "Manana de la salida: falta la regla de la caja del efectivo (e2e)";
        var advertir = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Put, "/api/receipts/accounting-policy", new { policy = "Advertir", reason = Motivo, validFrom = hoy });
        advertir.StatusCode.Should().Be(HttpStatusCode.OK, await advertir.Content.ReadAsStringAsync());

        // Queda en la auditoría encadenada con su motivo, y alerta.
        var auditado = await CausacionE2E.EnLaBaseAsync(fx, tenant, db => db.AuditOutbox.AsNoTracking()
            .Where(a => a.PayloadJson != null && a.PayloadJson.Contains("Contabilidad.FaltaDeRegla") && a.PayloadJson.Contains(Motivo)).CountAsync());
        auditado.Should().BeGreaterThan(0, "el cambio de política queda en la cadena de sellos con su motivo");
        var alertasDeHoy = await AlertasAsync(http, admin);
        alertasDeHoy.Should().NotBeEmpty("pasar a «advertir» alerta de una vez");

        // Con «Advertir» la caja confirma la misma operación.
        var confirmada = await CajaE2E.ConfirmarAsync(http, c, operacion, Guid.NewGuid());
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());

        // Cada día alerta mientras siga en «advertir».
        using (CausacionE2E.RelojEn(fx, hoy.AddDays(1)))
            await fx.CorrerTareaAsync(tenant, "recaudo.antiguedad");
        var alertasDeManana = await AlertasAsync(http, admin);
        alertasDeManana.Count.Should().BeGreaterThan(alertasDeHoy.Count, "la tarea diaria levanta la alerta del día siguiente");

        // De vuelta a «Bloquear» (también con motivo), la alerta del día deja de levantarse.
        var bloquear = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Put, "/api/receipts/accounting-policy",
            new { policy = "Bloquear", reason = "La matriz quedo completa (e2e)", validFrom = hoy.AddDays(2) });
        bloquear.StatusCode.Should().Be(HttpStatusCode.OK, await bloquear.Content.ReadAsStringAsync());
        using (CausacionE2E.RelojEn(fx, hoy.AddDays(2)))
            await fx.CorrerTareaAsync(tenant, "recaudo.antiguedad");
        (await AlertasAsync(http, admin)).Count.Should().Be(alertasDeManana.Count, "con «bloquear» ya no alerta");
    }

    private static async Task<List<JsonElement>> AlertasAsync(HttpClient http, string admin) =>
        (await InventarioE2E.GetAsync(http, admin, "/api/inventory/alerts?typeCode=Recaudo.PoliticaContableEnAdvertir&pageSize=50")).GetProperty("items")
            .EnumerateArray().ToList();
}
