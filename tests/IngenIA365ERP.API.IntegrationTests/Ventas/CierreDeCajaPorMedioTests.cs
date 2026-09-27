using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T566 (feature 012, I3; US5-4, US5-10, US5-11, FR-099, FR-100; quickstart §5.8 a §5.10), en el motor de <c>DB_PROVIDER</c>, en la
/// cooperativa aislada «cierrecaja»: el retiro a caja fuerte con la aprobación de su política, la reclasificación Visa → Mastercard
/// sin tocar la venta, el cierre por medio de pago (efectivo 20.000 menos por total, tarjetas por el lote de cada datáfono,
/// transferencias por referencias), el documento de diferencia que el cajero no aprueba y el supervisor sí —con su
/// «DiferenciaDeArqueoAprobada»—, el informe del arqueo en PDF y el cierre del día de PV1 con el detalle por adquirente y datáfono.
/// El lote <c>CashSessionClose</c> del disparador <c>CierreDeTurno</c> exige la contabilidad iniciada y lo prueba
/// <c>ContabilizacionDeVentasPorMensajesTests</c>. SC-025 (300 ventas y 6 medios en menos de 5 minutos) sólo con
/// <c>RUN_PERF_TESTS=1</c>. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CierreDeCajaPorMedioTests(CentralIdentityApiFixture fx)
{
    private const string Movimientos = "/api/inventory/cash-movements";

    [Fact]
    public async Task Movimientos_cierre_por_medio_con_diferencia_aprobada_y_cierre_del_dia()
    {
        var esc = await PrepararAsync(fx, "cierrecaja");
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var desde = esc.Inv.Corte.AddDays(1);
        var tipos = (await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/document-types")).EnumerateArray()
            .ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("publicId").GetGuid());
        await InventarioE2E.PoliticaAsync(http, esc.Admin, tipos["MC"], desde, (0m, "Inventory.CashMovements.Approve"));
        await InventarioE2E.PoliticaAsync(http, esc.Admin, tipos["DA"], desde, (0m, "Inventory.CashDifferences.Approve"));
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        // Ventas del turno: una en efectivo (5 × P7), una con Visa y una por transferencia.
        var efectivo = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        efectivo = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch,
            $"{Borradores}/{VentaId(efectivo)}/lines/{efectivo.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}", new { quantity = 5m });
        await CobrarAsync(http, cajero, VentaId(efectivo), 500_000m, [esc.Efectivo(500_000m)]);
        var conVisa = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        await CobrarAsync(http, cajero, VentaId(conVisa), 100_000m, [new
        {
            paymentMeansPublicId = esc.Medio("VISARB"), amount = 100_000m, authorizationCode = "778899", last4 = "1881", cardTerminalPublicId = esc.DatafonoRedeban,
        }]);
        var conTransferencia = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        await CobrarAsync(http, cajero, VentaId(conTransferencia), 100_000m,
            [new { paymentMeansPublicId = esc.Medio("TRANSFBCO"), amount = 100_000m, reference = "TRF-000123" }]);

        // (1) Retiro parcial a caja fuerte: la política lo manda a aprobación y lo aprueba el supervisor (FR-100).
        var retiro = await MovimientoAsync(http, cajero, new
        {
            cashSessionPublicId = sesion, kind = "WithdrawalToSafe", sourcePaymentMeansPublicId = esc.Medio("EFECTIVO"), destination = "Safe",
            amount = 300_000m, reason = "Retiro parcial a caja fuerte",
        });
        await AprobarAsync(http, esc, cajero, retiro);
        var tiposDelRetiro = (await Accounting.ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, retiro)).Select(m => m.GetProperty("type").GetString());
        tiposDelRetiro.Should().Contain("MovimientoDeCajaRegistrado");
        var comprobante = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Get, $"{Movimientos}/{retiro}/receipt");
        comprobante.StatusCode.Should().Be(HttpStatusCode.OK);
        comprobante.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        // (2) La Visa era una Mastercard: se reclasifica sin tocar la venta confirmada.
        var pagoVisa = (await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{VentaId(conVisa)}"))
            .GetProperty("payments")[0].GetProperty("documentPaymentPublicId").GetGuid();
        var reclasificacion = await MovimientoAsync(http, cajero, new
        {
            cashSessionPublicId = sesion, kind = "ReclassificationBetweenMeans", sourcePaymentMeansPublicId = esc.Medio("VISARB"),
            targetPaymentMeansPublicId = esc.Medio("MCCB"), amount = 100_000m, reason = "Se registró como Visa una Mastercard",
            reclassifiedPaymentPublicId = pagoVisa,
        });
        await AprobarAsync(http, esc, cajero, reclasificacion);
        (await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{VentaId(conVisa)}"))
            .GetProperty("payments")[0].GetProperty("meansCode").GetString().Should().Be("VISARB", "la venta no se toca");

        // (3) Lo esperado por medio: base + ventas ± movimientos y reclasificaciones.
        var esperado = await InventarioE2E.GetAsync(http, cajero, $"{Sesiones}/{sesion}/expected");
        decimal Esperado(string codigo) => esperado.GetProperty("lines").EnumerateArray()
            .Single(l => l.GetProperty("paymentMeans").GetProperty("code").GetString() == codigo).GetProperty("expected").GetDecimal();
        Esperado("EFECTIVO").Should().Be(200_000m + 500_000m - 300_000m);
        Esperado("VISARB").Should().Be(0m);
        Esperado("MCCB").Should().Be(100_000m);
        Esperado("TRANSFBCO").Should().Be(100_000m);

        // (4) Cierre con el efectivo 20.000 menos: inmediato, con el documento de diferencia en aprobación.
        var cierre = await esc.CerrarAsync(http, cajero, sesion, new Dictionary<string, decimal> { ["EFECTIVO"] = -20_000m });
        cierre.StatusCode.Should().Be(HttpStatusCode.OK, await cierre.Content.ReadAsStringAsync());
        var cerrada = await InventarioE2E.LeerAsync(cierre);
        cerrada.GetProperty("status").GetInt32().Should().Be(2, "Closed");
        var lineaEfectivo = cerrada.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("paymentMeansCode").GetString() == "EFECTIVO");
        lineaEfectivo.GetProperty("difference").GetDecimal().Should().Be(-20_000m);
        lineaEfectivo.GetProperty("treatment").GetInt32().Should().Be(3, "ShortageToExpense: Caja.TratamientoFaltante = Gasto por defecto");
        cerrada.GetProperty("lines").EnumerateArray().Where(l => l.GetProperty("paymentMeansCode").GetString() != "EFECTIVO")
            .Should().OnlyContain(l => l.GetProperty("difference").GetDecimal() == 0m, "tarjetas por lote y transferencias por referencias cuadran");
        var diferencia = cerrada.GetProperty("differenceDocument");
        diferencia.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval: supera la tolerancia del medio");
        var solicitud = diferencia.GetProperty("approvalRequestPublicId").GetGuid();

        // El cajero no la aprueba; el supervisor sí, y sale «DiferenciaDeArqueoAprobada».
        var delCajero = await InventarioE2E.DecidirAsync(http, esc.Admin, cajero, solicitud, aprobar: true);
        delCajero.IsSuccessStatusCode.Should().BeFalse("quien arqueó no aprueba su diferencia");
        if (delCajero.StatusCode != HttpStatusCode.NotFound) await InventarioE2E.FallaAsync(delCajero, "Approvals.SelfApprovalForbidden");
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, solicitud, aprobar: true));
        var idDiferencia = diferencia.GetProperty("documentPublicId").GetGuid();
        (await Accounting.ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, idDiferencia)).Select(m => m.GetProperty("type").GetString())
            .Should().Contain("DiferenciaDeArqueoAprobada");

        // (5) El informe del arqueo, con firmas, en PDF.
        var informe = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Get, $"{Sesiones}/{sesion}/count-report");
        informe.StatusCode.Should().Be(HttpStatusCode.OK);
        informe.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await informe.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());

        // (6) El cierre del día de PV1: todas las sesiones del día cerradas, consolidado por medio y con las tarjetas por adquirente y datáfono.
        var hoy = InventarioE2E.HoyEnColombia;
        var dia = await InventarioE2E.MandarAsync(http, esc.Supervisor.Token, HttpMethod.Post, "/api/inventory/day-closes",
            new { pointOfSalePublicId = esc.Punto, operatingDate = hoy.ToString("yyyy-MM-dd") });
        dia.StatusCode.Should().Be(HttpStatusCode.Created, await dia.Content.ReadAsStringAsync());
        var cierreDelDia = (await InventarioE2E.LeerAsync(dia)).GetProperty("dayClosePublicId").GetGuid();
        var detalle = await InventarioE2E.GetAsync(http, esc.Supervisor.Token, $"/api/inventory/day-closes/{cierreDelDia}");
        detalle.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("paymentMeansCode").GetString())
            .Should().Contain(["EFECTIVO", "MCCB", "TRANSFBCO"]);
        detalle.GetProperty("cards").EnumerateArray().Should().Contain(c => c.GetProperty("acquirerCode").GetString() != null
            && c.GetProperty("cardTerminalCode").GetString() != null, "las tarjetas se detallan por adquirente y datáfono");
        detalle.GetProperty("sessions").EnumerateArray().Select(s => s.GetProperty("cashSessionPublicId").GetGuid()).Should().Contain(sesion);
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, esc.Supervisor.Token, HttpMethod.Post, "/api/inventory/day-closes",
            new { pointOfSalePublicId = esc.Punto, operatingDate = hoy.ToString("yyyy-MM-dd") }), "Inventory.DayClose.AlreadyClosed");
    }

    [FactDeRendimiento]
    public async Task Una_sesion_con_300_ventas_y_6_medios_se_cierra_en_menos_de_5_minutos()
    {
        var esc = await PrepararAsync(fx, "cierre300");
        using var http = fx.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(30);
        var cajero = esc.Cajero2.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja2);
        await esc.Inv.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P1", 2_000, 1_000m)]);

        var medios = new Func<int, decimal, object>[]
        {
            (_, v) => esc.Efectivo(v),
            (i, v) => new { paymentMeansPublicId = esc.Medio("VISARB"), amount = v, authorizationCode = $"{i:000000}", last4 = "4242", cardTerminalPublicId = esc.DatafonoRedeban },
            (i, v) => new { paymentMeansPublicId = esc.Medio("MCCB"), amount = v, authorizationCode = $"{i:000000}", last4 = "5555", cardTerminalPublicId = esc.DatafonoCredibanco },
            (i, v) => new { paymentMeansPublicId = esc.Medio("BONOMERC"), amount = v, reference = $"B300-{i:0000}" },
            (i, v) => new { paymentMeansPublicId = esc.Medio("TRANSFBCO"), amount = v, reference = $"T300-{i:0000}" },
            (_, v) => esc.Efectivo(v, v + 1_000m),
        };
        for (var i = 0; i < 300; i++)
        {
            var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P1").CodigoDeBarras);
            await CobrarAsync(http, cajero, VentaId(venta), AmountDue(venta), [medios[i % medios.Length](i, AmountDue(venta))]);
        }

        var reloj = Stopwatch.StartNew();
        var cierre = await esc.CerrarAsync(http, cajero, sesion);
        cierre.StatusCode.Should().Be(HttpStatusCode.OK, await cierre.Content.ReadAsStringAsync());
        var informe = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Get, $"{Sesiones}/{sesion}/count-report");
        informe.StatusCode.Should().Be(HttpStatusCode.OK);
        reloj.Stop();
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(5), "SC-025: cierre e informe listos para imprimir");
    }

    private static async Task<Guid> MovimientoAsync(HttpClient http, string token, object cuerpo)
    {
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, Movimientos, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"movimiento: {await resp.Content.ReadAsStringAsync()}");
        return (await InventarioE2E.LeerAsync(resp)).GetProperty("documentPublicId").GetGuid();
    }

    /// <summary>Confirma el movimiento, que su política deja en aprobación, y lo aprueba el supervisor; queda confirmado.</summary>
    private static async Task AprobarAsync(HttpClient http, EscenarioDeVentas esc, string token, Guid movimiento)
    {
        var confirmar = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Movimientos}/{movimiento}/confirm", new { });
        confirmar.StatusCode.Should().Be(HttpStatusCode.OK, await confirmar.Content.ReadAsStringAsync());
        var resultado = await InventarioE2E.LeerAsync(confirmar);
        resultado.GetProperty("status").GetInt32().Should().Be(1, "PendingApproval por la política del tipo");
        var solicitud = resultado.GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, solicitud, aprobar: true));
        (await InventarioE2E.GetAsync(http, token, $"{Movimientos}/{movimiento}")).GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
    }
}
