using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Ventas;

/// <summary>
/// T870 (feature 012, I6, US14; quickstart §7 I6; contracts/api.md §18.4, §19.4, §13.4), en el motor de <c>DB_PROVIDER</c> y sobre
/// <c>CanalSimulado</c>: el Independent Test de US14 por HTTP. En la cooperativa aislada «cicloi6» (obligada a facturar, con la emisión
/// configurada): cotización → pedido (<c>to-order</c>) que reserva 4 de 10 (6 disponibles, también en la vista <c>stock</c>) → un pedido
/// fechado atrás cuya reserva vence y la tarea libera sola → remisión que descarga y emite <c>CostoDeVentaReconocido</c> → factura desde
/// remisiones sin kardex nuevo, validada por el canal; una nota crédito sin devolución sin efecto en existencia; una nota débito sobre una
/// venta a crédito provisional (aprobación <c>ProvisionalCredit</c> y <c>AjusteDeVentaACredito</c>); una promoción 3×2 que sale como
/// descuento de línea con su promoción y baja la base del IVA. En «remisioni6», la remisión vieja sin facturar levanta
/// <c>Inventario.RemisionSinFacturar</c> y bloquea el cierre del período hasta aceptarla con motivo y el permiso
/// <c>Inventory.Periods.AcceptUnbilledShipments</c>. Cada escritura lleva <c>Idempotency-Key</c>, y las del ciclo se repiten tres veces
/// con la misma clave: un solo efecto (SC-002). (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class CicloComercialTests(CentralIdentityApiFixture fx)
{
    private const string Cotizaciones = "/api/inventory/sales/quotes";
    private const string Pedidos = "/api/inventory/sales/orders";
    private const string Remisiones = "/api/inventory/sales/shipments";
    private const string Facturas = "/api/inventory/sales/invoices";
    private const string NotasCredito = "/api/inventory/sales/credit-notes";
    private const string NotasDebito = "/api/inventory/sales/debit-notes";
    private const int Confirmado = 2;
    private const int EnAprobacion = 1;

    // ------------------------------------------------------------------------------------------ preparación --

    private sealed record Ciclo(EscenarioDeFacturacionElectronica E, Guid TipoFactura, Guid Comprador)
    {
        public EscenarioDeInventario Inv => E.Inv;
        public string Admin => E.Admin;
        public EscenarioDeVentas.Usuario Supervisor => E.Ventas.Supervisor;
        public Guid Tipo(string codigo) => Inv.Tipos[codigo];
    }

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Ciclo>> Preparados = [];
    private static readonly object Cerrojo = new();

    /// <summary>
    /// La cooperativa «cicloi6»: la de facturación electrónica de I4 con la emisión por <c>SIMULADO</c>, más el tipo <c>FRE</c> de factura
    /// desde remisiones con su propia resolución <c>FR</c> asociada al canal (el sembrado <c>FVR</c> no trae prefijo fiscal) y un comprador
    /// cuyo documento termina en 7 (el canal lo valida).
    /// </summary>
    private Task<Ciclo> CicloAsync()
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararAsync();
                Preparados[fx] = tarea;
            }
            return tarea;
        }
    }

    private async Task<Ciclo> PrepararAsync()
    {
        var e = await EscenarioDeFacturacionElectronica.PrepararAsync(fx, "cicloi6");
        using var http = fx.CreateClient();
        var t = e.Admin;
        var tipo = await EscenarioDeFacturacionElectronica.TipoFiscalAsync(http, t, "FRE", "Factura desde remisiones", "SalesInvoiceFromShipments", "FR");
        var resolucion = await EscenarioDeFacturacionElectronica.ResolucionAsync(http, t, "Invoice", null, "FR", 1, 100, e.Desde,
            InventarioE2E.HoyEnColombia.AddYears(1));
        await EscenarioDeFacturacionElectronica.AsociarAsync(http, t, resolucion, EscenarioDeFacturacionElectronica.Canal, e.Desde, "clave-tecnica-fr-ab12");
        var comprador = await EscenarioDeFacturacionElectronica.PersonaAsync(http, t, "Ciclo", 7);
        return new Ciclo(e, tipo, comprador);
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    /// <summary>La misma escritura tres veces con la misma clave: el mismo estado y el mismo cuerpo (SC-002). Devuelve la primera respuesta.</summary>
    private static async Task<JsonElement> TresVecesAsync(HttpClient http, string token, HttpMethod metodo, string url, object cuerpo,
        HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var clave = Guid.NewGuid();
        var cuerpos = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var resp = await InventarioE2E.MandarAsync(http, token, metodo, url, cuerpo, clave);
            var texto = await resp.Content.ReadAsStringAsync();
            resp.StatusCode.Should().Be(esperado, $"{metodo} {url} (vez {i + 1}): «{texto}»");
            cuerpos.Add(texto);
        }
        var ids = cuerpos.Select(c => Id(JsonDocument.Parse(c).RootElement)).Distinct().ToList();
        ids.Should().ContainSingle($"{url} repetido con la misma clave responde lo mismo");
        return JsonDocument.Parse(cuerpos[0]).RootElement.Clone();
    }

    private static Guid? Id(JsonElement cuerpo) =>
        cuerpo.ValueKind != JsonValueKind.Object ? null
        : cuerpo.TryGetProperty("documentPublicId", out var d) && d.ValueKind == JsonValueKind.String ? d.GetGuid()
        : cuerpo.TryGetProperty("publicId", out var p) && p.ValueKind == JsonValueKind.String ? p.GetGuid()
        : null;

    private static Guid Doc(JsonElement documento) => EscenarioDeFacturacionElectronica.Id(documento);

    private static Guid Linea(JsonElement documento, int i = 0) => documento.GetProperty("lines")[i].GetProperty("linePublicId").GetGuid();

    private static object Lineas(EscenarioDeInventario inv, params (string Producto, decimal Cantidad, Guid? Origen)[] lineas) => lineas.Select(l => new
    {
        productPublicId = inv.P(l.Producto).Id, unitPublicId = inv.P(l.Producto).Unidad, quantity = l.Cantidad, originLinePublicId = l.Origen,
    }).ToList();

    /// <summary>Física, reservada y disponible del producto en la bodega (<c>GET /api/inventory/stock/{id}</c>).</summary>
    private static async Task<(decimal Fisica, decimal Reservada, decimal Disponible)> ExistenciaAsync(HttpClient http, EscenarioDeInventario inv, string producto,
        string bodega)
    {
        var e = await inv.ExistenciaAsync(http, inv.Admin, producto);
        var fila = e.GetProperty("byWarehouse").EnumerateArray().Single(w => w.GetProperty("warehouse").GetProperty("publicId").GetGuid() == inv.Bodega(bodega));
        return (fila.GetProperty("physical").GetDecimal(), fila.GetProperty("reserved").GetDecimal(), fila.GetProperty("available").GetDecimal());
    }

    private static Task<int> KardexAsync(CentralIdentityApiFixture fx, EscenarioDeInventario inv, Guid documento) =>
        ContabilidadDeInventarioE2E.EnteroAsync(fx, inv,
            $"""SELECT COUNT(*) FROM dbo."INV_KardexEntries" k JOIN dbo."INV_Documents" d ON d."Id" = k."DocumentId" WHERE d."PublicId" = '{documento}'""",
            $"SELECT COUNT(*) FROM [dbo].[INV_KardexEntries] k JOIN [dbo].[INV_Documents] d ON d.[Id] = k.[DocumentId] WHERE d.[PublicId] = '{documento}'");

    /// <summary>Guarda el borrador sin pagos, lee lo que se paga y lo vuelve a guardar con esos pagos; devuelve el borrador final.</summary>
    private static async Task<JsonElement> ConPagosAsync(HttpClient http, string token, string ruta, object sinPagos, Func<decimal, object[]> pagos,
        Func<object[], object> conPagos)
    {
        var borrador = await InventarioE2E.BorradorAsync(http, token, ruta, sinPagos);
        var total = EscenarioDeFacturacionElectronica.AmountDue(borrador);
        return await InventarioE2E.ExitoAsync(http, token, HttpMethod.Put, $"{ruta}/{Doc(borrador)}", conPagos(pagos(total)));
    }

    // ------------------------------------------------------------------------------------------ pruebas --

    [Fact]
    public async Task Cotizacion_pedido_que_reserva_remision_y_factura_desde_remisiones_sin_volver_a_descargar()
    {
        var c = await CicloAsync();
        using var http = fx.CreateClient();
        var inv = c.Inv;
        var sup = c.Supervisor.Token;
        var hoy = InventarioE2E.HoyEnColombia;

        // P5 queda con 10 físicas en PV1.
        await inv.AjusteConfirmadoAsync(http, c.Admin, "AJN", "PV1", [new("P5", 490)]);
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 0m, 10m));

        // (1) Cotización con vigencia, repetida tres veces con la misma clave: una sola. Confirmada, no mueve nada.
        var cotizacion = await TresVecesAsync(http, sup, HttpMethod.Post, Cotizaciones, new
        {
            documentTypePublicId = c.Tipo("COT"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            validUntil = hoy.AddDays(10).ToString("yyyy-MM-dd"), lines = Lineas(inv, ("P5", 4m, null)), payments = Array.Empty<object>(),
        }, HttpStatusCode.Created);
        var idCotizacion = Doc(cotizacion);
        var cotizada = await TresVecesAsync(http, sup, HttpMethod.Post, $"{Cotizaciones}/{idCotizacion}/confirm", new { });
        cotizada.GetProperty("status").GetInt32().Should().Be(Confirmado);
        (await KardexAsync(fx, inv, idCotizacion)).Should().Be(0, "la cotización no mueve existencia");
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 0m, 10m));

        // (2) «Convertir en pedido» crea el borrador enlazado; confirmado reserva 4 de 10 (6 disponibles), sin kardex.
        var pedido = await TresVecesAsync(http, sup, HttpMethod.Post, $"{Cotizaciones}/{idCotizacion}/to-order",
            new { documentTypePublicId = c.Tipo("PED") }, HttpStatusCode.Created);
        var idPedido = Doc(pedido);
        pedido.GetProperty("class").GetInt32().Should().Be(24, "SalesOrder");
        pedido.GetProperty("lines").GetArrayLength().Should().Be(1);
        await TresVecesAsync(http, sup, HttpMethod.Post, $"{Pedidos}/{idPedido}/confirm", new { });
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 4m, 6m), "el pedido reserva: 4 reservadas y 6 disponibles");
        (await KardexAsync(fx, inv, idPedido)).Should().Be(0);
        var existencias = await InventarioE2E.InformeAsync(http, c.Admin, "stock", $"warehouse={inv.Bodega("PV1")}&product={inv.P("P5").Id}");
        var fila = existencias.Filas.Single();
        (existencias.Numero(fila, "Físico"), existencias.Numero(fila, "Reservado"), existencias.Numero(fila, "Disponible")).Should().Be((10m, 4m, 6m));

        // (3) Un pedido fechado atrás: su reserva vence (15 días) y la tarea la libera sola, una vez.
        var viejo = await InventarioE2E.BorradorAsync(http, sup, Pedidos, new
        {
            documentTypePublicId = c.Tipo("PED"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            operationDate = hoy.AddDays(-20).ToString("yyyy-MM-dd"), lines = Lineas(inv, ("P5", 2m, null)), payments = Array.Empty<object>(),
        });
        await InventarioE2E.ConfirmarAsync(http, sup, Pedidos, Doc(viejo));
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 6m, 4m));
        await fx.CorrerTareaAsync(c.E.Coop.TenantPublicId, "inventario.reservas");
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 4m, 6m), "la reserva vencida se libera sola");
        await fx.CorrerTareaAsync(c.E.Coop.TenantPublicId, "inventario.reservas");
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((10m, 4m, 6m), "correrla otra vez no cambia nada");

        // (4) La remisión del pedido descarga, consume la reserva y emite CostoDeVentaReconocido.
        var detallePedido = await EscenarioDeFacturacionElectronica.VentaAsync(http, sup, idPedido);
        var remision = await InventarioE2E.BorradorAsync(http, sup, Remisiones, new
        {
            documentTypePublicId = c.Tipo("REM"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            originPublicIds = new[] { idPedido }, lines = Lineas(inv, ("P5", 4m, Linea(detallePedido))), payments = Array.Empty<object>(),
        });
        var idRemision = Doc(remision);
        await TresVecesAsync(http, sup, HttpMethod.Post, $"{Remisiones}/{idRemision}/confirm", new { });
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((6m, 0m, 6m), "la remisión descarga y consume la reserva");
        (await KardexAsync(fx, inv, idRemision)).Should().Be(1);
        (await c.E.MensajesAsync(fx, "CostoDeVentaReconocido", idRemision)).Should().Be(1, "un solo efecto aunque se confirmó tres veces");

        // (5) La factura desde la remisión: sin kardex, VentaFacturada, número de su resolución y validada por el canal.
        var detalleRemision = await EscenarioDeFacturacionElectronica.VentaAsync(http, sup, idRemision);
        await EscenarioDeVentas.SesionAsync(http, sup, c.E.CajaElectronica);
        object Factura(object[] pagos) => new
        {
            documentTypePublicId = c.TipoFactura, warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            originPublicIds = new[] { idRemision }, lines = Lineas(inv, ("P5", 4m, Linea(detalleRemision))), payments = pagos,
        };
        var factura = await ConPagosAsync(http, sup, Facturas, Factura([]), total => [c.E.Ventas.Efectivo(total)], Factura);
        var idFactura = Doc(factura);
        factura.GetProperty("class").GetInt32().Should().Be(27, "SalesInvoiceFromShipments");
        factura.GetProperty("lines")[0].GetProperty("discounts").GetArrayLength().Should().Be(0, $"remisión: {detalleRemision} / factura: {factura}");
        var facturada = await TresVecesAsync(http, sup, HttpMethod.Post, $"{Facturas}/{idFactura}/confirm",
            new { expectedAmountDue = EscenarioDeFacturacionElectronica.AmountDue(factura) });
        facturada.GetProperty("status").GetInt32().Should().Be(Confirmado);
        (await KardexAsync(fx, inv, idFactura)).Should().Be(0, "no vuelve a descargar lo remisionado");
        (await ExistenciaAsync(http, inv, "P5", "PV1")).Should().Be((6m, 0m, 6m));
        (await c.E.MensajesAsync(fx, "VentaFacturada", idFactura)).Should().Be(1);
        var venta = await EscenarioDeFacturacionElectronica.VentaAsync(http, c.Admin, idFactura);
        venta.GetProperty("prefix").GetString().Should().Be("FR");
        var electronico = EscenarioDeFacturacionElectronica.Electronico(venta);
        await EscenarioDeFacturacionElectronica.EmitirAsync(http, c.Admin, electronico);
        EscenarioDeFacturacionElectronica.Estado(await EscenarioDeFacturacionElectronica.DetalleAsync(http, c.Admin, electronico)).Should().BeOneOf(2, 3);

        // Lo remisionado ya está facturado: otra factura sobre la misma remisión no pasa.
        object Otra(object[] pagos) => new
        {
            documentTypePublicId = c.TipoFactura, warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            originPublicIds = new[] { idRemision }, lines = Lineas(inv, ("P5", 1m, Linea(detalleRemision))), payments = pagos,
        };
        var repetida = await InventarioE2E.MandarAsync(http, sup, HttpMethod.Post, Facturas, Otra([]));
        ((int)repetida.StatusCode).Should().BeOneOf([201, 422], await repetida.Content.ReadAsStringAsync());
        if (repetida.StatusCode == HttpStatusCode.Created)
        {
            var borrador = await InventarioE2E.LeerAsync(repetida);
            var confirmar = await EscenarioDeFacturacionElectronica.PedirConfirmarAsync(http, sup, Facturas, Doc(borrador),
                EscenarioDeFacturacionElectronica.AmountDue(borrador));
            await InventarioE2E.FallaAsync(confirmar, "Inventory.Shipment.AlreadyInvoiced");
        }
        else
        {
            (await InventarioE2E.CodigoDeErrorAsync(repetida)).Should().Be("Inventory.Shipment.AlreadyInvoiced");
        }
    }

    [Fact]
    public async Task Nota_credito_sin_devolucion_no_mueve_existencia_y_la_nota_debito_a_credito_pide_aprobacion_y_ajusta_el_credito()
    {
        var c = await CicloAsync();
        using var http = fx.CreateClient();
        var inv = c.Inv;
        var sup = c.Supervisor;

        // (1) Factura directa de 2 P1 y su nota crédito sin devolución: la existencia no cambia y la nota no tiene kardex.
        var factura = await c.E.FacturaConfirmadaAsync(http, sup, c.Comprador, "FE", null, ("P1", 2m));
        var idFactura = Doc(factura);
        var fisicaAntes = (await ExistenciaAsync(http, inv, "P1", "PV1")).Fisica;
        var detalle = await EscenarioDeFacturacionElectronica.VentaAsync(http, sup.Token, idFactura);
        object Nota(object[] reintegros) => new
        {
            originDocumentPublicId = idFactura, documentTypePublicId = c.E.Tipo("NC"), reason = "Rebaja por empaque averiado", totalVoid = false,
            withReturn = false, correctionConceptCode = "3", lines = new[] { new { originLinePublicId = Linea(detalle), amount = 1_000m } },
            refunds = reintegros,
        };
        var nota = await ConPagosAsync(http, sup.Token, NotasCredito, Nota([]), total => [c.E.Ventas.Efectivo(total)], Nota);
        var notaConfirmada = await TresVecesAsync(http, sup.Token, HttpMethod.Post, $"{NotasCredito}/{Doc(nota)}/confirm",
            new { expectedAmountDue = EscenarioDeFacturacionElectronica.AmountDue(nota) });
        notaConfirmada.GetProperty("status").GetInt32().Should().Be(Confirmado);
        (await KardexAsync(fx, inv, Doc(nota))).Should().Be(0, "sin devolución no hay movimiento");
        (await ExistenciaAsync(http, inv, "P1", "PV1")).Fisica.Should().Be(fisicaAntes);

        // (2) Una venta a crédito provisional al asociado (100.000): cajero.1 la confirma, queda en aprobación y el supervisor la aprueba.
        var cajero = c.E.Ventas.Cajero1;
        var asociado = await ContabilidadDeVentasE2E.AsociadoAsync(http, c.Admin, "Debora", "71000207");
        object Credito(decimal valor) => new
        {
            paymentMeansPublicId = c.E.Ventas.Medio("CREDASOC"), amount = valor, credit = new { installments = 3, termDays = 90, periodicityDays = 30 },
        };
        var aCredito = await InventarioE2E.BorradorAsync(http, cajero.Token, Facturas, new
        {
            documentTypePublicId = c.E.Tipo("FE"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = asociado,
            lines = Lineas(inv, ("P7", 1m, null)), payments = new[] { Credito(100_000m) },
        });
        var idVenta = Doc(aCredito);
        var enAprobacion = await InventarioE2E.ExitoAsync(http, cajero.Token, HttpMethod.Post, $"{Facturas}/{idVenta}/confirm", new { expectedAmountDue = 100_000m });
        enAprobacion.GetProperty("status").GetInt32().Should().Be(EnAprobacion, "el crédito provisional siempre se aprueba");
        await EscenarioDeVentas.AprobadaAsync(await InventarioE2E.DecidirAsync(http, c.Admin, sup.Token,
            enAprobacion.GetProperty("approval").GetProperty("requestPublicId").GetGuid(), aprobar: true));
        var ventaACredito = await EscenarioDeFacturacionElectronica.VentaAsync(http, c.Admin, idVenta);
        ventaACredito.GetProperty("status").GetInt32().Should().Be(Confirmado);
        await EscenarioDeFacturacionElectronica.EmitirAsync(http, c.Admin, EscenarioDeFacturacionElectronica.Electronico(ventaACredito));

        // (3) La nota débito sobre esa venta, cobrada a crédito: concepto de corrección, sin kardex, aprobación ProvisionalCredit,
        //     NotaDebitoEmitida y AjusteDeVentaACredito hacia Cartera, pendiente.
        object Debito(object[] pagos) => new
        {
            documentTypePublicId = c.Tipo("NDV"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = asociado,
            originPublicIds = new[] { idVenta }, correctionConceptCode = "1", lines = new[]
            {
                new { productPublicId = inv.P("P7").Id, unitPublicId = inv.P("P7").Unidad, quantity = 1m },
            },
            payments = pagos,
        };
        var debito = await ConPagosAsync(http, cajero.Token, NotasDebito, Debito([]), total => [Credito(total)], Debito);
        var idDebito = Doc(debito);
        debito.GetProperty("class").GetInt32().Should().Be(33, "DebitNote");
        var cargo = EscenarioDeFacturacionElectronica.AmountDue(debito);
        cargo.Should().BePositive();
        var debitoEnAprobacion = await TresVecesAsync(http, cajero.Token, HttpMethod.Post, $"{NotasDebito}/{idDebito}/confirm",
            new { expectedAmountDue = cargo });
        debitoEnAprobacion.GetProperty("status").GetInt32().Should().Be(EnAprobacion, "la nota débito a crédito pide la aprobación del crédito provisional");
        await EscenarioDeVentas.AprobadaAsync(await InventarioE2E.DecidirAsync(http, c.Admin, sup.Token,
            debitoEnAprobacion.GetProperty("approval").GetProperty("requestPublicId").GetGuid(), aprobar: true));
        var notaDebito = await EscenarioDeFacturacionElectronica.VentaAsync(http, c.Admin, idDebito);
        notaDebito.GetProperty("status").GetInt32().Should().Be(Confirmado);
        notaDebito.GetProperty("correctionConceptCode").GetString().Should().Be("1");
        (await KardexAsync(fx, inv, idDebito)).Should().Be(0, "la nota débito no mueve existencia");
        (await c.E.MensajesAsync(fx, "NotaDebitoEmitida", idDebito)).Should().Be(1);
        (await c.E.MensajesAsync(fx, "AjusteDeVentaACredito", idDebito, "DebitNote")).Should().Be(1, "ajusta el crédito hacia Cartera");
        var credito = await InventarioE2E.GetAsync(http, c.Admin, $"/api/inventory/sales/documents/{idDebito}/credit");
        credito.GetProperty("payments").EnumerateArray().Single().GetProperty("pendingValidation").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task La_promocion_tres_por_dos_sale_como_descuento_de_linea_y_baja_la_base_del_IVA()
    {
        var c = await CicloAsync();
        using var http = fx.CreateClient();
        var inv = c.Inv;
        var sup = c.Supervisor;
        var hoy = InventarioE2E.HoyEnColombia;

        var alta = await TresVecesAsync(http, c.Admin, HttpMethod.Post, "/api/inventory/promotions", new
        {
            code = "TRESXDOS", name = "Lleve 3 pague 2 en P2", kind = "BuyNPayM", validFrom = c.E.Desde.ToString("yyyy-MM-dd"),
            validTo = hoy.AddMonths(1).ToString("yyyy-MM-dd"), reason = "Promoción del ensayo", buyQuantity = 3m, payQuantity = 2m,
            scopes = new[] { new { productPublicId = inv.P("P2").Id } },
        }, HttpStatusCode.Created);
        var promocion = alta.GetProperty("promotionPublicId").GetGuid();
        (await InventarioE2E.GetAsync(http, c.Admin, "/api/inventory/promotions?active=true")).EnumerateArray()
            .Select(p => p.GetProperty("promotionPublicId").GetGuid()).Should().ContainSingle(p => p == promocion, "una sola aunque el alta se repitió");

        // Tres P2 a 5.950 con IVA: se paga como dos (11.900); el descuento es una unidad sin IVA (5.000) y la base del IVA baja a 10.000.
        await EscenarioDeVentas.SesionAsync(http, sup.Token, c.E.CajaElectronica);
        object Factura(object[] pagos) => new
        {
            documentTypePublicId = c.E.Tipo("FE"), warehousePublicId = inv.Bodega("PV1"), counterpartyPersonPublicId = c.Comprador,
            lines = Lineas(inv, ("P2", 3m, null)), payments = pagos,
        };
        var borrador = await ConPagosAsync(http, sup.Token, Facturas, Factura([]), total => [c.E.Ventas.Efectivo(total)], Factura);
        EscenarioDeFacturacionElectronica.AmountDue(borrador).Should().Be(11_900m);
        var linea = borrador.GetProperty("lines")[0];
        var descuento = linea.GetProperty("discounts").EnumerateArray().Single();
        descuento.GetProperty("source").GetInt32().Should().Be(2, "DiscountSource.Promotion");
        descuento.GetProperty("promotionPublicId").GetGuid().Should().Be(promocion);
        descuento.GetProperty("amount").GetDecimal().Should().Be(5_000m);
        linea.GetProperty("unitPrice").GetDecimal().Should().BeGreaterThan(0m, "nunca una línea a precio cero");
        borrador.GetProperty("totals").GetProperty("taxTotal").GetDecimal().Should().Be(1_900m, "el IVA sobre la base ya rebajada (10.000)");

        var confirmada = await InventarioE2E.ExitoAsync(http, sup.Token, HttpMethod.Post, $"{Facturas}/{Doc(borrador)}/confirm",
            new { expectedAmountDue = 11_900m });
        confirmada.GetProperty("status").GetInt32().Should().Be(Confirmado);
        var venta = await EscenarioDeFacturacionElectronica.VentaAsync(http, c.Admin, Doc(borrador));
        venta.GetProperty("lines")[0].GetProperty("discounts")[0].GetProperty("promotionName").GetString().Should().Be("Lleve 3 pague 2 en P2");
        var iva = venta.GetProperty("lines")[0].GetProperty("taxes").EnumerateArray().Single();
        (iva.GetProperty("base").GetDecimal(), iva.GetProperty("amount").GetDecimal()).Should().Be((10_000m, 1_900m), "el descuento baja la base gravable");

        // Usada en un documento confirmado, sólo cambian nombre, fin y activo.
        var cambio = await InventarioE2E.MandarAsync(http, c.Admin, HttpMethod.Put, $"/api/inventory/promotions/{promocion}", new
        {
            code = "TRESXDOS", name = "Otro nombre", kind = "BuyNPayM", validFrom = c.E.Desde.ToString("yyyy-MM-dd"),
            validTo = hoy.AddMonths(1).ToString("yyyy-MM-dd"), reason = "Cambio del ensayo", buyQuantity = 4m, payQuantity = 3m,
            scopes = new[] { new { productPublicId = inv.P("P2").Id } },
        });
        await InventarioE2E.FallaAsync(cambio, "Inventory.Promotion.InUse");
    }

    [Fact]
    public async Task La_remision_vieja_sin_facturar_levanta_su_alerta_y_el_cierre_exige_aceptarla_con_permiso_y_motivo()
    {
        // Sin las ventas de I3: su existencia de hoy en PV1 haría retroactiva una entrada fechada en el mes anterior (costo por cooperativa).
        var inv = await EscenarioDeInventario.PrepararAsync(fx, "remisioni6");
        var esc = inv;
        using var http = fx.CreateClient();
        var t = inv.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        var mesAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mesDelCorte = mesAnterior.AddMonths(-1);
        const string Periodos = "/api/inventory/periods";

        await ContabilidadDeInventarioE2E.ParametroAsync(http, t, "Ventas.RemisionDiasMaximosSinFacturar", "1", inv.Corte.AddDays(1));
        var lista = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/inventory/price-lists", new
        {
            code = "GENERAL", name = "Lista general", includesTaxes = true, scope = new { }, validFrom = inv.Corte.AddDays(1).ToString("yyyy-MM-dd"),
            reason = "Lista del ensayo",
        })).GetProperty("priceListPublicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, $"/api/inventory/price-lists/{lista}/items", new
        {
            items = new[] { new { productPublicId = inv.P("P4").Id, unitPublicId = inv.P("P4").Unidad, price = 3_570m } }, reason = "Precios del ensayo",
        });
        await inv.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P4", 5, 1_150m)], fecha: mesAnterior.AddDays(1));
        var comprador = await EscenarioDeFacturacionElectronica.PersonaAsync(http, t, "Remision", 7);
        var remision = await InventarioE2E.BorradorAsync(http, t, Remisiones, new
        {
            documentTypePublicId = inv.Tipos["REM"], warehousePublicId = inv.Bodega("PRIN"), counterpartyPersonPublicId = comprador,
            operationDate = mesAnterior.AddDays(2).ToString("yyyy-MM-dd"), payments = Array.Empty<object>(),
            lines = Lineas(inv, ("P4", 2m, null)),
        });
        var idRemision = Doc(remision);
        await InventarioE2E.ConfirmarAsync(http, t, Remisiones, idRemision);

        // (1) La alerta, una sola vez aunque la tarea corra dos.
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, "inventario.remisiones");
        await fx.CorrerTareaAsync(esc.Coop.TenantPublicId, "inventario.remisiones");
        var alertas = (await InventarioE2E.GetAsync(http, t, "/api/inventory/alerts?typeCode=Inventario.RemisionSinFacturar&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();
        alertas.Should().ContainSingle("una alerta por remisión (DedupKey)");

        // (2) El mes del corte cierra; el anterior lista la remisión y no cierra sin aceptarla.
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Periodos}/{mesDelCorte.Year}/{mesDelCorte.Month}/close", new { acknowledgeWarnings = true });
        var previa = await InventarioE2E.GetAsync(http, t, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close");
        var sinFacturar = previa.GetProperty("unbilledShipments").EnumerateArray().ToList();
        sinFacturar.Should().ContainSingle().Which.GetProperty("documentPublicId").GetGuid().Should().Be(idRemision);
        previa.GetProperty("unbilledTotal").GetDecimal().Should().BePositive();

        var sinAceptar = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close",
            new { acknowledgeWarnings = true });
        await InventarioE2E.FallaAsync(sinAceptar, "Inventory.Period.UnbilledShipmentsNotAccepted");

        // Quien cierra sin el permiso de aceptar remisiones: 422 propio (el recurso ya es visible).
        await InventarioE2E.RolAsync(http, esc.Coop, "CIERREI6", null, extra: ["Inventory.Periods.View", "Inventory.Periods.Close"]);
        var (cierra, _) = await InventarioE2E.UsuarioAsync(fx, http, esc.Coop, $"cierre.{esc.Coop.TenantPublicId:N}@coop.ventas.test", "CIERREI6");
        var sinPermiso = await InventarioE2E.MandarAsync(http, cierra, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close",
            new { acknowledgeWarnings = true, acceptUnbilledShipments = true, reason = "No me toca" });
        await InventarioE2E.FallaAsync(sinPermiso, "Inventory.Period.AcceptUnbilledNotAllowed");

        // (3) Aceptada con motivo y permiso, cierra y la lista de períodos dice lo aceptado.
        await TresVecesAsync(http, t, HttpMethod.Post, $"{Periodos}/{mesAnterior.Year}/{mesAnterior.Month}/close",
            new { acknowledgeWarnings = true, acceptUnbilledShipments = true, reason = "Se factura en el mes siguiente" });
        var periodos = await InventarioE2E.GetAsync(http, t, $"{Periodos}?year={mesAnterior.Year}");
        var cerrado = periodos.GetProperty("periods").EnumerateArray().Single(p => p.GetProperty("month").GetInt32() == mesAnterior.Month);
        var aceptado = cerrado.GetProperty("unbilledShipmentsAccepted");
        aceptado.GetProperty("count").GetInt32().Should().Be(1);
        aceptado.GetProperty("reason").GetString().Should().Be("Se factura en el mes siguiente");
    }
}
