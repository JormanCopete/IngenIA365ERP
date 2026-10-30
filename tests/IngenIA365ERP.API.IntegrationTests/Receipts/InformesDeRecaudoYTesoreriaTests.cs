using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US21, T0313 (US21 «Independent Test» y escenarios 1 y 2; SC-018; FR-152 a FR-154): los centros de informes de Recaudo y de Tesorería.
/// <list type="bullet">
/// <item>la vista en json sólo pide <c>Receipts.Reports.View</c> o <c>Treasury.Reports.View</c>; el archivo pide además el de exportar y, sin él, responde el mismo 404
/// que lo inexistente y no se audita (FR-017);</item>
/// <item>cada exportación a archivo deja su evento <c>Receipts.Report.Exported</c> o <c>Treasury.Report.Exported</c> en la auditoría de la cooperativa;</item>
/// <item>el recaudo del día por medio cuadra con el cierre de la sesión;</item>
/// <item>el alcance: quien no tiene puntos ni cuentas asignados no ve filas; con el punto o la cuenta asignados, ve sólo eso.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class InformesDeRecaudoYTesoreriaTests(CentralIdentityApiFixture fx)
{
    private const string Recaudo = "/api/reports/receipts";
    private const string Tesoreria = "/api/reports/treasury";

    [Fact]
    public async Task Ver_exige_ver_exportar_exige_exportar_cada_exportacion_queda_auditada_y_cada_quien_ve_su_alcance()
    {
        using var http = fx.CreateClient();
        var c = await CajaE2E.PreparadaAsync(fx, http, "informesryt");
        var esc = c.Escenario;
        var admin = c.Token;
        var u = esc.Usuarios;
        var desde = DateTime.UtcNow.AddMinutes(-1);

        // Un recibo de caja en efectivo por 60.000 a la cuota del crédito A.
        var op = await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 60_000m)], [CajaE2E.Medio(c.Efectivo, 60_000m)]);
        await CajaE2E.ReciboAsync(http, c, op);
        var cuenta = await RecaudoYTesoreriaE2E.CuentaDePagosAsync(http, admin);

        // El oficial sólo ve informes; la contadora los ve y los exporta. Ninguno de los dos tiene alcance todavía.
        var ver = await OriginacionE2E.RolPropioAsync(http, admin, "ENS-RYT-VER", "Receipts.Reports.View", "Treasury.Reports.View");
        var exportar = await OriginacionE2E.RolPropioAsync(http, admin, "ENS-RYT-EXP", "Receipts.Reports.View", "Receipts.Reports.Export", "Treasury.Reports.View",
            "Treasury.Reports.Export");
        await OriginacionE2E.DarRolAsync(http, admin, u.Oficial, ver);
        await OriginacionE2E.DarRolAsync(http, admin, u.Contadora, exportar);
        var oficial = u.Oficial.Token;
        var contadora = u.Contadora.Token;

        // (1) El registro de vistas y la vista en json sólo piden ver.
        (await CarteraE2E.GetAsync(http, oficial, Recaudo)).EnumerateArray().Select(v => v.GetProperty("key").GetString())
            .Should().Contain(["session-close", "daily-collection", "receipts-by-cashier", "unidentified-items"]);
        (await CarteraE2E.GetAsync(http, oficial, Tesoreria)).EnumerateArray().Select(v => v.GetProperty("key").GetString())
            .Should().Contain(["orders-by-state", "batches", "issued-checks", "bank-balances", "minimal-reconciliation"]);
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Recaudo}/receipts-by-cashier")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CarteraE2E.MandarGetAsync(http, oficial, $"{Tesoreria}/bank-balances")).StatusCode.Should().Be(HttpStatusCode.OK);

        // (2) Exportar sin el permiso de exportar: el mismo 404 que lo inexistente, y nada queda auditado.
        foreach (var url in new[] { $"{Recaudo}/receipts-by-cashier?format=xlsx", $"{Tesoreria}/bank-balances?format=pdf" })
        {
            var sinPermiso = await CarteraE2E.MandarGetAsync(http, oficial, url);
            sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound, url);
            (await CarteraE2E.CodigoDeErrorAsync(sinPermiso)).Should().Be("Generic.NotFound");
        }
        await ExportacionesAsync(esc.Coop, desde, "Receipts.Report.Exported", "receipts-by-cashier", 0, "sin el permiso no sale nada ni se audita");
        await ExportacionesAsync(esc.Coop, desde, "Treasury.Report.Exported", "bank-balances", 0, "sin el permiso no sale nada ni se audita");

        // Con él, sale el archivo y cada exportación deja su evento.
        var xlsx = await CarteraE2E.MandarGetAsync(http, contadora, $"{Recaudo}/receipts-by-cashier?format=xlsx");
        xlsx.StatusCode.Should().Be(HttpStatusCode.OK, await xlsx.Content.ReadAsStringAsync());
        xlsx.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await xlsx.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
        var pdf = await CarteraE2E.MandarGetAsync(http, contadora, $"{Tesoreria}/bank-balances?format=pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await ExportacionesAsync(esc.Coop, desde, "Receipts.Report.Exported", "receipts-by-cashier", 1, "la exportación de Recaudo queda auditada"))
            .Single().GetRawText().Should().Contain("xlsx");
        (await ExportacionesAsync(esc.Coop, desde, "Treasury.Report.Exported", "bank-balances", 1, "la exportación de Tesorería queda auditada"))
            .Single().GetRawText().Should().Contain("pdf");
        // Ver en json no se audita como exportación.
        await CarteraE2E.GetAsync(http, contadora, $"{Recaudo}/receipts-by-cashier");
        await ExportacionesAsync(esc.Coop, desde, "Receipts.Report.Exported", "receipts-by-cashier", 1, "la consulta en pantalla no es una exportación");

        // (3) El recaudo del día por medio cuadra con el cierre de la sesión (US21 «Independent Test»), para quien tiene todo el alcance.
        var porMedio = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, admin, $"{Recaudo}/daily-collection?groupBy=means"));
        var efectivo = porMedio.Filas.Should().ContainSingle(f => porMedio.Texto(f, "Medio") == "EFCJ").Subject;
        porMedio.Numero(efectivo, "Valor").Should().Be(60_000m);
        var sesiones = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, admin, $"{Recaudo}/session-close"));
        var sesion = sesiones.Filas.Should().ContainSingle(f => sesiones.Oculta(f, "_sesion") == c.Sesion.ToString()).Subject;
        sesiones.Numero(sesion, "Recaudado").Should().Be(porMedio.Numero(efectivo, "Valor"));

        // (4) El alcance (US21-1): sin puntos asignados, el oficial no ve el recaudo; con el punto, ve el de ese punto.
        new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, oficial, $"{Recaudo}/daily-collection?groupBy=point")).Filas.Should().BeEmpty("sin alcance no ve nada");
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Put, $"/api/receipts/users/{u.Oficial.PublicId}/scope",
            new { collectionPointPublicIds = new[] { c.Punto }, cashBoxPublicIds = Array.Empty<Guid>(), paymasterPersonPublicIds = Array.Empty<Guid>() }), "el punto del oficial");
        var conPunto = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, oficial, $"{Recaudo}/daily-collection?groupBy=point"));
        conPunto.Filas.Should().ContainSingle().Which.Should().Match<JsonElement>(f => conPunto.Numero(f, "Valor") == 60_000m);

        // Lo mismo en Tesorería: sin cuentas asignadas no ve saldos; con la cuenta de pagos, ve sólo esa.
        new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, oficial, $"{Tesoreria}/bank-balances")).Filas.Should().BeEmpty("sin alcance no ve cuentas");
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Put, $"/api/treasury/users/{u.Oficial.PublicId}/scope",
            new { bankAccountPublicIds = new[] { cuenta } }), "la cuenta del oficial");
        var conCuenta = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, oficial, $"{Tesoreria}/bank-balances"));
        conCuenta.Filas.Should().ContainSingle().Which.Should().Match<JsonElement>(f => conCuenta.Oculta(f, "_cuenta") == cuenta.ToString());
    }

    /// <summary>Los eventos <paramref name="accion"/> de una vista en la base de auditoría de la cooperativa, con el número esperado.</summary>
    private async Task<List<JsonElement>> ExportacionesAsync(CarteraE2E.CooperativaAislada coop, DateTime desde, string accion, string vista, int esperadas, string porque)
    {
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        using var http = fx.CreateClient();
        var hasta = DateTime.UtcNow.AddMinutes(1);
        var lote = await CarteraE2E.GetAsync(http, coop.TokenAdmin, $"/api/audit/logs?action={accion}&from={desde:O}&to={hasta:O}&pageSize=100");
        var eventos = lote.GetProperty("items").EnumerateArray().Where(e => e.GetRawText().Contains($"\\u0022{vista}\\u0022", StringComparison.Ordinal)
                                                                         || e.GetRawText().Contains($"\"{vista}\"", StringComparison.Ordinal)
                                                                         || e.GetRawText().Contains($"\\\"{vista}\\\"", StringComparison.Ordinal)).ToList();
        eventos.Should().HaveCount(esperadas, porque);
        return eventos;
    }
}
