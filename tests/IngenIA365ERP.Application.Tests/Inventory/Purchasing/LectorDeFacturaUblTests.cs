using System.IO.Compression;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T332 (api.md §14.4 <c>POST /prefill</c>; E10): el lector de la factura del proveedor lee en memoria un XML UBL
/// 2.1, el <c>AttachedDocument</c> que lo envuelve y el ZIP que lo trae, y da prefijo, número, CUFE, fechas, forma de pago, líneas
/// e impuestos; un PDF o una nota no son una factura. El archivo no se guarda: el lector no conoce el almacén de adjuntos, y el
/// prellenado sólo lee. Muestras en <c>Muestras/</c>.
/// </summary>
public class LectorDeFacturaUblTests
{
    private static byte[] Muestra(string nombre) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Inventory", "Purchasing", "Muestras", nombre));

    [Fact]
    public void El_XML_de_la_factura_da_numero_CUFE_fechas_forma_de_pago_lineas_e_impuestos()
    {
        var r = new LectorDeFacturaUbl().Leer(Muestra("factura-ubl.xml"));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var f = r.Value;
        f.Prefix.Should().Be("FEV");
        f.Number.Should().Be("4521");
        f.Cufe.Should().HaveLength(96).And.Be(f.Cufe!.ToLowerInvariant());
        f.IssueDate.Should().Be(new DateOnly(2026, 9, 20));
        f.DueDate.Should().Be(new DateOnly(2026, 10, 20));
        f.PaymentForm.Should().Be("Credit");
        f.SupplierTaxId.Should().Be("900123456");
        f.SupplierName.Should().Be("Distribuidora del Valle S.A.S.");
        f.Lines.Should().HaveCount(2);
        f.Lines[0].Quantity.Should().Be(3m);
        f.Lines[0].UnitCode.Should().Be("DZN");
        f.Lines[0].UnitPrice.Should().Be(15_600m);
        f.Lines[0].Barcode.Should().Be("7702511000012");
        f.Lines[0].SupplierItemCode.Should().Be("ARR-500");
        f.Lines[1].Discount.Should().Be(500m);
        f.Lines[1].Taxes.Should().ContainSingle(t => t.Code == "01" && t.Rate == 0.19m && t.Amount == 1_900m);
        f.Totals.Payable.Should().Be(58_700m);
    }

    [Fact]
    public void El_AttachedDocument_trae_la_factura_adentro()
    {
        var r = new LectorDeFacturaUbl().Leer(Muestra("attached-document.xml"));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Prefix.Should().Be("SETP");
        r.Value.Number.Should().Be("88");
        r.Value.PaymentForm.Should().Be("Cash");
        r.Value.SupplierTaxId.Should().Be("901555666");
        r.Value.Lines.Should().ContainSingle(l => l.Description == "Flete Cali - Palmira" && l.UnitPrice == 50_000m);
    }

    [Fact]
    public void Un_ZIP_con_el_XML_se_lee_en_memoria()
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry("ad0900123456000FEV4521.xml");
            using var flujo = entrada.Open();
            flujo.Write(Muestra("factura-ubl.xml"));
        }
        var r = new LectorDeFacturaUbl().Leer(memoria.ToArray());
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Number.Should().Be("4521");
    }

    [Fact]
    public void Un_PDF_no_se_puede_leer()
    {
        var r = new LectorDeFacturaUbl().Leer(Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n1 0 obj"));
        r.Error.Code.Should().Be("Inventory.SupplierInvoice.FileUnreadable");
    }

    [Fact]
    public void Un_texto_que_no_es_XML_tampoco()
    {
        var r = new LectorDeFacturaUbl().Leer(Encoding.UTF8.GetBytes("esto no es un xml"));
        r.Error.Code.Should().Be("Inventory.SupplierInvoice.FileUnreadable");
    }

    [Fact]
    public void Una_nota_credito_no_es_una_factura()
    {
        var r = new LectorDeFacturaUbl().Leer(Muestra("nota-credito-ubl.xml"));
        r.Error.Code.Should().Be("Inventory.SupplierInvoice.NotAnInvoice");
    }

    [Fact]
    public async Task El_prellenado_sugiere_el_producto_y_la_persona_y_no_guarda_el_archivo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.C.Db.ProductBarcodes.Add(new ProductBarcode { ProductId = c.K.ProductoId(c.P1), Barcode = "7702511000012", IsPrimary = true });
        await c.C.Db.SaveChangesAsync();
        var adjuntosAntes = c.C.Db.Attachments.Count();

        var r = await new PrefillSupplierInvoiceQueryHandler(c.C.Db, new LectorDeFacturaUbl())
            .Handle(new PrefillSupplierInvoiceQuery(Muestra("factura-ubl.xml")), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Supplier.PersonPublicId.Should().Be(c.ProveedorA.PublicId);
        r.Value.Lines[0].SuggestedProductPublicId.Should().Be(c.P1, "por el código de barras del XML");
        r.Value.Lines[1].SuggestedProductPublicId.Should().Be(c.P3, "por el código del proveedor, que es el del producto");
        c.C.Db.Attachments.Count().Should().Be(adjuntosAntes, "el XML se lee en memoria y se descarta (E10)");
    }

    [Fact]
    public void El_lector_no_conoce_el_almacen_de_adjuntos()
    {
        typeof(LectorDeFacturaUbl).GetConstructors().SelectMany(k => k.GetParameters())
            .Should().BeEmpty("sin IBlobStore ni base: no tiene cómo guardar el archivo");
        typeof(PrefillSupplierInvoiceQueryHandler).GetConstructors().SelectMany(k => k.GetParameters())
            .Select(p => p.ParameterType.Name).Should().NotContain("IBlobStore");
    }
}
