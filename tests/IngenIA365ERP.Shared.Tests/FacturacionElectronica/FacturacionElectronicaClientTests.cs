using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.FacturacionElectronica;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.FacturacionElectronica;

/// <summary>
/// T752 (feature 012, I4; contracts/api.md §24): el cliente de facturación electrónica —configuración, credencial, resoluciones, preparación,
/// documentos, casos a/b/c, contingencias y enlace de descarga—. Las lecturas no llevan clave; todo POST y PUT lleva <c>Idempotency-Key</c>;
/// nada pone <c>Authorization</c> a mano (<c>ElTokenDeSesionLoPoneElHandler</c>); los enums viajan por nombre; un 422 conserva su
/// <c>data</c>. Todo se ensaya contra un servidor de prueba: el canal (simulado o real) no se ve desde la pantalla. (nuevo)
/// </summary>
public class FacturacionElectronicaClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly FacturacionElectronicaClient _cliente;

    public FacturacionElectronicaClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new FacturacionElectronicaClient(http, auth);
    }

    // ------------------------------------------------------------------------------------ preparación --

    [Fact]
    public async Task La_preparacion_es_una_consulta_sin_clave_con_lo_que_falta_y_quien_lo_corrige()
    {
        var caja = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            asOf = "2026-09-28", obligated = true, verdict = 3, channel = (object?)null, resolutions = Array.Empty<object>(),
            openContingency = (object?)null,
            missing = new[] { new { code = "ElectronicInvoicing.Readiness.NoSettings", message = "No hay configuración.", whoFixes = new { page = "/admin/facturacion-electronica", permission = "ElectronicInvoicing.Settings.Manage" } } },
        });

        var r = await _cliente.PreparacionAsync(new DateOnly(2026, 9, 28), cashRegister: caja);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Verdict.Should().Be(TextosDeFacturacionElectronica.VeredictoBloqueado);
        r.Value.Missing.Should().ContainSingle().Which.WhoFixes.Page.Should().Be("/admin/facturacion-electronica");
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("GET");
        vista.Ruta.Should().Be($"/api/electronic-invoicing/readiness?asOf=2026-09-28&cashRegister={caja}");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------- configuración --

    [Fact]
    public async Task Configurar_una_vigencia_lleva_su_clave_y_los_enums_por_nombre()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, VigenciaJson());

        var r = await _cliente.ConfigurarAsync(new ConfiguracionDeEmisionRequest(
            Mode: "OwnSoftware", ChannelCode: "SIMULADO", Environment: "Testing", SoftwareId: "abc", TestSetId: null,
            EmailDeliveryBy: "Erp", IsEnabled: true, ValidFrom: new DateOnly(2026, 10, 1), Reason: "Arranque"), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.ChannelCode.Should().Be("SIMULADO");
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/electronic-invoicing/settings");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
        vista.Cuerpo.Should().Contain("\"mode\":\"OwnSoftware\"").And.Contain("\"environment\":\"Testing\"").And.Contain("\"emailDeliveryBy\":\"Erp\"")
            .And.Contain("\"validFrom\":\"2026-10-01\"");
    }

    [Fact]
    public async Task La_configuracion_vigente_y_verificar_la_credencial()
    {
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("verify-credential", StringComparison.Ordinal)
            ? ServidorDeIntegracion.Json(HttpStatusCode.OK, new { verified = true, verifiedAt = ServidorDeIntegracion.Ahora, outcome = "Validated", messages = Array.Empty<object>() })
            : ServidorDeIntegracion.Json(HttpStatusCode.OK, new
            {
                current = VigenciaJson(), history = new[] { VigenciaJson() },
                availableChannels = new[] { new { channelCode = "SIMULADO", name = "Canal simulado", capabilities = new { documentKinds = new[] { "Invoice" }, events = Array.Empty<string>(), acceptsErpNumber = true, isAsync = false, returnsPdf = false, canSendEmail = false, queriesNumberingRanges = false, issuerContingency = true } } },
                credential = new { key = "t.SIMULADO.json", configured = true, verifiedAt = (DateTime?)null },
            });

        var config = await _cliente.ConfiguracionAsync();
        var verificacion = await _cliente.VerificarCredencialAsync(null, new ClaveDeOperacion());

        config.Value!.AvailableChannels.Single().Capabilities.DocumentKinds.Should().Contain("Invoice");
        config.Value.Credential.Configured.Should().BeTrue();
        verificacion.Value!.Verified.Should().BeTrue();
        _servidor.Vistas[0].Clave.Should().BeNull();
        _servidor.Vistas[1].Ruta.Should().Be("/api/electronic-invoicing/settings/verify-credential");
        _servidor.Vistas[1].Clave.Should().NotBeNullOrWhiteSpace();
    }

    // ----------------------------------------------------------------------------------- resoluciones --

    [Fact]
    public async Task Las_resoluciones_se_filtran_por_nombre_y_su_alta_y_correccion_llevan_clave()
    {
        _servidor.Responder = r => r.Method == HttpMethod.Get
            ? ServidorDeIntegracion.Json(HttpStatusCode.OK, new[] { ResolucionJson() })
            : ServidorDeIntegracion.Json(r.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK, ResolucionJson());
        var id = Guid.NewGuid();
        var datos = new ResolucionRequest("Invoice", null, "18760000001", new DateOnly(2026, 1, 1), "FE", 1, 5000, new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1), "Testing", "Resolución nueva");

        var lista = await _cliente.ResolucionesAsync(kind: "Invoice", status: "Active");
        var alta = await _cliente.RegistrarResolucionAsync(datos, new ClaveDeOperacion());
        var correccion = await _cliente.CorregirResolucionAsync(id, datos, new ClaveDeOperacion());

        lista.Value!.Single().Channels.Single().TechnicalKeyMasked.Should().Be("••••ab12");
        alta.IsSuccess.Should().BeTrue(alta.ErrorMessage);
        correccion.IsSuccess.Should().BeTrue(correccion.ErrorMessage);
        _servidor.Vistas[0].Ruta.Should().Be("/api/electronic-invoicing/resolutions?kind=Invoice&status=Active");
        _servidor.Vistas[0].Clave.Should().BeNull();
        _servidor.Vistas[1].Metodo.Should().Be("POST");
        _servidor.Vistas[1].Cuerpo.Should().Contain("\"kind\":\"Invoice\"").And.Contain("\"rangeTo\":5000");
        _servidor.Vistas[1].Clave.Should().NotBeNullOrWhiteSpace();
        _servidor.Vistas[2].Metodo.Should().Be("PUT");
        _servidor.Vistas[2].Ruta.Should().Be($"/api/electronic-invoicing/resolutions/{id}");
        _servidor.Vistas[2].Clave.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Asociar_a_un_canal_puede_pedir_la_clave_tecnica_al_canal()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, ResolucionJson());
        var id = Guid.NewGuid();

        var r = await _cliente.AsociarACanalAsync(id, new AsociacionACanalRequest("SIMULADO", null, new DateOnly(2026, 10, 1), null, true, "Canal nuevo"),
            new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/electronic-invoicing/resolutions/{id}/channels");
        vista.Cuerpo.Should().Contain("\"fetchTechnicalKeyFromChannel\":true").And.Contain("\"channelCode\":\"SIMULADO\"");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
    }

    // ------------------------------------------------------------------------------------- documentos --

    [Fact]
    public async Task La_bandeja_filtra_por_estado_tipo_contingencia_y_vencidos()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = new[] { DocumentoJson() }, totalCount = 1, page = 1, pageSize = 20 });

        var r = await _cliente.DocumentosAsync(new FiltroDeDocumentosElectronicos
        {
            Status = "Rejected", Kind = "Invoice", Contingency = true, Overdue = true, Page = 2,
        });

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Items.Single().Number.Should().Be("SETT1");
        _servidor.Vistas.Single().Ruta.Should()
            .Be("/api/electronic-invoicing/documents?status=Rejected&kind=Invoice&contingency=true&overdue=true&page=2&pageSize=20");
        _servidor.Vistas.Single().Clave.Should().BeNull();
    }

    [Fact]
    public async Task Reintentar_y_consultar_llevan_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            electronicDocumentPublicId = Guid.NewGuid(), status = 2, outcome = 1, messages = Array.Empty<object>(), attempted = true, detail = (string?)null,
        });
        var id = Guid.NewGuid();

        var reintento = await _cliente.ReintentarAsync(id, new ClaveDeOperacion());
        var consulta = await _cliente.ConsultarEstadoAsync(id, new ClaveDeOperacion());

        reintento.Value!.Status.Should().Be(2);
        consulta.IsSuccess.Should().BeTrue(consulta.ErrorMessage);
        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            $"/api/electronic-invoicing/documents/{id}/retry", $"/api/electronic-invoicing/documents/{id}/query-status");
        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "POST" && !string.IsNullOrEmpty(v.Clave) && v.Authorization == null);
    }

    [Fact]
    public async Task El_caso_a_con_la_huella_distinta_conserva_los_campos()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "ElectronicInvoicing.Document.EconomicFootprintChanged", message = "Cambió lo económico.", data = new { fields = new[] { "lines[1].quantity" } },
        });
        var id = Guid.NewGuid();

        var r = await _cliente.CorregirCasoAAsync(id, new CorreccionCasoARequest("Dirección", new CambiosDeContraparteRequest(Address: "Calle 1")), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("ElectronicInvoicing.Document.EconomicFootprintChanged");
        r.Dato<string[]>("fields").Should().Equal("lines[1].quantity");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/electronic-invoicing/documents/{id}/correct");
        vista.Cuerpo.Should().Contain("\"partySnapshotChanges\"").And.Contain("Calle 1");
    }

    [Fact]
    public async Task Los_casos_b_y_c_y_el_cambio_de_canal_llevan_su_motivo_y_clave()
    {
        var reemplazo = Guid.NewGuid();
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("replacement-draft", StringComparison.Ordinal)
            ? ServidorDeIntegracion.Json(HttpStatusCode.Created, new { replacementDraftPublicId = reemplazo, sourceModule = "Inventory", editRoute = $"/api/inventory/sales/invoices/{reemplazo}" })
            : ServidorDeIntegracion.Json(HttpStatusCode.OK, new { electronicDocumentPublicId = Guid.NewGuid(), status = 0 });
        var id = Guid.NewGuid();

        var borrador = await _cliente.PrepararReemplazoAsync(id, new ClaveDeOperacion());
        (await _cliente.ReemplazarAsync(id, new ReemplazoRequest(reemplazo, "Cantidad errada"), new ClaveDeOperacion())).IsSuccess.Should().BeTrue();
        (await _cliente.CancelarAsync(id, "Venta que no fue", new ClaveDeOperacion())).IsSuccess.Should().BeTrue();
        (await _cliente.TransmitirPorElCanalVigenteAsync(id, "Canal retirado", new ClaveDeOperacion())).IsSuccess.Should().BeTrue();

        borrador.Value!.ReplacementDraftPublicId.Should().Be(reemplazo);
        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            $"/api/electronic-invoicing/documents/{id}/replacement-draft",
            $"/api/electronic-invoicing/documents/{id}/replace",
            $"/api/electronic-invoicing/documents/{id}/cancel",
            $"/api/electronic-invoicing/documents/{id}/transmit-by-current-channel");
        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "POST" && !string.IsNullOrEmpty(v.Clave));
        _servidor.Vistas[1].Cuerpo.Should().Contain(reemplazo.ToString()).And.Contain("Cantidad errada");
        _servidor.Vistas[2].Cuerpo.Should().Contain("\"reason\":\"Venta que no fue\"");
    }

    [Fact]
    public async Task El_enlace_de_un_artefacto_vuelve_absoluto()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { direct = true, url = "/api/attachments/local-blob/abc", expiresAt = ServidorDeIntegracion.Ahora });
        var id = Guid.NewGuid();

        var r = await _cliente.EnlaceDeArtefactoAsync(id, "GraphicRepresentation", version: 2);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Url.Should().Be("https://erp.pruebas/api/attachments/local-blob/abc");
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be($"/api/electronic-invoicing/documents/{id}/download-link?artifact=GraphicRepresentation&version=2");
        vista.Authorization.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------- contingencias --

    [Fact]
    public async Task Declarar_y_cerrar_una_contingencia()
    {
        _servidor.Responder = r => ServidorDeIntegracion.Json(r.RequestUri!.AbsolutePath.EndsWith("close", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.Created, ContingenciaJson());
        var id = Guid.NewGuid();

        var abierta = await _cliente.DeclararContingenciaAsync(new DeclaracionDeContingenciaRequest("Issuer03", "Se cayó el internet", null, "Llamada al proveedor"),
            new ClaveDeOperacion());
        var cerrada = await _cliente.CerrarContingenciaAsync(id, new CierreDeContingenciaRequest("Volvió el internet", null, null), new ClaveDeOperacion());

        abierta.Value!.Type.Should().Be(3);
        cerrada.IsSuccess.Should().BeTrue(cerrada.ErrorMessage);
        _servidor.Vistas[0].Ruta.Should().Be("/api/electronic-invoicing/contingencies");
        _servidor.Vistas[0].Cuerpo.Should().Contain("\"type\":\"Issuer03\"");
        _servidor.Vistas[1].Ruta.Should().Be($"/api/electronic-invoicing/contingencies/{id}/close");
        _servidor.Vistas.Should().OnlyContain(v => !string.IsNullOrEmpty(v.Clave));
    }

    [Fact]
    public async Task La_bitacora_de_contingencias_se_consulta_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new[] { ContingenciaJson() });

        var r = await _cliente.ContingenciasAsync(abiertas: true, desde: new DateOnly(2026, 9, 1));

        r.Value!.Single().Documents.Pending.Should().Be(2);
        _servidor.Vistas.Single().Ruta.Should().Be("/api/electronic-invoicing/contingencies?isOpen=true&from=2026-09-01");
        _servidor.Vistas.Single().Clave.Should().BeNull();
    }

    // -------------------------------------------------------------------------------------- fixtures --

    private static object VigenciaJson() => new
    {
        settingPublicId = Guid.NewGuid(), mode = 2, channelCode = "SIMULADO", environment = 2, softwareId = "abc", testSetId = (string?)null,
        testSetAcceptedAt = (DateTime?)null, emailDeliveryBy = 1, isEnabled = true, credentialVerifiedAt = (DateTime?)null, issuerTaxId = "900123456",
        issuerCheckDigit = "7", issuerBusinessName = "Cooperativa", issuerAddress = "Calle 1", issuerMunicipalityDaneCode = "76001",
        issuerEmail = "fe@coop.test", validFrom = "2026-10-01", validTo = (string?)null, reason = "Arranque", createdByName = "Admin",
    };

    private static object ResolucionJson() => new
    {
        resolutionPublicId = Guid.NewGuid(), kind = 1, backsUpKind = (int?)null, resolutionNumber = "18760000001", resolutionDate = "2026-01-01", prefix = "FE",
        rangeFrom = 1L, rangeTo = 5000L, validFrom = "2026-01-01", validTo = "2027-01-01", environment = 2, lastIssuedNumber = (long?)null,
        consumedFraction = 0m, daysToExpire = 95, status = "Active",
        channels = new[] { new { channelCode = "SIMULADO", softwareId = (string?)null, validFrom = "2026-01-01", validTo = (string?)null, technicalKeyMasked = "••••ab12" } },
    };

    private static object DocumentoJson() => new
    {
        electronicDocumentPublicId = Guid.NewGuid(), kind = 1, dianDocumentTypeCode = "01", prefix = "SETT", consecutive = 1L, number = "SETT1", environment = 2,
        channelCode = "SIMULADO", source = new { module = "Inventory", documentPublicId = Guid.NewGuid(), documentClass = "SalesInvoice", route = "/ventas/documentos/x" },
        counterpartyName = "Cliente", issuedAt = ServidorDeIntegracion.Ahora, total = 1000m, status = 4, contingencyType = (int?)null, rejectedBy = 1,
        uniqueCode = (string?)null, uniqueCodeKind = (int?)null, attemptCount = 1, nextAttemptAt = (DateTime?)null, transmissionDeadline = (DateTime?)null,
        lastMessage = "Rechazo",
    };

    private static object ContingenciaJson() => new
    {
        contingencyPublicId = Guid.NewGuid(), type = 3, channelCode = "SIMULADO", startedAt = ServidorDeIntegracion.Ahora, endedAt = (DateTime?)null, isOpen = true,
        detectedBy = new { kind = 1, name = "Cajero" }, reason = "Se cayó el internet", deadlineAt = (DateTime?)null,
        documents = new { total = 3, pending = 2, transmitted = 1, rejected = 0 },
    };
}
