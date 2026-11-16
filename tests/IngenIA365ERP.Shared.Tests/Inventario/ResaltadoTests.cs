using Xunit;
using FluentAssertions;
using IngenIA365ERP.Shared.Components.Shared;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>2026-11-15: lo que coincide con la búsqueda se marca término por término, sin tildes ni mayúsculas, en las posiciones del texto original.</summary>
public class ResaltadoTests
{
    [Fact]
    public void Marca_cada_termino_sin_tildes_ni_mayusculas()
    {
        var partes = Resaltado.Partes("ACEITE CASTROL-GTX 20W50", "castrol 20w");

        partes.Should().Equal(("ACEITE ", false), ("CASTROL", true), ("-GTX ", false), ("20W", true), ("50", false));
    }

    [Fact]
    public void Las_tildes_del_texto_no_corren_las_posiciones()
    {
        Resaltado.Partes("Lubricación sintética", "sintetica").Should().Equal(("Lubricación ", false), ("sintética", true));
    }

    [Fact]
    public void Sin_busqueda_el_texto_va_tal_cual_y_coincide_dice_si_hay_alguno()
    {
        Resaltado.Partes("0101001", null).Should().Equal(("0101001", false));
        Resaltado.Coincide("0101001", "0101").Should().BeTrue();
        Resaltado.Coincide("ACEITE", "castrol").Should().BeFalse();
        Resaltado.Coincide(null, "castrol").Should().BeFalse();
    }
}
