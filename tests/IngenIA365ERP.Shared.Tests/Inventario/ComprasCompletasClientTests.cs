using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// Feature 012, I5 (T810, T847; contracts/api.md §9.3, §14.8, §14.9): el parcial de compras completas de <c>ComprasClient</c> —solicitudes,
/// órdenes (PDF, envío al proveedor, cierre del saldo), cruce a tres vías, costos adicionales y la emisión RADIAN desde el ERP— y el impacto
/// en costos de un borrador en <c>InventarioClient</c>. Toda escritura lleva la clave de operación; las lecturas y el impacto (una consulta
/// aunque sea POST) no; ninguna pone <c>Authorization</c> a mano (<c>ElTokenDeSesionLoPoneElHandler</c>). (nuevo)
/// </summary>
public class ComprasCompletasClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly ComprasClient _compras;
    private readonly InventarioClient _inventario;

    public ComprasCompletasClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _compras = new ComprasClient(http, auth);
        _inventario = new InventarioClient(http, auth);
    }

    private static object PaginaVacia() => new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0L };

    [Fact]
    public async Task Las_listas_de_solicitudes_ordenes_y_costos_adicionales_van_por_su_ruta_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, PaginaVacia());
        var proveedor = Guid.NewGuid();

        (await _compras.ListarSolicitudesAsync(new FiltroDeCompras())).IsSuccess.Should().BeTrue();
        (await _compras.ListarOrdenesAsync(new FiltroDeCompras { SupplierPersonPublicId = proveedor, Status = 2 })).IsSuccess.Should().BeTrue();
        (await _compras.ListarCostosAdicionalesAsync(new FiltroDeCompras())).IsSuccess.Should().BeTrue();

        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            "/api/inventory/purchases/requests?page=1&pageSize=20",
            $"/api/inventory/purchases/orders?supplierPersonPublicId={proveedor}&status=2&page=1&pageSize=20",
            "/api/inventory/purchases/landed-costs?page=1&pageSize=20");
        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "GET" && v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task La_orden_se_guarda_con_su_entrega_sus_condiciones_y_la_linea_de_la_solicitud()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { publicId = Guid.NewGuid() });
        var linea = Guid.NewGuid();
        var pedido = new BorradorDeCompraRequest(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), null, null, null, null,
            [new LineaDeCompraRequest(null, Guid.NewGuid(), Guid.NewGuid(), 100m, 1000m, RequestLinePublicId: linea)],
            ExpectedDate: new DateOnly(2026, 10, 5), PaymentTerms: "30 días");

        var r = await _compras.GuardarAsync(ComprasClient.Rutas.Ordenes, null, pedido, new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/inventory/purchases/orders");
        vista.Cuerpo.Should().Contain("\"expectedDate\":\"2026-10-05\"").And.Contain("\"paymentTerms\":\"30 d")
            .And.Contain($"\"requestLinePublicId\":\"{linea}\"");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task El_detalle_de_la_orden_trae_la_entrega_y_lo_pendiente_por_recibir()
    {
        var linea = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            document = new { publicId = Guid.NewGuid(), @class = 2, status = 2 },
            supplierInvoice = (object?)null,
            radianEvents = Array.Empty<object>(),
            lineBalances = Array.Empty<object>(),
            operationMunicipalityDaneCode = (string?)null,
            plan = new { neededBy = (string?)null, expectedDate = "2026-10-05", paymentTerms = "30 días", balanceClosedAt = (DateTime?)null },
            pendingLines = new[] { new { linePublicId = linea, lineNumber = 1, quantity = 100m, consumed = 60m, pendingToOrder = (decimal?)null, pendingToReceive = 40m } },
        });

        var r = await _compras.ObtenerOrdenAsync(Guid.NewGuid());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Plan!.ExpectedDate.Should().Be(new DateOnly(2026, 10, 5));
        r.Value.PendingLines!.Single().PendingToReceive.Should().Be(40m);
        _servidor.Vistas.Single().Ruta.Should().StartWith("/api/inventory/purchases/orders/");
    }

    [Fact]
    public async Task El_PDF_de_la_orden_es_una_descarga_sin_clave()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) { Headers = { ContentType = new("application/pdf") } },
        };
        var id = Guid.NewGuid();

        var r = await _compras.DescargarOrdenEnPdfAsync(id);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Contenido.Should().HaveCount(4);
        r.Value.TipoContenido.Should().Be("application/pdf");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/purchases/orders/{id}/pdf");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Enviar_la_orden_y_cerrar_su_saldo_son_escrituras_con_clave()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        (await _compras.EnviarOrdenAsync(id, "compras@proveedor.test", new ClaveDeOperacion())).IsSuccess.Should().BeTrue();
        (await _compras.CerrarSaldoDeOrdenAsync(id, "El proveedor no tiene más", new ClaveDeOperacion())).IsSuccess.Should().BeTrue();

        _servidor.Vistas[0].Ruta.Should().Be($"/api/inventory/purchases/orders/{id}/send");
        _servidor.Vistas[0].Cuerpo.Should().Contain("\"email\":\"compras@proveedor.test\"");
        _servidor.Vistas[1].Ruta.Should().Be($"/api/inventory/purchases/orders/{id}/close-balance");
        _servidor.Vistas[1].Cuerpo.Should().Contain("\"reason\":\"El proveedor no tiene m");
        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "POST" && !string.IsNullOrWhiteSpace(v.Clave) && v.Authorization == null);
    }

    [Fact]
    public async Task El_cruce_se_lee_por_estado_y_por_factura_con_la_tolerancia_usada()
    {
        var linea = new
        {
            publicId = Guid.NewGuid(),
            supplierInvoice = new { publicId = Guid.NewGuid(), displayNumber = "FP-7", supplierNumber = "FV100" },
            lineNumber = 1,
            product = new { publicId = Guid.NewGuid(), code = "P1", name = "Arroz" },
            ordered = 100m, received = 98m, invoiced = 100m,
            orderPrice = (decimal?)1000m, receiptPrice = (decimal?)1000m, invoicePrice = (decimal?)1020m,
            quantityVariance = 2m, priceVariance = (decimal?)0.02m,
            reasons = new[] { "Quantity", "Price" },
            status = 1,
            approvalRequestPublicId = Guid.NewGuid(),
            exceedsTolerance = true,
            tolerance = "{\"precioPorcentaje\":0.01}",
        };
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/match", StringComparison.Ordinal)
            ? ServidorDeIntegracion.Json(HttpStatusCode.OK, new[] { linea })
            : ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = new[] { linea }, page = 1, pageSize = 20, totalCount = 1L });
        var factura = Guid.NewGuid();

        var lista = await _compras.ListarCruceAsync(EstadosDelCruce.Retenida, null);
        var deFactura = await _compras.CruceDeFacturaAsync(factura);

        lista.Value!.Items.Single().Reasons.Should().Equal("Quantity", "Price");
        deFactura.Value!.Single().ExceedsTolerance.Should().BeTrue();
        deFactura.Value!.Single().Tolerance.Should().Contain("precioPorcentaje");
        _servidor.Vistas[0].Ruta.Should().Be("/api/inventory/purchases/matches?status=1&page=1&pageSize=20");
        _servidor.Vistas[1].Ruta.Should().Be($"/api/inventory/purchases/supplier-invoices/{factura}/match");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task Los_costos_adicionales_mandan_la_factura_del_flete_las_recepciones_y_el_metodo_y_leen_el_reparto()
    {
        var factura = Guid.NewGuid();
        var recepcion = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            publicId = Guid.NewGuid(),
            landedCost = new
            {
                supplierInvoice = new { publicId = factura, displayNumber = "FP-9" },
                method = 1,
                amount = 100000m,
                available = 0m,
                allocations = new[]
                {
                    new
                    {
                        receiptLine = new { receiptPublicId = recepcion, receiptDisplayNumber = "RC-1", linePublicId = Guid.NewGuid(), lineNumber = 1 },
                        product = new { publicId = Guid.NewGuid(), code = "P1", name = "Arroz" },
                        basis = 600000m, allocated = 60000m, roundingResidue = 0m, toInventory = 60000m, toCostOfSales = 0m,
                    },
                },
                roundingResidue = 0m,
            },
        });
        var pedido = new BorradorDeCompraRequest(Guid.NewGuid(), null, null, null, null, null, null, null, [],
            SupplierInvoicePublicId: factura, ReceiptPublicIds: [recepcion], Amount: 100000m, Method: MetodosDeReparto.Valor);

        var r = await _compras.GuardarAsync(ComprasClient.Rutas.CostosAdicionales, null, pedido, new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.LandedCost!.Allocations.Single().Allocated.Should().Be(60000m);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/purchases/landed-costs");
        vista.Cuerpo.Should().Contain($"\"supplierInvoicePublicId\":\"{factura}\"").And.Contain($"\"receiptPublicIds\":[\"{recepcion}\"]")
            .And.Contain("\"method\":\"Value\"").And.Contain("\"amount\":100000");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Emitir_los_eventos_RADIAN_lleva_clave_y_lee_el_202()
    {
        var documento = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Accepted, new
        {
            events = new[] { new { eventCode = 30, electronicDocumentPublicId = documento, status = 1 }, new { eventCode = 32, electronicDocumentPublicId = Guid.NewGuid(), status = 1 } },
        });
        var factura = Guid.NewGuid();

        var r = await _compras.EmitirEventosRadianAsync(factura, [EventosRadian.Acuse030, EventosRadian.ReciboDelBien032], new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Events.Should().HaveCount(2);
        r.Value.Events[0].ElectronicDocumentPublicId.Should().Be(documento);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/purchases/supplier-invoices/{factura}/radian-events/emit");
        vista.Cuerpo.Should().Contain("\"eventCodes\":[30,32]");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task El_impacto_en_costos_es_una_consulta_sin_clave()
    {
        var venta = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            retroactive = true,
            affected = new[]
            {
                new { documentPublicId = venta, displayNumber = "FV-3", product = new { publicId = Guid.NewGuid(), code = "P1", name = "Arroz" }, inventoryAmount = 300m, soldAmount = 200m },
            },
            total = 500m,
        });
        var id = Guid.NewGuid();

        var r = await _inventario.ImpactoEnCostosAsync(id);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Retroactive.Should().BeTrue();
        r.Value.Affected.Single().SoldAmount.Should().Be(200m);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be($"/api/inventory/documents/{id}/cost-impact");
        vista.Clave.Should().BeNull("es una consulta: no guarda nada (api.md §2.3)");
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public void Los_textos_del_cruce_y_del_reparto_son_legibles()
    {
        EstadosDelCruce.Texto(EstadosDelCruce.Retenida).Should().Be("Retenida");
        EstadosDelCruce.Texto(null).Should().Be("Dentro de la tolerancia");
        EstadosDelCruce.Razon("Quantity").Should().Be("Cantidad");
        EstadosDelCruce.Razon("Price").Should().Be("Precio");
        MetodosDeReparto.Texto(3).Should().Be("Por peso");
        MetodosDeReparto.Todos.Select(m => m.Valor).Should().Equal("Value", "Quantity", "Weight", "Volume", "Manual");
    }
}
