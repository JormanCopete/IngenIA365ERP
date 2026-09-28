using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T564 y T569 (feature 012, I3; US5-1, US5-2, US5-3, US5-6, US5-8, US5-9; SC-002, SC-004; FR-054, FR-059, FR-066;
/// quickstart §5.2, §5.4 a §5.7), en el motor de <c>DB_PROVIDER</c>, en la cooperativa aislada «posventa», no obligada a facturar
/// hasta I4 (<see cref="EscenarioDeVentas"/>): la venta del POS con lector y efectivo con vueltas, el pago mixto con cuatro medios
/// (sólo los cuatro últimos dígitos de cada tarjeta), la suma que no cuadra, el cobro repetido con la misma clave, suspender y
/// recuperar en otra caja, quitar, reimprimir y descartar auditados, la devolución al costo con que salió y el descuento sobre el
/// tope que aprueba el supervisor. El caso «cada pago llega a su cuenta» (T567) necesita la contabilidad iniciada y corre en su
/// propia cooperativa (<see cref="CadaPagoLlegaASuCuentaTests"/>). (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class VentaPosCompletaTests(CentralIdentityApiFixture fx)
{
    private const string Escenario = "posventa";

    [Fact]
    public async Task Seis_lecturas_de_cinco_productos_y_efectivo_con_vueltas_en_menos_de_30_segundos()
    {
        var esc = await PrepararAsync(fx, Escenario);
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        var reloj = Stopwatch.StartNew();
        var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P1").CodigoDeBarras);
        foreach (var p in new[] { "P2", "P3", "P4", "P3", "P5" }) venta = await LeerCodigoAsync(http, cajero, VentaId(venta), esc.Inv.P(p).CodigoDeBarras);

        var lineas = venta.GetProperty("lines").EnumerateArray().ToList();
        lineas.Should().HaveCount(5, "seis lecturas de cinco productos: la repetida suma cantidad");
        lineas.Single(l => l.GetProperty("product").GetProperty("code").GetString() == "P3").GetProperty("quantity").GetDecimal().Should().Be(2m);
        var total = AmountDue(venta);
        total.Should().Be(Precios["P1"] + Precios["P2"] + 2 * Precios["P3"] + Precios["P4"] + Precios["P5"], "la lista general trae el IVA incluido");

        var entregado = Math.Ceiling(total / 50_000m) * 50_000m;
        var cobro = await CobrarAsync(http, cajero, VentaId(venta), total, [esc.Efectivo(total, entregado)]);
        reloj.Stop();

        cobro.GetProperty("status").GetInt32().Should().Be(2, "Confirmed");
        cobro.GetProperty("change").GetDecimal().Should().Be(entregado - total, "las vueltas");
        cobro.GetProperty("number").GetInt64().Should().BeGreaterThan(0);
        cobro.GetProperty("ticket").ValueKind.Should().Be(JsonValueKind.Object, "el comprobante no electrónico trae la tirilla siempre");
        cobro.GetProperty("ticket").GetProperty("copy").GetBoolean().Should().BeFalse();
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "SC-004: de la primera lectura a la tirilla");

        var documento = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{VentaId(venta)}");
        DateOnly.Parse(documento.GetProperty("operationDate").GetString()!).Should().Be(InventarioE2E.HoyEnColombia);
        documento.GetProperty("cashSessionPublicId").GetGuid().Should().Be(sesion);

        // Una venta hecha a las 19:30 de Colombia (en UTC ya es mañana) queda con la fecha de hoy (HoyLocal): el reloj de la suite a esa hora.
        var conductor = fx.Despachador;
        var ahora = conductor.Reloj.AhoraLocal;
        using (conductor.Adelantar(new DateTimeOffset(InventarioE2E.HoyEnColombia.ToDateTime(new TimeOnly(19, 30)), ahora.Offset) - ahora))
        {
            DateOnly.FromDateTime(conductor.Reloj.UtcNow).Should().Be(InventarioE2E.HoyEnColombia.AddDays(1), "en UTC ya es mañana");
            var deNoche = await esc.VentaDeContadoAsync(http, cajero, sesion, "P1");
            var deNocheDocumento = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{deNoche.GetProperty("documentPublicId").GetGuid()}");
            DateOnly.Parse(deNocheDocumento.GetProperty("operationDate").GetString()!).Should().Be(InventarioE2E.HoyEnColombia, "la fecha es la de Colombia");
        }
    }

    [Fact]
    public async Task Pago_mixto_con_cuatro_medios_la_suma_exacta_y_el_cobro_repetido_con_la_misma_clave()
    {
        var esc = await PrepararAsync(fx, Escenario);
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        // 5 × P7 (excluido de IVA, 100.000) = 500.000.
        var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch,
            $"{Borradores}/{VentaId(venta)}/lines/{venta.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}", new { quantity = 5m });
        var total = AmountDue(venta);
        total.Should().Be(500_000m);

        object Visa(decimal valor, string? referencia = null) => new
        {
            paymentMeansPublicId = esc.Medio("VISARB"), amount = valor, authorizationCode = "123456", last4 = "4242",
            cardTerminalPublicId = esc.DatafonoRedeban, reference = referencia,
        };
        var master = new
        {
            paymentMeansPublicId = esc.Medio("MCCB"), amount = 150_000m, authorizationCode = "654321", last4 = "5555", cardTerminalPublicId = esc.DatafonoCredibanco,
        };
        var bono = new { paymentMeansPublicId = esc.Medio("BONOMERC"), amount = 100_000m, reference = "BONO-POS-0001" };

        // Los pagos no suman el total: TotalMismatch con lo que falta.
        var corto = await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, cajero, VentaId(venta), total, [Visa(200_000m), master, bono, esc.Efectivo(40_000m)]),
            "Payments.TotalMismatch");
        corto.GetProperty("data").GetProperty("missing").GetDecimal().Should().Be(10_000m);

        // Un número de tarjeta completo en cualquier campo se rechaza: nunca se guarda (FR-101).
        await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, cajero, VentaId(venta), total,
            [Visa(200_000m, "4111111111111111"), master, bono, esc.Efectivo(50_000m)]), "Payments.CardNumberNotAllowed");

        // Con la misma clave, tres veces: la misma venta y la marca de repetición (SC-002).
        var clave = Guid.NewGuid();
        object[] pagos = [Visa(200_000m), master, bono, esc.Efectivo(50_000m)];
        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++) respuestas.Add(await PedirCobrarAsync(http, cajero, VentaId(venta), total, pagos, clave));
        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var cuerpos = new List<JsonElement>();
        foreach (var r in respuestas) cuerpos.Add(await InventarioE2E.LeerAsync(r));
        cuerpos.Select(c => c.GetProperty("documentPublicId").GetGuid()).Distinct().Should().ContainSingle();
        cuerpos.Select(c => c.GetProperty("number").GetInt64()).Distinct().Should().ContainSingle("un solo número: una sola venta");
        respuestas.Skip(1).Should().OnlyContain(r => r.Headers.Contains("Idempotent-Replayed")
            && r.Headers.GetValues("Idempotent-Replayed").Single() == "true");

        // Cada pago con su medio, valor y referencia; de las tarjetas sólo los cuatro últimos dígitos.
        var documento = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{VentaId(venta)}");
        var registrados = documento.GetProperty("payments").EnumerateArray().ToList();
        registrados.Should().HaveCount(4);
        registrados.Single(p => p.GetProperty("meansCode").GetString() == "VISARB").GetProperty("last4").GetString().Should().Be("4242");
        registrados.Single(p => p.GetProperty("meansCode").GetString() == "MCCB").GetProperty("authorizationCode").GetString().Should().Be("654321");
        registrados.Single(p => p.GetProperty("meansCode").GetString() == "BONOMERC").GetProperty("reference").GetString().Should().Be("BONO-POS-0001");
        registrados.Sum(p => p.GetProperty("amount").GetDecimal()).Should().Be(500_000m);
        documento.ToString().Should().NotContain("4111111111111111");
        var ventas = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents?number={cuerpos[0].GetProperty("number").GetInt64()}&pageSize=10");
        ventas.GetProperty("items").EnumerateArray().Count(v => v.GetProperty("documentPublicId").GetGuid() == VentaId(venta)).Should().Be(1);
    }

    [Fact]
    public async Task Suspender_recuperar_en_otra_caja_quitar_reimprimir_y_descartar_quedan_auditados()
    {
        var esc = await PrepararAsync(fx, Escenario);
        using var http = fx.CreateClient();
        var sesion1 = await SesionAsync(http, esc.Cajero1.Token, esc.Caja1);
        var sesion2 = await SesionAsync(http, esc.Cajero2.Token, esc.Caja2);

        // cajero.1 suspende una venta con dos líneas.
        var venta = await NuevaVentaAsync(http, esc.Cajero1.Token, sesion1, esc.Inv.P("P1").CodigoDeBarras);
        venta = await LeerCodigoAsync(http, esc.Cajero1.Token, VentaId(venta), esc.Inv.P("P2").CodigoDeBarras);
        var id = VentaId(venta);
        await InventarioE2E.ExitoAsync(http, esc.Cajero1.Token, HttpMethod.Post, $"{Borradores}/{id}/suspend", new { label = "Señora de la chaqueta roja" });
        var suspendidas = await InventarioE2E.GetAsync(http, esc.Cajero2.Token, $"{Borradores}?pointOfSale={esc.Punto}&suspended=true");
        suspendidas.EnumerateArray().Should().Contain(v => v.GetProperty("draftPublicId").GetGuid() == id, "cualquier cajero del punto la ve");

        // No se cierra una sesión con ventas suspendidas.
        await InventarioE2E.FallaAsync(await esc.CerrarAsync(http, esc.Cajero1.Token, sesion1), "Inventory.CashSession.HasOpenDrafts");

        // cajero.2 la recupera en la caja 2: sigue igual, porque vive en el servidor.
        var recuperada = await InventarioE2E.ExitoAsync(http, esc.Cajero2.Token, HttpMethod.Post, $"{Borradores}/{id}/resume", new { cashSessionPublicId = sesion2 });
        recuperada.GetProperty("cashSessionPublicId").GetGuid().Should().Be(sesion2);
        recuperada.GetProperty("lines").GetArrayLength().Should().Be(2);
        recuperada.GetProperty("suspended").ValueKind.Should().Be(JsonValueKind.Null);

        // Quita una línea y cobra; después reimprime con la marca COPIA.
        var p2 = recuperada.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("product").GetProperty("code").GetString() == "P2")
            .GetProperty("linePublicId").GetGuid();
        var sinP2 = await InventarioE2E.ExitoAsync(http, esc.Cajero2.Token, HttpMethod.Delete, $"{Borradores}/{id}/lines/{p2}");
        sinP2.GetProperty("lines").GetArrayLength().Should().Be(1);
        var cobro = await CobrarAsync(http, esc.Cajero2.Token, id, AmountDue(sinP2), [esc.Efectivo(AmountDue(sinP2))]);
        cobro.GetProperty("status").GetInt32().Should().Be(2);
        var copia = await InventarioE2E.ExitoAsync(http, esc.Cajero2.Token, HttpMethod.Post, $"/api/inventory/documents/{id}/reprint",
            new { format = "Ticket80", reason = "El cliente la perdió" });
        copia.GetProperty("copy").GetBoolean().Should().BeTrue();
        copia.GetProperty("ticket").GetProperty("copy").GetBoolean().Should().BeTrue("la tirilla lleva la marca «COPIA»");

        // Descartar otra venta.
        var otra = await NuevaVentaAsync(http, esc.Cajero2.Token, sesion2, esc.Inv.P("P5").CodigoDeBarras);
        await InventarioE2E.ExitoAsync(http, esc.Cajero2.Token, HttpMethod.Post, $"{Borradores}/{VentaId(otra)}/discard", new { reason = "El cliente desistió" });
        (await InventarioE2E.GetAsync(http, esc.Cajero2.Token, $"{Borradores}/{VentaId(otra)}")).GetProperty("status").GetInt32().Should().Be(4, "Discarded");

        // Cada acción queda auditada, una a una.
        foreach (var accion in new[] { "Inventory.Pos.Suspended", "Inventory.Pos.Resumed", "Inventory.Pos.LineRemoved", "Inventory.Document.Reprinted" })
            (await esc.AuditadosAsync(fx, http, accion, id.ToString())).Should().NotBeEmpty($"«{accion}» de la venta {id}");
        (await esc.AuditadosAsync(fx, http, "Inventory.Pos.Discarded", VentaId(otra).ToString())).Should().NotBeEmpty();
    }

    [Fact]
    public async Task La_devolucion_con_nota_no_electronica_reintegra_por_el_mismo_medio_y_al_costo_con_que_salio()
    {
        var esc = await PrepararAsync(fx, Escenario);
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        // P4 sale al promedio de hoy (1.150); después entra más caro y el promedio cambia.
        var costoDeSalida = await CostoPromedioAsync(http, esc, "P4");
        var cobro = await esc.VentaDeContadoAsync(http, cajero, sesion, "P4");
        var venta = cobro.GetProperty("documentPublicId").GetGuid();
        await esc.Inv.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PV1", [new("P4", 100, 9_000m)]);
        (await CostoPromedioAsync(http, esc, "P4")).Should().NotBe(costoDeSalida, "el promedio ya es otro");

        var original = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{venta}");
        var linea = original.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();
        var nota = await InventarioE2E.BorradorAsync(http, cajero, "/api/inventory/sales/credit-notes", new
        {
            originDocumentPublicId = venta, documentTypePublicId = esc.TipoNv, reason = "El cliente devuelve el producto", totalVoid = false, withReturn = true,
            lines = new[] { new { originLinePublicId = linea, quantity = 1m } }, refunds = Array.Empty<object>(),
        });
        var idNota = nota.GetProperty("documentPublicId").GetGuid();
        var aDevolver = nota.GetProperty("totals").GetProperty("amountDue").GetDecimal();
        aDevolver.Should().Be(Precios["P4"]);
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Put, $"/api/inventory/sales/credit-notes/{idNota}", new
        {
            originDocumentPublicId = venta, documentTypePublicId = esc.TipoNv, reason = "El cliente devuelve el producto", totalVoid = false, withReturn = true,
            lines = new[] { new { originLinePublicId = linea, quantity = 1m } },
            refunds = new[] { new { paymentMeansPublicId = esc.Medio("EFECTIVO"), amount = aDevolver, cashSessionPublicId = sesion } },
        });
        var confirmada = await InventarioE2E.MandarAsync(http, cajero, HttpMethod.Post, $"/api/inventory/sales/credit-notes/{idNota}/confirm",
            new { expectedAmountDue = aDevolver });
        confirmada.StatusCode.Should().Be(HttpStatusCode.OK, await confirmada.Content.ReadAsStringAsync());

        var detalle = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{idNota}");
        detalle.GetProperty("class").GetInt32().Should().Be(30, "NonElectronicSalesNote");
        detalle.GetProperty("number").GetInt64().Should().BeGreaterThan(0, "con su propio número");
        var reintegro = detalle.GetProperty("payments").EnumerateArray().Single();
        reintegro.GetProperty("meansCode").GetString().Should().Be("EFECTIVO", "por el mismo medio de la venta");
        reintegro.GetProperty("direction").GetInt32().Should().Be(2, "Refunded");

        var costoDeEntrada = Convert.ToDecimal(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            $"""SELECT k."UnitCost" FROM dbo."INV_KardexEntries" k JOIN dbo."INV_Documents" d ON d."Id" = k."DocumentId" WHERE d."PublicId" = '{idNota}'""",
            $"SELECT k.[UnitCost] FROM [dbo].[INV_KardexEntries] k JOIN [dbo].[INV_Documents] d ON d.[Id] = k.[DocumentId] WHERE d.[PublicId] = '{idNota}'"));
        costoDeEntrada.Should().Be(costoDeSalida, "la mercancía vuelve al costo con que salió, aunque el promedio ya sea otro");
    }

    [Fact]
    public async Task Un_descuento_sobre_el_tope_lo_aprueba_el_supervisor_con_su_identidad_y_cambiar_la_linea_lo_invalida()
    {
        var esc = await PrepararAsync(fx, Escenario);
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P3").CodigoDeBarras);
        var id = VentaId(venta);
        var linea = venta.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid();

        // 12 % sobre una línea con el tope del cajero en 5 %: la línea queda pidiendo aprobación y no se cobra.
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}/lines/{linea}", new { discount = new { percent = 0.12m } });
        var solicitud = SolicitudDelDescuento(venta);
        await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, cajero, id, AmountDue(venta), [esc.Efectivo(AmountDue(venta))]),
            "Inventory.Discount.ApprovalPending");

        // El cajero no la aprueba: sin Inventory.Discounts.Authorize ni siquiera la ve (el mismo 404 que una inexistente, §1.2).
        (await InventarioE2E.DecidirAsync(http, esc.Admin, cajero, solicitud, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Y quien sí puede autorizar tampoco se aprueba a sí mismo: el supervisor pide 20 % (su tope es 15 %) en su propia caja.
        var caja3 = await CajaAsync(http, esc.Admin, esc.Punto, "CJ3", "Caja 3", esc.Inv.Bodega("PV1"), null, [("PosSale", esc.TipoRv)]);
        var sesionDelSupervisor = await SesionAsync(http, esc.Supervisor.Token, caja3);
        var propia = await NuevaVentaAsync(http, esc.Supervisor.Token, sesionDelSupervisor, esc.Inv.P("P5").CodigoDeBarras);
        propia = await InventarioE2E.ExitoAsync(http, esc.Supervisor.Token, HttpMethod.Patch,
            $"{Borradores}/{VentaId(propia)}/lines/{propia.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}", new { discount = new { percent = 0.20m } });
        await InventarioE2E.FallaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, SolicitudDelDescuento(propia), aprobar: true),
            "Approvals.SelfApprovalForbidden");
        await InventarioE2E.ExitoAsync(http, esc.Supervisor.Token, HttpMethod.Post, $"{Borradores}/{VentaId(propia)}/discard", new { reason = "Fin del ensayo" });

        // El supervisor aprueba desde su sesión; después el cajero cambia la cantidad y la aprobación deja de valer.
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, solicitud, aprobar: true));
        var aprobada = await InventarioE2E.GetAsync(http, cajero, $"{Borradores}/{id}");
        EstadoDelDescuento(aprobada).Should().Be("Approved");
        var cambiada = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}/lines/{linea}", new { quantity = 2m });
        EstadoDelDescuento(cambiada).Should().NotBe("Approved", "la huella de la línea cambió y la aprobación caducó");
        await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, cajero, id, AmountDue(cambiada), [esc.Efectivo(AmountDue(cambiada))]),
            "Inventory.Discount.ApprovalPending");

        // Una nueva aprobación sobre la línea como quedó, y el cobro confirma.
        var nueva = SolicitudDelDescuento(cambiada);
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token, nueva, aprobar: true));
        var lista = await InventarioE2E.GetAsync(http, cajero, $"{Borradores}/{id}");
        var cobro = await CobrarAsync(http, cajero, id, AmountDue(lista), [esc.Efectivo(AmountDue(lista))]);
        cobro.GetProperty("status").GetInt32().Should().Be(2);

        // La línea guarda quién aprobó y cómo (OwnSession = 1), nunca con contraseña.
        var metodo = Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, esc.Coop,
            $"""SELECT x."ApprovalMethod" FROM dbo."INV_DocumentLineDiscounts" x JOIN dbo."INV_DocumentLines" l ON l."Id" = x."DocumentLineId" JOIN dbo."INV_Documents" d ON d."Id" = l."DocumentId" WHERE d."PublicId" = '{id}' AND x."ApprovedByUserId" IS NOT NULL AND x."IsDeleted" = false""",
            $"SELECT x.[ApprovalMethod] FROM [dbo].[INV_DocumentLineDiscounts] x JOIN [dbo].[INV_DocumentLines] l ON l.[Id] = x.[DocumentLineId] JOIN [dbo].[INV_Documents] d ON d.[Id] = l.[DocumentId] WHERE d.[PublicId] = '{id}' AND x.[ApprovedByUserId] IS NOT NULL AND x.[IsDeleted] = 0"));
        metodo.Should().Be(1, "OwnSession: el supervisor aprobó desde su sesión");
    }

    // ------------------------------------------------------------------------------------------ ayudas --

    private static JsonElement DescuentoPedido(JsonElement venta) =>
        venta.GetProperty("lines")[0].GetProperty("discounts").EnumerateArray().First(d => d.GetProperty("requiresApproval").GetBoolean());

    private static Guid SolicitudDelDescuento(JsonElement venta) =>
        DescuentoPedido(venta).GetProperty("approval").GetProperty("approvalRequestPublicId").GetGuid();

    private static string? EstadoDelDescuento(JsonElement venta) =>
        DescuentoPedido(venta).GetProperty("approval").GetProperty("status").GetString();

    /// <summary>El costo promedio del producto (el escenario costea por cooperativa: <c>costState.averageCost</c>).</summary>
    private static async Task<decimal> CostoPromedioAsync(HttpClient http, EscenarioDeVentas esc, string producto) =>
        (await esc.Inv.ExistenciaAsync(http, esc.Admin, producto)).GetProperty("costState").GetProperty("averageCost").GetDecimal();
}

/// <summary>
/// T567 (feature 012, I3 con I2; FR-098, SC-024; quickstart §5.4 pasos 4 y 5), en el motor de <c>DB_PROVIDER</c>, en la cooperativa
/// aislada «pospagos» con la contabilidad iniciada (<see cref="ContabilidadDeVentasE2E"/>): con una regla <c>MedioDePago</c> por medio (la
/// del efectivo por punto), el comprobante de la venta mixta lleva un débito por medio a la cuenta de su regla sin digitar nada; con la
/// regla de <c>MCCB</c> inactiva, cobrar con él responde <c>Inventory.Prevalidation.NotPostable</c> con quién corrige, y la completitud lo
/// lista. Está en este archivo con la venta del POS que usa; va en su propia clase porque su cooperativa es otra. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CadaPagoLlegaASuCuentaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Cada_pago_de_la_venta_mixta_llega_a_la_cuenta_de_su_medio_y_sin_regla_no_se_cobra()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "pospagos");
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await SesionAsync(http, cajero, esc.Caja1);

        // (1) Sin la regla de MCCB, la validación previa detiene el cobro y dice quién corrige; la completitud lo lista (SC-024). Va primero:
        // una regla con la que ya se contabilizó hoy no se cierra antes de hoy (Accounting.InventoryRule.RetroactiveOverPosted).
        var reglas = await InventarioE2E.GetAsync(http, esc.Admin, "/api/accounting/inventory/rules?operation=Venta&role=MedioDePago&pageSize=50");
        var reglaMccb = reglas.GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("dimensions").GetProperty("paymentMeansCode").GetString() == "MCCB").GetProperty("rulePublicId").GetGuid();
        var ayer = InventarioE2E.HoyEnColombia.AddDays(-1);
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/accounting/inventory/rules/{reglaMccb}/deactivate",
            new { validTo = ayer.ToString("yyyy-MM-dd"), reason = "Ensayo de SC-024" });

        var otra = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var rechazo = await InventarioE2E.FallaAsync(await PedirCobrarAsync(http, cajero, VentaId(otra), 100_000m,
            [new { paymentMeansPublicId = esc.Medio("MCCB"), amount = 100_000m, authorizationCode = "333333", last4 = "5555", cardTerminalPublicId = esc.DatafonoCredibanco }]),
            "Inventory.Prevalidation.NotPostable");
        rechazo.GetProperty("data").GetProperty("errors").EnumerateArray()
            .Should().Contain(e => e.GetProperty("whoFixes").ValueKind == JsonValueKind.Object, "dice quién corrige");
        (await InventarioE2E.GetAsync(http, esc.Admin, $"/api/accounting/inventory/completeness?date={InventarioE2E.HoyEnColombia:yyyy-MM-dd}"))
            .ToString().Should().Contain("MCCB", "un medio activo sin regla vigente aparece en la completitud");
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Post, $"{Borradores}/{VentaId(otra)}/discard", new { reason = "Sin cuenta para MCCB" });

        // La contadora vuelve a darle cuenta a MCCB desde hoy, y la venta mixta pasa.
        await Accounting.ContabilidadDeInventarioE2E.ReglaAsync(http, esc.Admin, "Venta", "MedioDePago", ContabilidadDeVentasE2E.PorCobrarMaster,
            InventarioE2E.HoyEnColombia, new { paymentMeansCode = "MCCB" });

        // (2) Cada pago llega a la cuenta de su medio sin digitar nada (FR-098).
        var venta = await NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch,
            $"{Borradores}/{VentaId(venta)}/lines/{venta.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}", new { quantity = 5m });
        var cobro = await CobrarAsync(http, cajero, VentaId(venta), 500_000m,
        [
            new { paymentMeansPublicId = esc.Medio("VISARB"), amount = 200_000m, authorizationCode = "111111", last4 = "4242", cardTerminalPublicId = esc.DatafonoRedeban },
            new { paymentMeansPublicId = esc.Medio("MCCB"), amount = 150_000m, authorizationCode = "222222", last4 = "5555", cardTerminalPublicId = esc.DatafonoCredibanco },
            new { paymentMeansPublicId = esc.Medio("BONOMERC"), amount = 100_000m, reference = "BONO-CTA-0001" },
            esc.Efectivo(50_000m),
        ]);
        cobro.GetProperty("status").GetInt32().Should().Be(2);

        await fx.Despachador.PasadasAsync(esc.Coop.TenantPublicId, 2);
        var mensaje = await Accounting.ContabilidadDeInventarioE2E.MensajeAsync(http, esc.Admin, VentaId(venta), "VentaFacturada");
        Accounting.ContabilidadDeInventarioE2E.Estado(mensaje).Should().Be(2, $"la venta en línea queda procesada: {mensaje}");
        var lineas = await ContabilidadDeVentasE2E.LineasDelComprobanteAsync(http, esc.Admin, Accounting.ContabilidadDeInventarioE2E.Comprobante(mensaje)!.Value);
        decimal DebitoEn(string cuenta) => lineas.Where(l => ContabilidadDeVentasE2E.Cuenta(l) == cuenta).Sum(ContabilidadDeVentasE2E.Debito);
        DebitoEn(ContabilidadDeVentasE2E.PorCobrarVisa).Should().Be(200_000m);
        DebitoEn(ContabilidadDeVentasE2E.PorCobrarMaster).Should().Be(150_000m);
        DebitoEn(ContabilidadDeVentasE2E.Bonos).Should().Be(100_000m);
        DebitoEn(ContabilidadDeVentasE2E.CajaPv1).Should().Be(50_000m, "el efectivo, a la caja del punto");
        lineas.Where(l => ContabilidadDeVentasE2E.Cuenta(l) == ContabilidadDeVentasE2E.Ingresos).Sum(ContabilidadDeVentasE2E.Credito).Should().Be(500_000m);

    }
}
