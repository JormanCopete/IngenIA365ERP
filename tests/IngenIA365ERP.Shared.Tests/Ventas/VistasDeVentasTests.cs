using FluentAssertions;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>T643: el centro de informes de Ventas muestra las once vistas de I3, incluida la de deterioro, y no las de inventario. (nuevo)</summary>
public class VistasDeVentasTests
{
    [Fact]
    public void Son_las_once_de_I3_con_el_deterioro()
    {
        VistasDeVentas.Claves.Should().HaveCount(11).And.OnlyHaveUniqueItems().And.Contain("impairment").And.Contain("cash-session");
        VistasDeVentas.EsDeVentas("kardex").Should().BeFalse();
    }
}
