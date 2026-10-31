using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>FR-035: el aviso de alcance de sucursal en los filtros de los informes contables. (nuevo)</summary>
public class MiAlcanceDeSucursalTests
{
    [Fact]
    public void Sin_restriccion_no_avisa() =>
        new MiAlcanceDeSucursalDto(false, []).Aviso.Should().BeNull();

    [Fact]
    public void Sin_sucursales_explica_por_que_sale_vacio_y_a_donde_ir()
    {
        var a = new MiAlcanceDeSucursalDto(true, []);

        a.SinSucursales.Should().BeTrue();
        a.Aviso.Should().Contain("salen vacíos").And.Contain("Maestros › Agencias");
    }

    [Fact]
    public void Con_sucursales_las_nombra() =>
        new MiAlcanceDeSucursalDto(true, ["Centro", "Norte"]).Aviso.Should().Be("Ve sólo los movimientos de: Centro, Norte.");
}
