using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Regulatory;

namespace IngenIA365ERP.Domain.Tests.Lending.Regulatory;

/// <summary>
/// Decisión del dueño del 2026-10-16 (feature 013, K5, US16): los rangos de mora del reporte a centrales por tipo de cartera, con el rango desde el que
/// el dato es negativo, leídos del parámetro <c>Centrales.RangosDeMora</c>. El defecto reproduce el comportamiento de antes: toda mora es negativa.
/// (nuevo)
/// </summary>
public class RangosDeMoraParaCentralesTests
{
    [Fact]
    public void El_defecto_hace_negativa_toda_mora_mayor_que_cero()
    {
        var consumo = RangosDeMoraParaCentrales.PorDefecto.Para("CONSUMO");

        consumo.TipoDeCartera.Should().Be(RangosDeMoraParaCentrales.Comodin);
        consumo.EsNegativo(0).Should().BeFalse("cero días es al día");
        consumo.EsNegativo(1).Should().BeTrue();
        consumo.RangoDe(1)!.Should().Be(new RangoDeMora(1, 1, 30));
        consumo.RangoDe(60)!.Numero.Should().Be(2);
        consumo.RangoDe(181)!.Should().Be(new RangoDeMora(7, 181, null));
        consumo.RangoDe(181)!.Etiqueta.Should().Be("181+");
        consumo.RangoDe(45)!.Etiqueta.Should().Be("31-60");
        consumo.RangoDe(0).Should().BeNull();
    }

    [Fact]
    public void Cada_tipo_de_cartera_tiene_sus_rangos_y_su_rango_negativo()
    {
        var r = RangosDeMoraParaCentrales.Leer("*=30,60,90,120|2;VIVIENDA=60,120,180|1")!;

        var consumo = r.Para("consumo");
        consumo.EsNegativo(30).Should().BeFalse("el comodín es negativo desde el rango 2 (31-60)");
        consumo.EsNegativo(31).Should().BeTrue();
        var vivienda = r.Para("VIVIENDA");
        vivienda.RangoDe(45)!.Should().Be(new RangoDeMora(1, 1, 60));
        vivienda.EsNegativo(45).Should().BeTrue();
        vivienda.Rangos.Select(x => x.Etiqueta).Should().Equal("1-60", "61-120", "121-180", "181+");
    }

    [Fact]
    public void Sin_comodin_un_tipo_que_no_se_nombra_usa_el_del_defecto()
    {
        var r = RangosDeMoraParaCentrales.Leer("VIVIENDA=60,120|3")!;

        r.Para("VIVIENDA").EsNegativo(100).Should().BeFalse("vivienda es negativa desde el rango 3 (121+)");
        r.Para("MICROCREDITO").Should().Be(RangosDeMoraParaCentrales.PorDefecto.PorTipo[RangosDeMoraParaCentrales.Comodin]);
        r.Para(null).EsNegativo(1).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("CONSUMO=30,60")]
    [InlineData("consumo=30|1")]
    [InlineData("*=30,30|1")]
    [InlineData("*=60,30|1")]
    [InlineData("*=0,30|1")]
    [InlineData("*=30,60|0")]
    [InlineData("*=30,60|4")]
    [InlineData("*=30|1;*=60|1")]
    public void Un_texto_sin_la_forma_o_sin_la_regla_no_se_admite(string texto)
    {
        RangosDeMoraParaCentrales.Leer(texto).Should().BeNull();
        RangosDeMoraParaCentrales.Validar(texto).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void El_ultimo_rango_abierto_puede_ser_el_negativo()
    {
        RangosDeMoraParaCentrales.Validar("*=30,60|3").Should().BeNull();
        RangosDeMoraParaCentrales.Leer("*=30,60|3")!.Para("X").EsNegativo(61).Should().BeTrue();
    }
}
