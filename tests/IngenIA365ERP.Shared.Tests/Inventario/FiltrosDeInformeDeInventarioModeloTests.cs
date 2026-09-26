using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// T182 (página base <c>/inventario/informes</c>; contracts/api.md §27): los filtros de la pantalla van y vuelven por la
/// query string. Las fechas se editan en pantalla; lo demás (bodega, producto, filtros propios de la vista) llega por el
/// enlace y viaja tal cual a la consulta y a la exportación, para que profundizar desde otra pantalla no pierda nada.
/// <c>vista</c> y <c>format</c> no son filtros: los pone la pantalla.
/// </summary>
public class FiltrosDeInformeDeInventarioModeloTests
{
    [Fact]
    public void Las_fechas_y_los_demas_filtros_vuelven_de_la_query_y_se_arman_otra_vez()
    {
        var bodega = Guid.NewGuid();
        var m = FiltrosDeInformeDeInventarioModelo.DesdeQuery($"?vista=kardex&from=2026-09-01&to=2026-09-25&warehouse={bodega}&groupBy=customer&format=pdf");

        m.From.Should().Be(new DateOnly(2026, 9, 1));
        m.To.Should().Be(new DateOnly(2026, 9, 25));
        m.AsOf.Should().BeNull();
        m.Otros.Should().ContainKey("warehouse").WhoseValue.Should().Be(bodega.ToString());
        m.Otros.Should().ContainKey("groupBy").And.NotContainKey("vista").And.NotContainKey("format");

        m.ToQuery().Should().Be($"from=2026-09-01&to=2026-09-25&groupBy=customer&warehouse={bodega}");
    }

    [Fact]
    public void Lo_vacio_no_viaja_y_una_fecha_mal_escrita_se_ignora()
    {
        var m = FiltrosDeInformeDeInventarioModelo.DesdeQuery("from=ayer&asOf=2026-09-30&product=");

        m.From.Should().BeNull();
        m.AsOf.Should().Be(new DateOnly(2026, 9, 30));
        m.ToQuery().Should().Be("asOf=2026-09-30");
    }

    [Fact]
    public void La_copia_no_comparte_los_otros_filtros()
    {
        var m = FiltrosDeInformeDeInventarioModelo.DesdeQuery("warehouse=abc");
        var copia = m.Copia();
        copia.Otros["product"] = "xyz";

        m.Otros.Should().NotContainKey("product");
    }

    [Fact]
    public void Los_valores_se_escapan()
    {
        var m = new FiltrosDeInformeDeInventarioModelo();
        m.Otros["search"] = "a b&c";

        m.ToQuery().Should().Be("search=a%20b%26c");
    }
}
