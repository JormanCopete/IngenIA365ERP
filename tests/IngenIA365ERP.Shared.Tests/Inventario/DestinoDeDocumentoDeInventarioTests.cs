using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// Feature 012, US17, T958: profundizar desde <c>/inventario/informes</c> —la columna oculta <c>_documento</c> abre la pantalla del
/// documento según su clase (ajustes, traslado por su despacho, recepción y factura de compra, conteo); <c>_producto</c> y
/// <c>_bodega</c> llevan al kardex—. Una clase sin pantalla de detalle no inventa una ruta.
/// </summary>
public class DestinoDeDocumentoDeInventarioTests
{
    private static readonly Guid Id = Guid.Parse("7a0f3c1e-2b4d-4e8f-9a01-23456789abcd");

    [Theory]
    [InlineData(10, "/inventario/ajustes/")]
    [InlineData(11, "/inventario/ajustes/")]
    [InlineData(12, "/inventario/ajustes/")]
    [InlineData(13, "/inventario/ajustes/")]
    [InlineData(16, "/inventario/traslados/")]
    [InlineData(19, "/inventario/conteos/")]
    [InlineData(3, "/compras/recepciones/")]
    [InlineData(4, "/compras/facturas-proveedor/")]
    public void Cada_clase_con_pantalla_abre_la_suya(int clase, string prefijo)
    {
        DestinoDeDocumentoDeInventario.Documento(clase, Id).Should().Be(prefijo + Id);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(34)]
    [InlineData(26)]
    public void Una_clase_sin_pantalla_de_detalle_no_tiene_destino(int clase)
    {
        DestinoDeDocumentoDeInventario.Documento(clase, Id).Should().BeNull();
    }

    [Fact]
    public void El_producto_y_la_bodega_llevan_al_kardex()
    {
        var bodega = Guid.NewGuid();
        DestinoDeDocumentoDeInventario.Kardex(Id.ToString(), bodega.ToString()).Should().Be($"/inventario/kardex?product={Id}&warehouse={bodega}");
        DestinoDeDocumentoDeInventario.Kardex(Id.ToString(), null).Should().Be($"/inventario/kardex?product={Id}");
        DestinoDeDocumentoDeInventario.Kardex("no-es-un-guid", null).Should().BeNull();
    }
}
