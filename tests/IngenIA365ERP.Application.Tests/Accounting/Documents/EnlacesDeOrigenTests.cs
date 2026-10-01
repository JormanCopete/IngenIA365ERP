using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;

namespace IngenIA365ERP.Application.Tests.Accounting.Documents;

/// <summary>Feature 012, T493 (contracts/api.md §29): «Ver en Inventario» desde un comprobante de origen INV.</summary>
public class EnlacesDeOrigenTests
{
    [Fact]
    public void Los_origenes_de_inventario_llevan_a_su_pantalla()
    {
        var id = Guid.NewGuid();
        EnlacesDeOrigen.Ruta("InventoryDocument", id).Should().Be($"/inventario/documentos/{id}");
        EnlacesDeOrigen.Ruta("InventoryPostingBatch", id).Should().Be($"/contabilidad/inventario/lotes/{id}");
        EnlacesDeOrigen.Ruta("InventoryProduct", id).Should().Be($"/inventario/productos/{id}");
        EnlacesDeOrigen.Ruta("InventoryDocument", null).Should().BeNull();
        EnlacesDeOrigen.Ruta("PayrollRun", id).Should().Be($"/nomina/liquidacion?corrida={id}", "lo de la 009 no cambia");
    }
}
