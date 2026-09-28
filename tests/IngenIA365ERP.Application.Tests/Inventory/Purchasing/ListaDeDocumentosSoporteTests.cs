using FluentAssertions;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, I4, T748 (api.md §14.7): la lista de <c>/api/inventory/purchases/support-documents</c> es la misma consulta de las facturas
/// del proveedor sobre las clases del documento soporte y su nota de ajuste; las demás clases siguen fuera de esa consulta.
/// </summary>
public sealed class ListaDeDocumentosSoporteTests
{
    private static readonly ListSupplierInvoicesQueryValidator Validador = new();

    [Theory]
    [InlineData(DocumentClass.SupplierInvoice)]
    [InlineData(DocumentClass.SupplierNote)]
    [InlineData(DocumentClass.SupportDocument)]
    [InlineData(DocumentClass.SupportDocumentAdjustmentNote)]
    public void Admite_las_clases_de_factura_nota_y_documento_soporte(DocumentClass clase) =>
        Validador.Validate(new ListSupplierInvoicesQuery(new FiltrosDeCompras(), new PageRequest(1, 20), clase)).IsValid.Should().BeTrue();

    [Fact]
    public void Rechaza_otra_clase() =>
        Validador.Validate(new ListSupplierInvoicesQuery(new FiltrosDeCompras(), new PageRequest(1, 20), DocumentClass.PurchaseReceipt)).IsValid.Should().BeFalse();
}
