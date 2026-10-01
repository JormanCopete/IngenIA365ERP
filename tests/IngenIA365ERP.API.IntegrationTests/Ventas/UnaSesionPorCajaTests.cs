using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T563 (feature 012, I3; US5-5, T50, data-model §15 «Concurrencia», FR-058, contracts/api.md §20.1 y §21.1), en el motor de
/// <c>DB_PROVIDER</c>: una sesión abierta por caja y por cajero también bajo concurrencia (la colisión del índice se traduce al
/// mismo error), una caja sin sesión no vende, un cobro que corre contra el cierre o entra en el arqueo o responde <c>NotOpen</c>,
/// un día cerrado no abre sesiones, y un punto sin POS cobra en la oficina pero no abre ventas del POS ni lee productos.
/// Cooperativa aislada «unasesion»: el cierre del día la deja sin sesiones hoy en PV1. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class UnaSesionPorCajaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_sesion_por_caja_y_por_cajero_tambien_bajo_concurrencia()
    {
        var esc = await PrepararAsync(fx, "unasesion");
        using var http = fx.CreateClient();

        // Dos aperturas simultáneas de la misma caja: una abre y la otra RegisterBusy.
        var carrera = await Task.WhenAll(PedirAbrirAsync(http, esc.Cajero1.Token, esc.Caja1), PedirAbrirAsync(http, esc.Cajero2.Token, esc.Caja1));
        carrera.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, "una sola sesión abierta por caja");
        var perdedora = carrera.Single(r => r.StatusCode != HttpStatusCode.Created);
        var registro = await InventarioE2E.FallaAsync(perdedora, "Inventory.CashSession.RegisterBusy");
        registro.GetProperty("data").GetProperty("cashierName").GetString().Should().NotBeNullOrWhiteSpace();
        var ganador = carrera[0].StatusCode == HttpStatusCode.Created ? esc.Cajero1 : esc.Cajero2;
        var otro = ganador == esc.Cajero1 ? esc.Cajero2 : esc.Cajero1;
        var sesion = (await InventarioE2E.LeerAsync(carrera.Single(r => r.StatusCode == HttpStatusCode.Created)))
            .GetProperty("session").GetProperty("cashSessionPublicId").GetGuid();

        // El que ya tiene sesión no abre otra en la caja 2 (Caja.UnaSesionPorCajero, verdadero por defecto).
        var enLaDos = await InventarioE2E.FallaAsync(await PedirAbrirAsync(http, ganador.Token, esc.Caja2), "Inventory.CashSession.CashierBusy");
        enLaDos.GetProperty("data").GetProperty("cashRegisterCode").GetString().Should().Be("CJ1");

        // El mismo cajero en dos cajas a la vez: una abre y la otra CashierBusy (la colisión del índice, traducida).
        var caja3 = await CajaAsync(http, esc.Admin, esc.Punto, "CJ3", "Caja 3", esc.Inv.Bodega("PV1"), null, [("PosSale", esc.TipoRv)]);
        var doble = await Task.WhenAll(PedirAbrirAsync(http, otro.Token, esc.Caja2), PedirAbrirAsync(http, otro.Token, caja3));
        doble.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, "un cajero, una sesión abierta");
        await InventarioE2E.FallaAsync(doble.Single(r => r.StatusCode != HttpStatusCode.Created), "Inventory.CashSession.CashierBusy");
        var sesionDelOtro = (await InventarioE2E.LeerAsync(doble.Single(r => r.StatusCode == HttpStatusCode.Created)))
            .GetProperty("session").GetProperty("cashSessionPublicId").GetGuid();

        // Una venta del POS sólo se abre en la sesión propia y abierta.
        var ajena = await InventarioE2E.MandarAsync(http, otro.Token, HttpMethod.Post, Borradores, new { cashSessionPublicId = sesion });
        await InventarioE2E.FallaAsync(ajena, "Inventory.CashSession.NotOpen");

        // Un cobro contra un cierre simultáneo: o el cierre incluye la venta, o el cobro responde NotOpen.
        var venta = await NuevaVentaAsync(http, ganador.Token, sesion, esc.Inv.P("P1").CodigoDeBarras);
        var total = AmountDue(venta);
        var efectivo = esc.Medio("EFECTIVO");
        var cierre = new { counts = new[] { new { paymentMeansPublicId = efectivo, countedTotal = 200_000m, reason = "Arqueo del ensayo de concurrencia" } } };
        var (cobro, cerrado) = (PedirCobrarAsync(http, ganador.Token, VentaId(venta), total, [esc.Efectivo(total)]),
            InventarioE2E.MandarAsync(http, ganador.Token, HttpMethod.Post, $"{Sesiones}/{sesion}/close", cierre));
        await Task.WhenAll(cobro, cerrado);
        var respuestaDelCobro = await cobro;
        var respuestaDelCierre = await cerrado;
        var textoDelCierre = await respuestaDelCierre.Content.ReadAsStringAsync();
        if (respuestaDelCobro.StatusCode == HttpStatusCode.OK)
        {
            if (respuestaDelCierre.StatusCode == HttpStatusCode.OK)
            {
                var lineas = JsonDocument.Parse(textoDelCierre).RootElement.GetProperty("lines").EnumerateArray();
                lineas.Single(l => l.GetProperty("paymentMeansCode").GetString() == "EFECTIVO").GetProperty("expected").GetDecimal()
                    .Should().Be(200_000m + total, "si el cobro entró antes, el cierre lo incluye en lo esperado");
            }
            else
            {
                // El cierre perdió la carrera por la venta que todavía estaba abierta cuando leyó los borradores.
                ((int)respuestaDelCierre.StatusCode).Should().Be(422, textoDelCierre);
                JsonDocument.Parse(textoDelCierre).RootElement.GetProperty("code").GetString().Should().BeOneOf("Inventory.CashSession.HasOpenDrafts");
                var otraVez = await CerrarConMotivoAsync(http, ganador.Token, sesion, efectivo, 200_000m);
                otraVez.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("paymentMeansCode").GetString() == "EFECTIVO")
                    .GetProperty("expected").GetDecimal().Should().Be(200_000m + total);
            }
        }
        else
        {
            await InventarioE2E.FallaAsync(respuestaDelCobro, "Inventory.CashSession.NotOpen");
            respuestaDelCierre.StatusCode.Should().Be(HttpStatusCode.OK, textoDelCierre);
            JsonDocument.Parse(textoDelCierre).RootElement.GetProperty("lines").EnumerateArray()
                .Single(l => l.GetProperty("paymentMeansCode").GetString() == "EFECTIVO").GetProperty("expected").GetDecimal()
                .Should().Be(200_000m, "el cobro que no entró no cuenta");
        }

        // Una caja sin sesión no vende: la sesión cerrada ya no abre ventas.
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, ganador.Token, HttpMethod.Post, Borradores, new { cashSessionPublicId = sesion }),
            "Inventory.CashSession.NotOpen");

        // El cierre del día del punto: con una sesión abierta no se hace; cerrada, sí, y después no se abre otra con esa fecha.
        var hoy = InventarioE2E.HoyEnColombia;
        var diaAbierto = await InventarioE2E.MandarAsync(http, esc.Supervisor.Token, HttpMethod.Post, "/api/inventory/day-closes",
            new { pointOfSalePublicId = esc.Punto, operatingDate = hoy.ToString("yyyy-MM-dd") });
        await InventarioE2E.FallaAsync(diaAbierto, "Inventory.DayClose.SessionsOpen");
        await CerrarConMotivoAsync(http, otro.Token, sesionDelOtro, efectivo, 200_000m);
        var dia = await InventarioE2E.MandarAsync(http, esc.Supervisor.Token, HttpMethod.Post, "/api/inventory/day-closes",
            new { pointOfSalePublicId = esc.Punto, operatingDate = hoy.ToString("yyyy-MM-dd") });
        dia.StatusCode.Should().Be(HttpStatusCode.Created, await dia.Content.ReadAsStringAsync());
        await InventarioE2E.FallaAsync(await PedirAbrirAsync(http, ganador.Token, esc.Caja1), "Inventory.CashSession.DayClosed");
    }

    [Fact]
    public async Task Un_punto_sin_POS_cobra_en_la_oficina_pero_no_abre_ventas_del_POS_ni_lee()
    {
        var esc = await PrepararAsync(fx, "sinpos");
        using var http = fx.CreateClient();
        var t = esc.Admin;

        // Un punto de oficina en la sucursal dos (bodega PV2) sin POS, con su caja.
        var oficina = await PuntoAsync(http, t, "OFI", "Oficina", esc.Inv.S2, esc.Canal, esc.Inv.Bodega("PV2"), posHabilitado: false);
        var caja = await CajaAsync(http, t, oficina, "OF1", "Caja oficina", esc.Inv.Bodega("PV2"), null, [("PosSale", esc.TipoRv)]);
        await esc.Inv.AjusteConfirmadoAsync(http, t, "AJP", "PV2", [new("P7", 10, 60_000m)]);
        var sesion = await AbrirAsync(http, t, caja);

        var detalle = await InventarioE2E.GetAsync(http, t, $"{Sesiones}/{sesion}");
        var punto = detalle.TryGetProperty("session", out var s) ? s.GetProperty("pointOfSale") : detalle.GetProperty("pointOfSale");
        punto.GetProperty("posEnabled").GetBoolean().Should().BeFalse("la pantalla oculta el enlace al POS");

        // La venta de oficina en efectivo sí se cobra con la sesión del punto (T50).
        var borrador = await InventarioE2E.BorradorAsync(http, t, "/api/inventory/sales/invoices", new
        {
            documentTypePublicId = esc.TipoRv, warehousePublicId = esc.Inv.Bodega("PV2"),
            lines = new[] { new { productPublicId = esc.Inv.P("P7").Id, unitPublicId = esc.Inv.P("P7").Unidad, quantity = 1m } },
            payments = new[] { new { paymentMeansPublicId = esc.Medio("EFECTIVO"), amount = 100_000m, cashSessionPublicId = sesion } },
        });
        var id = borrador.GetProperty("documentPublicId").GetGuid();
        var confirmada = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"/api/inventory/sales/invoices/{id}/confirm", new { expectedAmountDue = 100_000m });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());
        (await InventarioE2E.LeerAsync(confirmada)).GetProperty("status").GetInt32().Should().Be(2, "Confirmed");

        // Pero el POS no: abrir una venta ni leer un producto con esa sesión.
        await InventarioE2E.FallaAsync(await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, Borradores, new { cashSessionPublicId = sesion }),
            "Inventory.Pos.NotEnabled");
        var lectura = await InventarioE2E.MandarAsync(http, t, HttpMethod.Get,
            $"/api/inventory/pos/lookup?code={esc.Inv.P("P7").CodigoDeBarras}&cashSession={sesion}");
        await InventarioE2E.FallaAsync(lectura, "Inventory.Pos.NotEnabled");
    }

    private static async Task<JsonElement> CerrarConMotivoAsync(HttpClient http, string token, Guid sesion, Guid efectivo, decimal contado)
    {
        var resp = await InventarioE2E.MandarAsync(http, token, HttpMethod.Post, $"{Sesiones}/{sesion}/close",
            new { counts = new[] { new { paymentMeansPublicId = efectivo, countedTotal = contado, reason = "Arqueo del ensayo" } } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await InventarioE2E.LeerAsync(resp);
    }
}
