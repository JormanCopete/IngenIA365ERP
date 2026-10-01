using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Ventas;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T633 (feature 012, I3; contracts/api.md §18–§21): el cliente de Ventas —POS, caja, precios y documentos de oficina—. Las lecturas
/// no llevan clave y las escrituras sí (un cobro reintentado con la misma clave nunca vende dos veces, SC-002); nada pone
/// <c>Authorization</c> a mano (<c>ElTokenDeSesionLoPoneElHandler</c>); los enums viajan por nombre; un 422 conserva su <c>data</c>
/// (el total que cambió, el bono ya usado); la entrega de una carta vuelve como archivo y la de una tirilla como modelo. (nuevo)
/// </summary>
public class VentasClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly VentasClient _cliente;

    public VentasClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new VentasClient(http, auth);
    }

    // ---------------------------------------------------------------------------------------------- POS --

    [Fact]
    public async Task La_lectura_del_lector_es_una_consulta_exacta_sin_clave()
    {
        var sesion = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            product = new { publicId = Guid.NewGuid(), code = "ARZ-1", name = "Arroz" }, status = 1,
            unit = new { publicId = Guid.NewGuid(), code = "UND", name = "Unidad" }, factor = 1m, price = 4500m,
            priceList = (object?)null, includesTaxes = true, available = 12m,
        });

        var r = await _cliente.BuscarEnElPosAsync("7702001 ", sesion);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Price.Should().Be(4500m);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/pos/lookup?code=7702001&cashSession={sesion}");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Abrir_la_venta_con_la_primera_lectura_lleva_su_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, VentaJson());
        var sesion = Guid.NewGuid();

        var r = await _cliente.AbrirVentaAsync(new AbrirVentaRequest(sesion, new PrimeraLecturaRequest("7702001", 3m)), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Totals.AmountDue.Should().Be(13500m);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/inventory/pos/drafts");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
        vista.Cuerpo.Should().Contain(sesion.ToString()).And.Contain("\"firstLine\"").And.Contain("7702001");
    }

    [Fact]
    public async Task Cambiar_el_rol_de_la_venta_manda_el_nombre_del_rol()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, VentaJson());
        var id = Guid.NewGuid();

        var r = await _cliente.CambiarCabeceraAsync(id, new CabeceraDeVentaRequest(Role: TextosDeVentas.NombreDeRolDeCaja(TextosDeVentas.RolFacturaAPedido)),
            new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("PATCH");
        vista.Ruta.Should().Be($"/api/inventory/pos/drafts/{id}");
        vista.Cuerpo.Should().Contain("\"role\":\"InvoiceOnRequest\"");
    }

    [Fact]
    public async Task El_cobro_reintentado_con_el_mismo_contenido_conserva_la_clave()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
            ServidorDeIntegracion.Json(HttpStatusCode.OK, CobroJson()),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var id = Guid.NewGuid();
        var efectivo = Guid.NewGuid();
        var cobro = new CobroRequest([new PagoRequest(efectivo, 13500m, Tendered: 20000m)], 13500m, null);

        (await _cliente.CobrarAsync(id, cobro, clave)).IsSuccess.Should().BeFalse();
        var r = await _cliente.CobrarAsync(id, cobro, clave);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Change.Should().Be(6500m);
        r.Value.Ticket!.Lines.Should().ContainSingle().Which.Code.Should().Be("ARZ-1");
        _servidor.Vistas.Select(v => v.Clave).Distinct().Should().ContainSingle("un cobro repetido nunca es una segunda venta (SC-002)");
        _servidor.Vistas[0].Ruta.Should().Be($"/api/inventory/pos/drafts/{id}/checkout");
        _servidor.Vistas[0].Cuerpo.Should().Contain("\"expectedAmountDue\":13500").And.Contain("\"tendered\":20000");
    }

    [Fact]
    public async Task Un_total_que_cambio_conserva_su_data()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Inventory.Document.TotalChanged", message = "El total cambió.", data = new { expected = 13500, actual = 14000 },
        });

        var r = await _cliente.CobrarAsync(Guid.NewGuid(), new CobroRequest([], 13500m, null), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Inventory.Document.TotalChanged");
        r.Entero("actual").Should().Be(14000);
    }

    [Fact]
    public async Task Las_ventas_suspendidas_del_punto_se_consultan_sin_clave()
    {
        var punto = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new[]
        {
            new { draftPublicId = Guid.NewGuid(), cashSessionPublicId = (Guid?)null, cashierName = "Cajero", lines = 2, total = 9000m,
                suspended = new { label = "Señora del saco rojo", suspendedAt = DateTime.UtcNow, suspendedByName = "Cajero" } },
        });

        var r = await _cliente.VentasDelPosAsync(punto: punto, suspendidas: true);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Single().Suspended!.Label.Should().Be("Señora del saco rojo");
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/pos/drafts?pointOfSale={punto}&suspended=true");
        _servidor.Vistas.Single().Clave.Should().BeNull();
    }

    [Fact]
    public async Task El_aprobador_en_persona_pide_su_desafio_y_decide_con_su_prueba_nunca_con_contrasena()
    {
        var solicitud = Guid.NewGuid();
        var desafio = Guid.NewGuid();
        var respuestas = new Queue<HttpResponseMessage>([
            ServidorDeIntegracion.Json(HttpStatusCode.OK, new { challengePublicId = desafio, expiresAt = DateTime.UtcNow.AddMinutes(2), methods = new[] { "Passkey", "Totp" }, publicKeyOptions = (string?)null }),
            ServidorDeIntegracion.Json(HttpStatusCode.OK, new { requestPublicId = solicitud, status = "Approved", currentLevel = 1, source = new { publicId = Guid.NewGuid(), @class = "PosEquivalentDocument", status = "Confirmed", displayNumber = "POS1" } }),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();

        var d = await _cliente.DesafioDePresenciaAsync(solicitud, "supervisor@coop.co", new ClaveDeOperacion());
        var r = await _cliente.DecidirEnPersonaAsync(solicitud, new DecisionEnPersonaRequest("Approve", null, "InPersonTotp",
            new PresenciaRequest(d.Value!.ChallengePublicId, null, "123456"), "abc"), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Status.Should().Be("Approved");
        _servidor.Vistas[0].Ruta.Should().Be($"/api/inventory/approvals/{solicitud}/presence-challenge");
        _servidor.Vistas[1].Ruta.Should().Be($"/api/inventory/approvals/{solicitud}/decide");
        _servidor.Vistas[1].Cuerpo.Should().Contain("\"method\":\"InPersonTotp\"").And.Contain("\"totpCode\":\"123456\"").And.NotContain("password");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave != null && v.Authorization == null);
    }

    // ---------------------------------------------------------------------------------------- caja --

    [Fact]
    public async Task Abrir_la_sesion_manda_la_base_por_denominaciones()
    {
        var caja = Guid.NewGuid();
        var billete = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new
        {
            session = SesionJson(), baseIncomeDocument = (object?)null, warnings = Array.Empty<object>(),
        });

        var r = await _cliente.AbrirSesionAsync(new AbrirSesionRequest(caja, 200000m, [new ConteoDeDenominacionRequest(billete, 4)]), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Session.PointOfSale.PosEnabled.Should().BeTrue();
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/cash-sessions");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Cuerpo.Should().Contain("\"openingBase\":200000").And.Contain(billete.ToString());
    }

    [Fact]
    public async Task Mi_sesion_abierta_se_busca_por_mine_y_status_por_nombre()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = new[] { SesionJson() }, page = 1, pageSize = 1, totalCount = 1 });

        var r = await _cliente.MiSesionAbiertaAsync();

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Cashier.Name.Should().Be("Cajero Uno");
        _servidor.Vistas.Single().Ruta.Should().Be("/api/inventory/cash-sessions?mine=true&status=Open&page=1&pageSize=1");
    }

    [Fact]
    public async Task El_cierre_manda_lo_contado_por_medio_y_el_retiro_por_nombre()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            cashSessionPublicId = Guid.NewGuid(), status = 2, lines = Array.Empty<object>(), differenceDocument = (object?)null,
            closingWithdrawalDocument = (object?)null, batch = (object?)null,
        });
        var id = Guid.NewGuid();
        var medio = Guid.NewGuid();

        var r = await _cliente.CerrarSesionAsync(id, new CerrarSesionRequest(
            [new ConteoPorMedioRequest(medio, CountedTotal: 180000m, Reason: "Faltante del cambio")],
            new RetiroDeCierreRequest(TextosDeVentas.NombreDeDestino(1))), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/cash-sessions/{id}/close");
        vista.Cuerpo.Should().Contain("\"destination\":\"Safe\"").And.Contain("\"countedTotal\":180000");
    }

    [Fact]
    public async Task Un_movimiento_de_caja_manda_su_clase_por_nombre()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new
        {
            documentPublicId = Guid.NewGuid(), number = (string?)null, status = 0, cashSessionPublicId = Guid.NewGuid(), kind = 1,
            sourcePaymentMeansCode = "EFE", targetPaymentMeansCode = (string?)null, destination = 1, destinationCashRegisterCode = (string?)null,
            amount = 500000m, reason = "Exceso de efectivo", createdByName = "Cajero", approvedByName = (string?)null,
        });

        var r = await _cliente.GuardarMovimientoAsync(null, new MovimientoDeCajaRequest(Guid.NewGuid(), TextosDeVentas.NombreDeClaseDeMovimiento(1),
            Guid.NewGuid(), 500000m, "Exceso de efectivo", Destination: TextosDeVentas.NombreDeDestino(1)), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Amount.Should().Be(500000m);
        _servidor.Vistas.Single().Cuerpo.Should().Contain("\"kind\":\"WithdrawalToSafe\"");
    }

    [Fact]
    public async Task El_arqueo_en_pdf_es_una_descarga_sin_clave()
    {
        _servidor.Responder = _ => Pdf();
        var id = Guid.NewGuid();

        var r = await _cliente.DescargarArqueoAsync(id);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.TipoContenido.Should().Be("application/pdf");
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/cash-sessions/{id}/count-report");
        _servidor.Vistas.Single().Clave.Should().BeNull();
    }

    // -------------------------------------------------------------------------------------- precios --

    [Fact]
    public async Task Resolver_un_precio_lleva_sus_dimensiones_en_la_query()
    {
        var producto = Guid.NewGuid();
        var persona = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            price = 4200m, includesTaxes = true, fallbackUsed = false,
            priceList = new { publicId = Guid.NewGuid(), code = "ASOC", name = "Asociados", matchedDimensions = new[] { "Segment" } },
            candidates = Array.Empty<object>(),
        });

        var r = await _cliente.ResolverPrecioAsync(producto, persona: persona, fecha: new DateOnly(2026, 10, 1));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.PriceList.Code.Should().Be("ASOC");
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/prices/resolve?product={producto}&person={persona}&date=2026-10-01");
    }

    [Fact]
    public async Task Los_precios_de_una_lista_viajan_con_su_motivo()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { created = 1, updated = 0, unchanged = 0, removed = 0 });
        var lista = Guid.NewGuid();

        var r = await _cliente.FijarPreciosAsync(lista, new PreciosDeListaRequest([new PrecioDeListaRequest(Guid.NewGuid(), Guid.NewGuid(), 4200m)], null, "Alza de octubre"),
            new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Created.Should().Be(1);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("PUT");
        vista.Ruta.Should().Be($"/api/inventory/price-lists/{lista}/items");
        vista.Cuerpo.Should().Contain("Alza de octubre");
    }

    // ----------------------------------------------------------------------------------- documentos --

    [Fact]
    public async Task La_lista_de_ventas_filtra_por_clase_y_pendientes_de_entrega()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });
        var sesion = Guid.NewGuid();

        var r = await _cliente.ListarDocumentosAsync(new FiltroDeVentas { Class = 28, CashSession = sesion, PendingDelivery = true });

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/sales/documents?class=28&cashSession={sesion}&pendingDelivery=true&page=1&pageSize=20");
    }

    [Fact]
    public async Task Entregar_una_tirilla_devuelve_el_modelo_y_una_carta_el_pdf()
    {
        var id = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            documentPublicId = id, format = 80, copy = false, ticket = TirillaJson(), pdf = (string?)null, fileName = (string?)null, emailSent = false,
        });
        var tirilla = await _cliente.EntregarAsync(id, new EntregaRequest(TextosDeVentas.NombreDeFormato(80)), new ClaveDeOperacion());

        _servidor.Responder = _ => Pdf();
        var carta = await _cliente.EntregarAsync(id, new EntregaRequest(TextosDeVentas.NombreDeFormato(216)), new ClaveDeOperacion());

        tirilla.IsSuccess.Should().BeTrue(tirilla.ErrorMessage);
        tirilla.Value!.Ticket!.Header.CompanyName.Should().Be("Cooperativa");
        tirilla.Value.Archivo.Should().BeNull();
        carta.IsSuccess.Should().BeTrue(carta.ErrorMessage);
        carta.Value!.Archivo!.TipoContenido.Should().Be("application/pdf");
        _servidor.Vistas.Should().OnlyContain(v => v.Ruta == $"/api/inventory/sales/documents/{id}/deliver" && v.Clave != null);
        _servidor.Vistas[0].Cuerpo.Should().Contain("\"format\":\"Ticket80\"");
        _servidor.Vistas[1].Cuerpo.Should().Contain("\"format\":\"Letter\"");
    }

    [Fact]
    public async Task Reimprimir_va_a_la_ruta_comun_de_documentos()
    {
        var id = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            documentPublicId = id, format = 58, copy = true, ticket = TirillaJson(), pdf = (string?)null, fileName = (string?)null, emailSent = false,
        });

        var r = await _cliente.ReimprimirAsync(id, new EntregaRequest(TextosDeVentas.NombreDeFormato(58), Reason: "El cliente la perdió"), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Copy.Should().BeTrue();
        _servidor.Vistas.Single().Ruta.Should().Be($"/api/inventory/documents/{id}/reprint");
    }

    [Fact]
    public async Task Confirmar_una_factura_de_oficina_manda_lo_que_la_persona_vio_a_pagar()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            documentPublicId = Guid.NewGuid(), status = 2, displayNumber = "FV-1", warnings = Array.Empty<object>(),
        });
        var id = Guid.NewGuid();

        var r = await _cliente.ConfirmarAsync(VentasClient.Rutas.Facturas, id, 118000m, [1, 2], new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/sales/invoices/{id}/confirm");
        vista.Cuerpo.Should().Contain("\"expectedAmountDue\":118000");
    }

    // ------------------------------------------------------------------------------------ ayudantes --

    private static HttpResponseMessage Pdf()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) };
        resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        resp.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "venta.pdf" };
        return resp;
    }

    private static object Ref(string code) => new { publicId = Guid.NewGuid(), code, name = code };

    private static object VentaJson() => new
    {
        draftPublicId = Guid.NewGuid(), status = 0, @class = 28, documentType = new { publicId = Guid.NewGuid(), code = "POS", role = 1 },
        cashSessionPublicId = Guid.NewGuid(), pointOfSale = Ref("P1"), cashRegister = Ref("C1"), warehouse = Ref("B1"),
        operationDate = "2026-09-27", customer = new { personPublicId = (Guid?)null, name = "Consumidor final", isFinalConsumer = true, segment = (string?)null },
        salesperson = (object?)null, lines = Array.Empty<object>(), lastLine = (object?)null, documentDiscount = (object?)null,
        totals = new { subtotal = 11345m, discountTotal = 0m, taxTotal = 2155m, withholdingTotal = 0m, total = 13500m, amountDue = 13500m },
        availablePaymentMeans = Array.Empty<object>(), suspended = (object?)null, pendingApprovals = Array.Empty<object>(),
        warnings = Array.Empty<object>(), notes = (string?)null, rowVersion = (string?)null,
    };

    private static object SesionJson() => new
    {
        cashSessionPublicId = Guid.NewGuid(), pointOfSale = new { pointOfSalePublicId = Guid.NewGuid(), code = "P1", name = "Florida", posEnabled = true },
        cashRegister = Ref("C1"), cashier = new { userPublicId = Guid.NewGuid(), name = "Cajero Uno", personPublicId = (Guid?)null },
        operatingDate = "2026-09-27", openedAt = DateTime.UtcNow, openingBase = 200000m, baseMode = "Daily", status = 1, closedAt = (DateTime?)null,
        closedByName = (string?)null, salesCount = 0, salesTotal = 0m, differenceDocument = (object?)null,
    };

    private static object TirillaJson() => new
    {
        format = 80, copy = false,
        header = new { companyName = "Cooperativa", nit = "900", branchName = "Florida", address = (string?)null, resolutionText = (string?)null, regimeText = "Responsable de IVA" },
        document = new { classLabel = "Documento equivalente POS", prefix = "POS", number = 1L, issuedAt = DateTime.UtcNow, cashRegisterCode = "C1", cashierName = "Cajero", salespersonName = (string?)null },
        party = new { name = "Consumidor final", idType = "CC", idNumber = "222222222222" },
        lines = new[] { new { code = "ARZ-1", description = "Arroz", quantity = 3m, unitCode = "UND", unitPrice = 4500m, discount = 0m, total = 13500m, taxMark = "G" } },
        taxes = Array.Empty<object>(), withholdings = Array.Empty<object>(),
        totals = new { subtotal = 11345m, discountTotal = 0m, taxTotal = 2155m, total = 13500m, amountDue = 13500m },
        payments = new[] { new { meansName = "Efectivo", amount = 13500m, reference = (string?)null, last4 = (string?)null } },
        change = 6500m, electronic = (object?)null, footer = new[] { "SIN VALIDEZ FISCAL" },
    };

    private static object CobroJson() => new
    {
        documentPublicId = Guid.NewGuid(), status = 2, approvalRequestPublicId = (Guid?)null, @class = 28, prefix = "POS", number = 1L,
        total = 13500m, amountDue = 13500m, change = 6500m, postingMode = 3, electronic = (object?)null, ticket = TirillaJson(),
        warnings = Array.Empty<object>(),
    };
}
