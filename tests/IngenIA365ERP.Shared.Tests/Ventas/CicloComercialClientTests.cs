using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Ventas;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T893 (feature 012, I6; contracts/api.md §18.4, §19.4): el cliente de Ventas suma el ciclo comercial —cotizaciones, pedidos, remisiones y
/// notas débito sobre las cinco operaciones del ciclo común, «Convertir en pedido» y la factura desde pedido o remisiones— y las
/// promociones. Toda escritura lleva su clave y ninguna pone <c>Authorization</c> a mano (<c>ElTokenDeSesionLoPoneElHandler</c>); la clase
/// de la promoción viaja por nombre; el detalle trae la promoción aplicada, la vigencia, los orígenes y lo pendiente. (nuevo)
/// </summary>
public class CicloComercialClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly VentasClient _cliente;

    public CicloComercialClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new VentasClient(http, auth);
    }

    [Theory]
    [InlineData(VentasClient.Rutas.Cotizaciones, "/api/inventory/sales/quotes")]
    [InlineData(VentasClient.Rutas.Pedidos, "/api/inventory/sales/orders")]
    [InlineData(VentasClient.Rutas.Remisiones, "/api/inventory/sales/shipments")]
    [InlineData(VentasClient.Rutas.NotasDebito, "/api/inventory/sales/debit-notes")]
    [InlineData(VentasClient.Rutas.Facturas, "/api/inventory/sales/invoices")]
    public async Task Cada_documento_del_ciclo_se_guarda_por_su_ruta_con_su_clave(string ruta, string esperada)
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, DocumentoJson());
        var borrador = new BorradorDeVentaRequest(Guid.NewGuid(), Guid.NewGuid(), [], []);

        var nuevo = await _cliente.GuardarBorradorAsync(ruta, null, borrador, new ClaveDeOperacion());
        var id = Guid.NewGuid();
        var cambiado = await _cliente.GuardarBorradorAsync(ruta, id, borrador, new ClaveDeOperacion());

        nuevo.IsSuccess.Should().BeTrue(nuevo.ErrorMessage);
        cambiado.IsSuccess.Should().BeTrue(cambiado.ErrorMessage);
        (_servidor.Vistas[0].Metodo, _servidor.Vistas[0].Ruta).Should().Be(("POST", esperada));
        (_servidor.Vistas[1].Metodo, _servidor.Vistas[1].Ruta).Should().Be(("PUT", $"{esperada}/{id}"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave != null && v.Authorization == null);
    }

    [Fact]
    public async Task La_cotizacion_manda_su_vigencia_y_la_nota_debito_su_concepto_y_su_factura()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, DocumentoJson());
        var factura = Guid.NewGuid();

        await _cliente.GuardarBorradorAsync(VentasClient.Rutas.Cotizaciones, null,
            new BorradorDeVentaRequest(Guid.NewGuid(), Guid.NewGuid(), [], [], ValidUntil: new DateOnly(2026, 10, 15)), new ClaveDeOperacion());
        await _cliente.GuardarBorradorAsync(VentasClient.Rutas.NotasDebito, null,
            new BorradorDeVentaRequest(Guid.NewGuid(), Guid.NewGuid(), [], [], OriginPublicIds: [factura], CorrectionConceptCode: "1"), new ClaveDeOperacion());

        _servidor.Vistas[0].Cuerpo.Should().Contain("\"validUntil\":\"2026-10-15\"");
        _servidor.Vistas[1].Cuerpo.Should().Contain("\"correctionConceptCode\":\"1\"").And.Contain(factura.ToString());
    }

    [Fact]
    public async Task La_factura_desde_remisiones_manda_sus_origenes_y_la_linea_origen()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, DocumentoJson());
        var remision = Guid.NewGuid();
        var lineaOrigen = Guid.NewGuid();

        var r = await _cliente.GuardarFacturaAsync(null, new BorradorDeVentaRequest(Guid.NewGuid(), Guid.NewGuid(),
            [new LineaDeVentaRequest(Guid.NewGuid(), Guid.NewGuid(), 2m, OriginLinePublicId: lineaOrigen)], [], OriginPublicIds: [remision]), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/sales/invoices");
        vista.Cuerpo.Should().Contain($"\"originPublicIds\":[\"{remision}\"]").And.Contain($"\"originLinePublicId\":\"{lineaOrigen}\"");
    }

    [Fact]
    public async Task Convertir_en_pedido_va_a_to_order_con_su_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, DocumentoJson(clase: 24));
        var cotizacion = Guid.NewGuid();

        var r = await _cliente.ConvertirEnPedidoAsync(cotizacion, null, new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Class.Should().Be(24);
        var vista = _servidor.Vistas.Single();
        (vista.Metodo, vista.Ruta).Should().Be(("POST", $"/api/inventory/sales/quotes/{cotizacion}/to-order"));
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Confirmar_y_anular_un_pedido_van_por_su_ruta()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { documentPublicId = Guid.NewGuid(), status = 2, displayNumber = "PED1", warnings = Array.Empty<object>() });
        var id = Guid.NewGuid();

        await _cliente.ConfirmarAsync(VentasClient.Rutas.Pedidos, id, null, null, new ClaveDeOperacion());

        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/sales/orders/{id}/confirm");
    }

    [Fact]
    public async Task El_detalle_trae_la_promocion_aplicada_la_vigencia_los_origenes_y_lo_pendiente()
    {
        var promocion = Guid.NewGuid();
        var origen = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, DocumentoJson(clase: 24, promocion: promocion, origen: origen));

        var r = await _cliente.ObtenerDocumentoAsync(Guid.NewGuid());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var d = r.Value!;
        d.ValidUntil.Should().Be(new DateOnly(2026, 10, 5));
        d.Origins!.Single().PublicId.Should().Be(origen);
        var linea = d.Lines.Single();
        linea.Pending.Should().Be(6m);
        var descuento = linea.Discounts.Single();
        (descuento.EsPromocion, descuento.PromotionPublicId, descuento.PromotionName).Should().Be((true, promocion, "Lleve 3 pague 2"));
    }

    // ---------------------------------------------------------------------------------------- promociones --

    [Fact]
    public async Task Las_promociones_se_consultan_sin_clave_por_vigencia()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new[] { PromocionJson() });

        var r = await _cliente.ListarPromocionesAsync(new DateOnly(2026, 10, 1), activas: true);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Single().Kind.Should().Be(3);
        r.Value.Single().Scopes.Single().Kind.Should().Be(1);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/promotions?asOf=2026-10-01&active=true");
        vista.Clave.Should().BeNull();
    }

    [Fact]
    public async Task Crear_una_promocion_manda_su_clase_por_nombre_y_su_motivo()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, PromocionJson());
        var producto = Guid.NewGuid();

        var r = await _cliente.CrearPromocionAsync(new PromocionRequest("L3P2", "Lleve 3 pague 2", TextosDeVentas.NombreDeClaseDePromocion(3),
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "Temporada", BuyQuantity: 3m, PayQuantity: 2m,
            Scopes: [new AmbitoDePromocionRequest(ProductPublicId: producto)]), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        (vista.Metodo, vista.Ruta).Should().Be(("POST", "/api/inventory/promotions"));
        vista.Cuerpo.Should().Contain("\"kind\":\"BuyNPayM\"").And.Contain("\"reason\":\"Temporada\"").And.Contain(producto.ToString());
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Una_promocion_en_uso_conserva_su_data_al_editar()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Inventory.Promotion.InUse", message = "La promoción ya se aplicó.", data = new { fields = new[] { "kind" } },
        });
        var id = Guid.NewGuid();

        var r = await _cliente.EditarPromocionAsync(id, new EdicionDePromocionRequest("Otra", new DateOnly(2026, 11, 30), true, "Cambio", Kind: "Percent"),
            new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Inventory.Promotion.InUse");
        _servidor.Vistas.Single().Metodo.Should().Be("PUT");
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/promotions/{id}");
    }

    // ------------------------------------------------------------------------------------ ayudantes --

    private static object Ref(string code) => new { publicId = Guid.NewGuid(), code, name = code };

    private static object PromocionJson() => new
    {
        promotionPublicId = Guid.NewGuid(), code = "L3P2", name = "Lleve 3 pague 2", kind = 3, percent = (decimal?)null, amount = (decimal?)null,
        buyQuantity = 3m, payQuantity = 2m, bundlePrice = (decimal?)null, validFrom = "2026-10-01", validTo = "2026-10-31", cumulative = false,
        isActive = true, notes = (string?)null, inUse = false,
        scopes = new[] { new { kind = 1, productPublicId = Guid.NewGuid(), productCode = "ARZ-1", categoryPublicId = (Guid?)null, categoryCode = (string?)null,
            segment = (string?)null, salesChannelPublicId = (Guid?)null, salesChannelCode = (string?)null, requiredQuantity = (decimal?)null } },
        tiers = Array.Empty<object>(),
    };

    private static object DocumentoJson(int clase = 23, Guid? promocion = null, Guid? origen = null) => new
    {
        documentPublicId = Guid.NewGuid(), @class = clase, documentType = Ref("COT"), prefix = "COT", number = (long?)null, status = 0,
        operationDate = "2026-09-29", confirmedAt = (DateTime?)null, warehouse = Ref("B1"), branch = Ref("S1"), pointOfSale = (object?)null,
        cashRegister = (object?)null, cashSessionPublicId = (Guid?)null, salesChannel = (object?)null, postingMode = (int?)null,
        counterparty = new { personPublicId = Guid.NewGuid(), isFinalConsumer = false, name = "Ana", idType = "CC", idNumber = "1", address = (string?)null, email = (string?)null },
        salesperson = (object?)null,
        lines = new[]
        {
            new
            {
                linePublicId = Guid.NewGuid(), lineNumber = 1, product = Ref("ARZ-1"), unit = new { publicId = Guid.NewGuid(), code = "UND" }, factor = 1m,
                quantity = 10m, quantityBase = 10m, roundingQuantity = 0m, listPrice = 2000m, unitPrice = 2000m, includesTaxes = false, priceList = (object?)null,
                discounts = promocion is null
                    ? Array.Empty<object>()
                    : new object[] { new { source = 2, percent = (decimal?)null, amount = 6000m, fromDocumentDiscount = false, isPriceOverride = false, approval = (object?)null,
                        promotionPublicId = promocion, promotionName = "Lleve 3 pague 2" } },
                taxes = Array.Empty<object>(), subtotal = 14000m, total = 14000m, belowCost = false,
                originLinePublicId = (Guid?)Guid.NewGuid(), pending = 6m,
            },
        },
        withholdings = Array.Empty<object>(),
        totals = new { subtotal = 14000m, discountTotal = 6000m, taxTotal = 0m, withholdingTotal = 0m, total = 14000m, amountDue = 14000m },
        payments = Array.Empty<object>(), messages = Array.Empty<object>(), electronic = (object?)null,
        links = new { origin = (object?)null, voids = (object?)null, voidedBy = (object?)null, notes = Array.Empty<object>(), replacementOf = (object?)null, replacedBy = (object?)null },
        createdBy = new { publicId = Guid.NewGuid(), username = "ana" }, confirmedBy = (object?)null, issues = Array.Empty<object>(), rowVersion = (string?)null,
        validUntil = "2026-10-05", correctionConceptCode = (string?)null,
        origins = origen is { } o ? new[] { new { publicId = o, displayNumber = "PED1" } } : Array.Empty<object>(),
    };
}
