using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadDeInventarioE2E;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// T570 (feature 012, I2 con las ventas de I3; quickstart §4.2, §4.5, §4.8, §5.9; US7-3, US7-8, US7-9, US7-10; FR-077, FR-078, FR-090),
/// en el motor de <c>DB_PROVIDER</c>, los casos de la integración contable que necesitan ventas y no caben en
/// <see cref="ContabilizacionPorMensajesTests"/>, cada uno en su cooperativa aislada con la contabilidad iniciada
/// (<see cref="ContabilidadDeVentasE2E"/>) y el despachador conducido por la prueba (<see cref="Integration.ConductorDelDespachador"/>):
/// el lote resumido de las ventas de dos cajeros, la anulación de una factura del resumido, la tercera bodega que entra después de las
/// ventas con su <c>AjusteDeCostoReconocido</c> (caso dorado 17); la venta sin paso anulada después de pasar el tipo a en línea y su
/// envío posterior en orden; y el lote que ordena el cierre del turno (<c>CierreDeTurno</c>, parte de T566). Al final de cada uno,
/// huérfanos cero en los dos sentidos y el balance de prueba cuadrado. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ContabilizacionDeVentasPorMensajesTests(CentralIdentityApiFixture fx)
{
    private const string Facturas = "/api/inventory/sales/invoices";

    [Fact]
    public async Task Las_ventas_de_dos_cajeros_van_en_un_resumido_la_anulacion_en_otro_comprobante_y_la_bodega_tardia_ajusta_su_costo()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "ventaslote");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var coop = esc.Coop.TenantPublicId;
        var hoy = InventarioE2E.HoyEnColombia;
        var desde = new DateOnly(hoy.Year, 1, 1);
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "PorLotes", desde, cadena: "Sales");
        await ParametroAsync(http, t, "Contabilidad.Granularidad", "Resumido", desde, tipo: esc.TipoRv);
        // El lote del día a las 10:10 con el reloj de la suite a las 10:00 de hoy: lo corre el proceso (actor Process, §10 de
        // contabilidad.md), no una persona; el caso no depende de la hora en que corra la prueba.
        var conductor = fx.Despachador;
        var ahora = conductor.Reloj.AhoraLocal;
        using var _ = conductor.Adelantar(new DateTimeOffset(hoy.ToDateTime(new TimeOnly(10, 0)), ahora.Offset) - ahora);
        await ParametroAsync(http, t, "Contabilidad.HoraDeLote", "10:10", hoy, tipo: esc.TipoRv);
        foreach (var grupo in new[] { "ASEO", "ABARROTES" })
            await ReglaAsync(http, t, "AjusteDeCosto", "Costo", ContabilidadDeVentasE2E.CostoDeVentas, desde, new { accountingGroupCode = grupo });
        await ReglaAsync(http, t, "AjusteDeCosto", "Contrapartida", ContabilidadDeVentasE2E.CostoDeVentas, desde, new { reasonCode = "Retroactive" });

        // (1) Ventas del día de dos cajeros, una a crédito de X: todas quedan en lote.
        var x = await ContabilidadDeVentasE2E.AsociadoAsync(http, t, "Ximena", "72000001");
        var sesion1 = await SesionAsync(http, esc.Cajero1.Token, esc.Caja1);
        var sesion2 = await SesionAsync(http, esc.Cajero2.Token, esc.Caja2);
        var ventas = new List<Guid>
        {
            (await esc.VentaDeContadoAsync(http, esc.Cajero1.Token, sesion1, "P7")).GetProperty("documentPublicId").GetGuid(),
            (await esc.VentaDeContadoAsync(http, esc.Cajero1.Token, sesion1, "P7")).GetProperty("documentPublicId").GetGuid(),
            (await esc.VentaDeContadoAsync(http, esc.Cajero2.Token, sesion2, "P7")).GetProperty("documentPublicId").GetGuid(),
        };
        var aCredito = await NuevaVentaAsync(http, esc.Cajero2.Token, sesion2, esc.Inv.P("P7").CodigoDeBarras);
        await InventarioE2E.ExitoAsync(http, esc.Cajero2.Token, HttpMethod.Patch, $"{Borradores}/{VentaId(aCredito)}", new { customerPersonPublicId = x });
        var enAprobacion = await CobrarAsync(http, esc.Cajero2.Token, VentaId(aCredito), 100_000m,
            [new { paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 100_000m, credit = new { installments = 1, termDays = 30, periodicityDays = 30 } }]);
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, t, esc.Supervisor.Token, enAprobacion.GetProperty("approvalRequestPublicId").GetGuid(), aprobar: true));
        ventas.Add(VentaId(aCredito));
        foreach (var v in ventas) Estado(await MensajeAsync(http, t, v, "VentaFacturada")).Should().Be(1, "InBatch hasta su lote");

        // (2) El lote resumido del día, a su hora: un FV por fecha, tipo y sucursal, con la fecha de las ventas, que lista sus documentos.
        conductor.Reloj.Desfase += TimeSpan.FromMinutes(12);
        var programado = await conductor.EnviarComoProcesoAsync<IngenIA365ERP.Application.Common.Models.Result<IngenIA365ERP.Application.Common.Integration.LotesProgramadosDto>>(
            coop, "Tarea:prueba", new IngenIA365ERP.Application.Common.Integration.ScheduleIntegrationBatchesCommand(30));
        programado.IsSuccess.Should().BeTrue();
        programado.Value.Created.Should().ContainSingle("una franja, un lote");
        await conductor.PasadasAsync(coop, 3);
        var lote = (await InventarioE2E.GetAsync(http, t, "/api/accounting/inventory/batches?trigger=Scheduled&pageSize=50")).GetProperty("items")
            .EnumerateArray().Single().GetProperty("batchPublicId").GetGuid();
        var detalle = await InventarioE2E.GetAsync(http, t, $"/api/accounting/inventory/batches/{lote}");
        detalle.GetProperty("batch").GetProperty("status").GetInt32().Should().Be(2, $"el lote termina completo: {detalle}");
        var comprobantes = detalle.GetProperty("vouchers").EnumerateArray().Where(c => c.GetProperty("voucherTypeCode").GetString() == "FV").ToList();
        comprobantes.Should().ContainSingle("las ventas de los dos cajeros, de un día, un tipo y una sucursal, van en un solo FV");
        DateOnly.Parse(comprobantes[0].GetProperty("operationDate").GetString()!).Should().Be(hoy, "con la fecha de las ventas");
        detalle.GetProperty("documents").EnumerateArray().Select(d => d.ToString()).Should().HaveCountGreaterThanOrEqualTo(4);
        foreach (var v in ventas) Estado(await MensajeAsync(http, t, v, "VentaFacturada")).Should().Be(2);
        var resumido = Comprobante(await MensajeAsync(http, t, ventas[0], "VentaFacturada"))!.Value;

        // El detalle que la cuenta exige se conserva: el crédito de X con su tercero y su cruce.
        var lineas = await ContabilidadDeVentasE2E.LineasDelComprobanteAsync(http, t, resumido);
        var delCredito = lineas.Single(l => ContabilidadDeVentasE2E.Cuenta(l) == ContabilidadDeVentasE2E.CreditoAsociados);
        delCredito.GetProperty("personPublicId").GetGuid().Should().Be(x);
        delCredito.GetProperty("crossDocumentType").GetString().Should().Be("FV");
        lineas.Where(l => ContabilidadDeVentasE2E.Cuenta(l) == ContabilidadDeVentasE2E.CajaPv1).Should().ContainSingle("el efectivo de las tres ventas se suma");

        // Los recibos: un solo comprobante para las cuatro, actor el proceso y los dos cajeros como usuarios de origen (FR-077).
        var ids = string.Join(",", ventas.Select(v => $"'{v}'"));
        (await EnteroAsync(fx, esc.Inv,
            $"""SELECT COUNT(DISTINCT "AccountingDocumentId") FROM dbo."ACC_InventoryPostings" WHERE "MessageType" = 'VentaFacturada' AND "SourcePublicId" IN ({ids})""",
            $"SELECT COUNT(DISTINCT [AccountingDocumentId]) FROM [dbo].[ACC_InventoryPostings] WHERE [MessageType] = 'VentaFacturada' AND [SourcePublicId] IN ({ids})"))
            .Should().Be(1);
        (await EnteroAsync(fx, esc.Inv,
            $"""SELECT COUNT(*) FROM dbo."ACC_InventoryPostings" WHERE "MessageType" = 'VentaFacturada' AND "SourcePublicId" IN ({ids}) AND "ActorKind" <> 2""",
            $"SELECT COUNT(*) FROM [dbo].[ACC_InventoryPostings] WHERE [MessageType] = 'VentaFacturada' AND [SourcePublicId] IN ({ids}) AND [ActorKind] <> 2"))
            .Should().Be(0, "lo contabiliza el proceso");
        // Las tres de contado las originan los dos cajeros; la de crédito, quien dio la última aprobación, que es quien la confirmó (T33).
        var deContado = string.Join(",", ventas.Take(3).Select(v => $"'{v}'"));
        (await EnteroAsync(fx, esc.Inv,
            $"""SELECT COUNT(DISTINCT "OriginUserCentralId") FROM dbo."ACC_InventoryPostings" WHERE "MessageType" = 'VentaFacturada' AND "SourcePublicId" IN ({deContado})""",
            $"SELECT COUNT(DISTINCT [OriginUserCentralId]) FROM [dbo].[ACC_InventoryPostings] WHERE [MessageType] = 'VentaFacturada' AND [SourcePublicId] IN ({deContado})"))
            .Should().Be(2, "los dos cajeros quedan como usuarios de origen, como dato");

        // (3) Anular una venta del resumido: comprobante nuevo con su fecha, el resumido intacto (US7-8).
        var anular = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Facturas}/{ventas[0]}/void", new { reason = "El cliente no se llevó la mercancía" });
        anular.StatusCode.Should().Be(HttpStatusCode.Created, await anular.Content.ReadAsStringAsync());
        var anulacion = (await InventarioE2E.LeerAsync(anular)).GetProperty("voidingDocumentPublicId").GetGuid();
        Estado(await MensajeAsync(http, t, anulacion, "DocumentoAnulado")).Should().Be(1, "la anulación sigue el modo sellado del original");
        await OrdenarLoteAsync(http, t, hoy, null, "Lote de la anulación");
        await fx.Despachador.PasadasAsync(coop, 3);
        var mensajeDeAnulacion = await MensajeAsync(http, t, anulacion, "DocumentoAnulado");
        Estado(mensajeDeAnulacion).Should().Be(2, $"la anulación se contabiliza: {mensajeDeAnulacion}");
        Comprobante(mensajeDeAnulacion).Should().NotBe(resumido, "es un comprobante nuevo");
        (await InventarioE2E.GetAsync(http, t, $"/api/accounting/documents/{resumido}")).GetProperty("status").ToString()
            .Should().NotBe("Reversed", "el resumido queda intacto").And.NotBe("3");

        // (4) Caso dorado 17: una tercera bodega entra con su saldo a un corte anterior a las ventas; cada venta afectada recibe su ajuste.
        var tiposDeBodega = (await InventarioE2E.GetAsync(http, t, "/api/inventory/warehouse-types")).EnumerateArray().ToList();
        var pv3 = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/warehouses", new
        {
            code = "PV3", name = "Punto de venta 3", branchPublicId = esc.Inv.S2,
            warehouseTypePublicId = tiposDeBodega.Single(w => w.GetProperty("code").GetString() == "PUNTOVENTA").GetProperty("publicId").GetGuid(),
        })).GetProperty("warehouse").GetProperty("publicId").GetGuid();
        await InventarioE2E.AlcanceAsync(http, t, esc.Supervisor.PublicId, esc.Inv.Bodega("PV1"), pv3);
        var saldos = await InventarioE2E.RevisarYAplicarAsync(http, t, "/api/inventory/opening-balances", InventarioE2E.Libro(("Datos",
            ["bodega", "fechaDeCorte", "producto", "ubicacion", "cantidad", "costoUnitario"],
            [["PV3", esc.Inv.Corte.ToString("yyyy-MM-dd"), "P7", null, "500", "90000"]])));
        var saldo = saldos.GetProperty("extra").GetProperty("documents")[0].GetProperty("documentPublicId").GetGuid();
        var enviado = await InventarioE2E.ConfirmarAsync(http, t, "/api/inventory/opening-balances", saldo);
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, t, esc.Supervisor.Token, enviado.GetProperty("approval").GetProperty("requestPublicId").GetGuid(), aprobar: true));
        var ajustes = await EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationMessages" WHERE "Type" = 'AjusteDeCostoReconocido'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessages] WHERE [Type] = 'AjusteDeCostoReconocido'");
        ajustes.Should().BeGreaterThanOrEqualTo(ventas.Count, "un AjusteDeCostoReconocido por documento afectado: las ventas de P7 costaron distinto");
        // Los ajustes siguen el destino de lo que ajustan (por lotes) y se fechan en su fecha efectiva: el lote va del corte a hoy.
        await OrdenarLoteAsync(http, t, hoy, null, "Lote de los ajustes de costo", desde: esc.Inv.Corte);
        await fx.Despachador.PasadasAsync(coop, 3);
        (await EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationMessageDeliveries" d JOIN dbo."COR_IntegrationMessages" m ON m."Id" = d."MessageId" WHERE m."Type" = 'AjusteDeCostoReconocido' AND d."Destination" = 'Accounting' AND d."Status" <> 2""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationMessageDeliveries] d JOIN [dbo].[COR_IntegrationMessages] m ON m.[Id] = d.[MessageId] WHERE m.[Type] = 'AjusteDeCostoReconocido' AND d.[Destination] = 'Accounting' AND d.[Status] <> 2"))
            .Should().Be(0, "cada ajuste de costo llega a Contabilidad");

        // (5) Huérfanos cero en los dos sentidos y el libro cuadra.
        (await ComprobantesSinReciboAsync(fx, esc.Inv)).Should().Be(0);
        (await RecibosSinComprobanteAsync(fx, esc.Inv)).Should().Be(0);
        (await DescuadreDelLibroAsync(fx, esc.Inv)).Should().Be(0m);
    }

    [Fact]
    public async Task Una_venta_sin_paso_anulada_despues_de_pasar_a_en_linea_sigue_sin_pasar_y_el_envio_posterior_la_manda_con_su_anulacion()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "ventasnopasa");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var coop = esc.Coop.TenantPublicId;
        var hoy = InventarioE2E.HoyEnColombia;
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "NoPasa", new DateOnly(hoy.Year, 1, 1), cadena: "Sales");

        var sesion = await SesionAsync(http, esc.Cajero1.Token, esc.Caja1);
        var venta = (await esc.VentaDeContadoAsync(http, esc.Cajero1.Token, sesion, "P7")).GetProperty("documentPublicId").GetGuid();
        Estado(await MensajeAsync(http, t, venta, "VentaFacturada")).Should().Be(4, "NotApplicable: el tipo no pasa");

        // La cadena pasa a en línea desde hoy; la venta ya sellada «no pasa» y su anulación sigue al original (FR-078, FR-079).
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "EnLinea", hoy, cadena: "Sales");
        var anular = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Facturas}/{venta}/void", new { reason = "Error de cobro" });
        anular.StatusCode.Should().Be(HttpStatusCode.Created, await anular.Content.ReadAsStringAsync());
        var anulacion = (await InventarioE2E.LeerAsync(anular)).GetProperty("voidingDocumentPublicId").GetGuid();
        await fx.Despachador.PasadasAsync(coop, 2);
        Estado(await MensajeAsync(http, t, venta, "VentaFacturada")).Should().Be(4);
        Estado(await MensajeAsync(http, t, anulacion, "DocumentoAnulado")).Should().Be(4, "la anulación de lo que no pasó tampoco pasa");

        // El envío posterior la manda con su anulación, en orden (US7-9, US7-10).
        var tipos = (await InventarioE2E.GetAsync(http, t, "/api/inventory/document-types")).EnumerateArray()
            .ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("publicId").GetGuid());
        var corte = (await MensajeAsync(http, t, anulacion, "DocumentoAnulado")).GetProperty("messagePublicId").GetGuid();
        var envio = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/inventory/messages/send-not-applicable", new
        {
            from = hoy.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"), documentTypePublicIds = new[] { esc.TipoRv, tipos["ANU"] },
            cutoffMessagePublicId = corte, reason = "La contadora pidió las ventas del día",
        });
        envio.StatusCode.Should().Be(HttpStatusCode.Accepted, await envio.Content.ReadAsStringAsync());
        await fx.Despachador.PasadasAsync(coop, 3);
        var facturada = await MensajeAsync(http, t, venta, "VentaFacturada");
        var anulada = await MensajeAsync(http, t, anulacion, "DocumentoAnulado");
        Estado(facturada).Should().Be(2, $"enviada después, se contabiliza: {facturada}");
        Estado(anulada).Should().Be(2, $"y su anulación detrás: {anulada}");
        TipoDeComprobante(facturada).Should().Be("FV");

        (await ComprobantesSinReciboAsync(fx, esc.Inv)).Should().Be(0);
        (await RecibosSinComprobanteAsync(fx, esc.Inv)).Should().Be(0);
        (await DescuadreDelLibroAsync(fx, esc.Inv)).Should().Be(0m);
    }

    [Fact]
    public async Task Con_el_disparador_CierreDeTurno_el_cierre_de_la_sesion_ordena_su_lote()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "ventasturno");
        using var http = fx.CreateClient();
        var t = esc.Admin;
        var coop = esc.Coop.TenantPublicId;
        var desde = new DateOnly(InventarioE2E.HoyEnColombia.Year, 1, 1);
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "PorLotes", desde, cadena: "Sales");
        await ParametroAsync(http, t, "Contabilidad.DisparadorDeLote", "CierreDeTurno", desde, tipo: esc.TipoRv);

        var sesion = await SesionAsync(http, esc.Cajero1.Token, esc.Caja1);
        var venta = (await esc.VentaDeContadoAsync(http, esc.Cajero1.Token, sesion, "P7")).GetProperty("documentPublicId").GetGuid();
        Estado(await MensajeAsync(http, t, venta, "VentaFacturada")).Should().Be(1);

        var cierre = await esc.CerrarAsync(http, esc.Cajero1.Token, sesion);
        cierre.StatusCode.Should().Be(HttpStatusCode.OK, await cierre.Content.ReadAsStringAsync());
        var lote = (await InventarioE2E.LeerAsync(cierre)).GetProperty("batch");
        lote.ValueKind.Should().Be(JsonValueKind.Object, "el cierre ordena el lote del turno en la misma transacción (T12)");
        lote.GetProperty("trigger").GetString().Should().Be("CashSessionClose");

        await fx.Despachador.PasadasAsync(coop, 3);
        var facturada = await MensajeAsync(http, t, venta, "VentaFacturada");
        Estado(facturada).Should().Be(2, $"el lote del turno la contabiliza: {facturada}");
        TipoDeComprobante(facturada).Should().Be("FV");
        (await DescuadreDelLibroAsync(fx, esc.Inv)).Should().Be(0m);
    }

    /// <summary>Vista previa y orden de un lote manual del día (opcionalmente por tipos); devuelve el lote.</summary>
    private static async Task<Guid> OrdenarLoteAsync(HttpClient http, string t, DateOnly dia, string[]? tipos, string motivo, DateOnly? desde = null)
    {
        var inicio = (desde ?? dia).ToString("yyyy-MM-dd");
        var previa = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches/preview", new
        {
            from = inicio, to = dia.ToString("yyyy-MM-dd"), documentTypeCodes = tipos,
        });
        previa.GetProperty("cutoffMessagePublicId").ValueKind.Should().Be(JsonValueKind.String, $"hay algo que ordenar: {previa}");
        var orden = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, "/api/accounting/inventory/batches", new
        {
            cutoffMessagePublicId = previa.GetProperty("cutoffMessagePublicId").GetGuid(), from = inicio, to = dia.ToString("yyyy-MM-dd"),
            documentTypeCodes = tipos, reason = motivo,
        });
        orden.StatusCode.Should().Be(HttpStatusCode.Accepted, await orden.Content.ReadAsStringAsync());
        return (await InventarioE2E.LeerAsync(orden)).GetProperty("batchPublicId").GetGuid();
    }
}
