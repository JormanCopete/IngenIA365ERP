using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Sales.CamposAdicionales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-15 (pedido del dueño: la placa del vehículo): la cooperativa define sus campos adicionales; el borrador de la venta sella su valor
/// con el tipo y el tamaño del campo; el obligatorio falta al confirmar; lo que se imprime va a las notas de la factura y la cotización lo pasa
/// a la venta.
/// </summary>
public class CamposAdicionalesTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private async Task<DocumentFieldDto> PlacaAsync(bool obligatorio = true, bool enCotizaciones = false)
    {
        var r = await new SaveDocumentFieldCommandHandler(_db).Handle(new SaveDocumentFieldCommand(null, "placa", "Placa", DocumentFieldType.Text, 7,
            obligatorio, ShowInSales: true, ShowInQuotes: enCotizaciones, PrintOnInvoice: true, SortOrder: 1, IsActive: true), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private async Task<InventoryDocument> DocumentoAsync(DocumentClass clase)
    {
        var documento = new InventoryDocument { Class = clase, DocumentTypeId = 1, OperationDate = new DateOnly(2026, 11, 15) };
        _db.InventoryDocuments.Add(documento);
        await _db.SaveChangesAsync();
        return documento;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<IReadOnlyList<DocumentFieldValueDto>>> FijarAsync(InventoryDocument d, string codigo, string? valor) =>
        new SetDocumentFieldValuesCommandHandler(_db).Handle(new SetDocumentFieldValuesCommand(d.PublicId, [new DocumentFieldValueInput(codigo, valor)]), default);

    [Fact]
    public async Task El_codigo_del_campo_es_unico()
    {
        await PlacaAsync();
        var otra = await new SaveDocumentFieldCommandHandler(_db).Handle(new SaveDocumentFieldCommand(null, "PLACA", "Otra placa", DocumentFieldType.Text, 7,
            false, true, false, true, 2, true), default);

        otra.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task El_valor_se_sella_en_mayusculas_y_respeta_el_tamano()
    {
        await PlacaAsync();
        var factura = await DocumentoAsync(DocumentClass.SalesInvoice);

        var larga = await FijarAsync(factura, "PLACA", "ABC12345");
        var buena = await FijarAsync(factura, "PLACA", "abc123");

        larga.Error.Code.Should().Be("Inventory.DocumentField.TooLong");
        buena.Value.Should().ContainSingle().Which.Should().Match<DocumentFieldValueDto>(v => v.Value == "ABC123" && v.Name == "Placa");
    }

    [Fact]
    public async Task Un_campo_de_ventas_no_se_pide_en_la_cotizacion()
    {
        await PlacaAsync(enCotizaciones: false);
        var cotizacion = await DocumentoAsync(DocumentClass.SalesQuote);

        var r = await FijarAsync(cotizacion, "PLACA", "ABC123");

        r.Error.Code.Should().Be("Inventory.DocumentField.NotForDocument");
    }

    [Fact]
    public async Task El_obligatorio_falta_al_confirmar_hasta_que_se_digita_y_va_a_las_notas()
    {
        await PlacaAsync(obligatorio: true);
        var factura = await DocumentoAsync(DocumentClass.SalesInvoice);

        (await CamposAdicionalesDeVenta.FaltanteAsync(_db, factura, default))!.Code.Should().Be("Inventory.DocumentField.Required");
        await FijarAsync(factura, "PLACA", "XYZ987");
        (await CamposAdicionalesDeVenta.FaltanteAsync(_db, factura, default)).Should().BeNull();
        (await CamposAdicionalesDeVenta.NotasAsync(_db, factura.Id, default)).Should().Equal("Placa: XYZ987");
    }

    [Fact]
    public async Task La_cotizacion_pasa_la_placa_a_la_venta()
    {
        await PlacaAsync(obligatorio: false, enCotizaciones: true);
        var cotizacion = await DocumentoAsync(DocumentClass.SalesQuote);
        var venta = await DocumentoAsync(DocumentClass.SalesInvoice);
        await FijarAsync(cotizacion, "PLACA", "QWE456");

        await CamposAdicionalesDeVenta.CopiarAsync(_db, cotizacion.Id, venta, default);
        await _db.SaveChangesAsync();

        (await _db.DocumentFieldValues.SingleAsync(v => v.DocumentId == venta.Id)).Value.Should().Be("QWE456");
    }
}
