using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// Revisión E2 de la feature 009, hallazgo 13: el clic en un rubro de los estados financieros
/// lleva al libro auxiliar con <c>?niifItem=</c>, y el libro tiene que leerlo de vuelta, contarlo
/// entre los filtros puestos (el título del panel dice «Filtros (n)») y mandarlo a la API con el
/// nombre del contrato. Hasta el 2026-09-20 la pantalla buscaba en las filas una clave que la API
/// nunca mandaba y ninguna fila era clicable.
/// </summary>
public class FiltroPorRubroEnPantallaTests
{
    [Fact]
    public void El_rubro_viaja_como_niifItem_en_mayusculas_y_vuelve_de_la_query_string()
    {
        var m = new FiltrosDeInformeModelo { From = new DateOnly(2026, 1, 1), To = new DateOnly(2026, 3, 31), NiifItem = " esf-a-efe " };

        var q = m.ToQuery();
        q.Should().Contain("niifItem=ESF-A-EFE");

        var vuelta = FiltrosDeInformeModelo.DesdeQuery("/contabilidad/libro-auxiliar?" + q);
        vuelta.NiifItem.Should().Be("ESF-A-EFE");
        vuelta.From.Should().Be(new DateOnly(2026, 1, 1));
        vuelta.ToQuery().Should().Be(q);
    }

    [Fact]
    public void El_rubro_cuenta_como_filtro_puesto_se_copia_y_en_blanco_no_existe()
    {
        new FiltrosDeInformeModelo { NiifItem = "ERI-ING" }.Cantidad.Should().Be(1);
        new FiltrosDeInformeModelo { NiifItem = "   " }.Cantidad.Should().Be(0);
        new FiltrosDeInformeModelo { NiifItem = "   " }.ToQuery().Should().BeEmpty();

        var copia = new FiltrosDeInformeModelo { NiifItem = "ERI-ING" }.Copia();
        copia.NiifItem.Should().Be("ERI-ING");
    }
}
