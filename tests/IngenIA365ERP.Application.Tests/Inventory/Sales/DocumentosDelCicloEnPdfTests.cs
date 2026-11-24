using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Sales.Quotes;
using IngenIA365ERP.Domain.Enums.Inventory;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-23 (pedido del dueño): el pedido y la remisión se imprimen con el molde de la cotización y su propio título; el archivo lleva el
/// nombre de su clase y las condiciones y la vigencia son sólo de la cotización.
/// </summary>
public class DocumentosDelCicloEnPdfTests
{
    [Theory]
    [InlineData(DocumentClass.SalesQuote, "Cotización")]
    [InlineData(DocumentClass.SalesOrder, "Pedido")]
    [InlineData(DocumentClass.Shipment, "Remisión")]
    public void Cada_clase_lleva_su_titulo(DocumentClass clase, string titulo) =>
        ModeloDeCotizacion.TituloDe(clase).Should().Be(titulo);

    [Theory]
    [InlineData("Cotización", "REM-12", "cotizacion-REM-12.pdf", true)]
    [InlineData("Remisión", "REM-12", "remision-REM-12.pdf", false)]
    [InlineData("Pedido", "PED-7", "pedido-PED-7.pdf", false)]
    public void El_archivo_se_llama_como_su_clase(string titulo, string numero, string archivo, bool esCotizacion)
    {
        var imprimible = Imprimible(titulo, numero);

        imprimible.NombreDelArchivo.Should().Be(archivo);
        imprimible.EsCotizacion.Should().Be(esCotizacion);
    }

    [Fact]
    public void El_borrador_sin_numero_lleva_la_marca_de_borrador() =>
        Imprimible("Remisión", null).NombreDelArchivo.Should().StartWith("remision-borrador-");

    private static CotizacionImprimible Imprimible(string titulo, string? numero) => new(
        Guid.NewGuid(), numero, "Remisión", DocumentStatus.Confirmed, new DateOnly(2026, 11, 23), null,
        new EmpresaDeLaCotizacion("Cooperativa", "900", null, null, null), new ClienteDeLaCotizacion("Cliente", null, null, null, null, null, null),
        null, [], [], [], 0m, 0m, 0m, 0m, 0m, 0m, null, null, null, titulo);
}
