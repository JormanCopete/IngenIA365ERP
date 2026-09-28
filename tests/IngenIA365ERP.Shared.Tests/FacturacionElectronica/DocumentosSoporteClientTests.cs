using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Models.Compras;
using IngenIA365ERP.Shared.Services.Compras;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Ventas;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.FacturacionElectronica;

/// <summary>
/// T752, T757, T758 (feature 012, I4; contracts/api.md §14.7, §18.3.1, §20.3): el documento soporte y su nota de ajuste en
/// <c>ComprasClient</c> (lista por clase, ciclo común sobre su ruta y la generación semanal), y en <c>VentasClient</c> la factura que pide
/// el comprador sobre un documento equivalente POS ya expedido y la entrega en carta de un electrónico, que vuelve como enlace firmado. (nuevo)
/// </summary>
public class DocumentosSoporteClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly ComprasClient _compras;
    private readonly VentasClient _ventas;

    public DocumentosSoporteClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _compras = new ComprasClient(http, auth);
        _ventas = new VentasClient(http, auth);
    }

    [Fact]
    public async Task La_lista_de_documentos_soporte_y_la_de_sus_notas()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0L });
        var proveedor = Guid.NewGuid();

        (await _compras.ListarDocumentosSoporteAsync(new FiltroDeCompras { SupplierPersonPublicId = proveedor })).IsSuccess.Should().BeTrue();
        (await _compras.ListarDocumentosSoporteAsync(new FiltroDeCompras(), notas: true)).IsSuccess.Should().BeTrue();

        _servidor.Vistas[0].Ruta.Should().Be($"/api/inventory/purchases/support-documents?supplierPersonPublicId={proveedor}&page=1&pageSize=20");
        _servidor.Vistas[1].Ruta.Should().Be("/api/inventory/purchases/support-documents?page=1&pageSize=20&class=SupportDocumentAdjustmentNote");
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task El_ciclo_del_documento_soporte_va_por_su_ruta_con_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { documentPublicId = Guid.NewGuid() });
        var id = Guid.NewGuid();

        await _compras.ConfirmarAsync(ComprasClient.Rutas.DocumentosSoporte, id, null, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/purchases/support-documents/{id}/confirm");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task La_generacion_semanal_lleva_su_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new[]
        {
            new { supportDocumentPublicId = Guid.NewGuid(), supplierPersonPublicId = Guid.NewGuid(), receiptPublicIds = new[] { Guid.NewGuid() } },
        });

        var r = await _compras.GenerarDocumentosSoporteSemanalesAsync(new DateOnly(2026, 9, 25), new ClaveDeOperacion());

        r.Value!.Single().ReceiptPublicIds.Should().ContainSingle();
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/inventory/purchases/support-documents/weekly");
        vista.Cuerpo.Should().Contain("\"upTo\":\"2026-09-25\"");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Pedir_factura_sobre_un_documento_equivalente_es_una_sola_escritura_con_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            adjustmentNotePublicId = Guid.NewGuid(), adjustmentNoteNumber = "NA1", invoicePublicId = Guid.NewGuid(), invoiceNumber = "FE10", messages = Array.Empty<object>(),
        });
        var id = Guid.NewGuid();
        var comprador = Guid.NewGuid();

        var r = await _ventas.PedirFacturaAsync(id, new FacturaEnLugarRequest(comprador, "La pidió el comprador", 13500m), new ClaveDeOperacion());

        r.Value!.InvoiceNumber.Should().Be("FE10");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/sales/documents/{id}/invoice-instead");
        vista.Cuerpo.Should().Contain(comprador.ToString()).And.Contain("\"expectedAmountDue\":13500");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task La_carta_de_un_electronico_vuelve_como_enlace_firmado()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            documentPublicId = Guid.NewGuid(), format = 3, copy = false, ticket = (object?)null, fileName = (string?)null, emailSent = true,
            link = new { direct = true, url = "https://s3.test/rg.pdf", expiresAt = ServidorDeIntegracion.Ahora },
        });

        var r = await _ventas.EntregarAsync(Guid.NewGuid(), new EntregaRequest("Letter", SendEmail: true), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Link!.Url.Should().Be("https://s3.test/rg.pdf");
        r.Value.EmailSent.Should().BeTrue();
    }

    [Fact]
    public void El_bloque_electronico_de_la_venta_se_lee_tipado()
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            electronicDocumentPublicId = Guid.NewGuid(), kind = 4, status = 5, uniqueCode = "abc", qrContent = "qr", contingencyType = 3, waitedMs = 0L,
            deliverable = true, pendingDelivery = false, messages = (object?)null,
        });

        var bloque = ElectronicoDeLaVentaDto.Desde(json);

        bloque!.Status.Should().Be(5);
        bloque.ContingencyType.Should().Be(3);
        ElectronicoDeLaVentaDto.Desde(null).Should().BeNull();
    }
}
