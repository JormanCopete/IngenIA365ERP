using FluentAssertions;
using IngenIA365ERP.Shared.Services.Pagos;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Pagos;

/// <summary>
/// Feature 022 (T053; FR-007): el valor en pesos se lee sin la ambigüedad del <c>type=number</c>: «50.000» son cincuenta mil, no
/// cincuenta, y lo que no se entiende se dice en vez de adivinarse.
/// </summary>
public class LecturaDeValorTests
{
    [Theory]
    [InlineData("50000", 50000)]
    [InlineData("50.000", 50000)]
    [InlineData("50 000", 50000)]
    [InlineData("1.250.000", 1250000)]
    [InlineData("1,250,000", 1250000)]
    [InlineData("$ 2.000", 2000)]
    [InlineData("  7 ", 7)]
    public void En_pesos_los_separadores_son_de_miles(string escrito, int esperado)
    {
        var (valido, valor, _) = LecturaDeValor.Leer(escrito);

        valido.Should().BeTrue();
        valor.Should().Be(esperado);
    }

    [Theory]
    [InlineData("12.5")]
    [InlineData("1.234,56")]
    [InlineData("12.34.5")]
    [InlineData("abc")]
    [InlineData("1.234,567")]
    public void En_pesos_lo_que_no_es_un_entero_no_se_adivina(string escrito)
    {
        var (valido, valor, motivo) = LecturaDeValor.Leer(escrito);

        valido.Should().BeFalse();
        valor.Should().BeNull();
        motivo.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("12,5", "12.5")]
    [InlineData("12.5", "12.5")]
    [InlineData("12.500", "12500")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("0,25", "0.25")]
    public void Con_decimales_la_coma_es_decimal_y_el_punto_de_miles_si_arma_grupos(string escrito, string esperado)
    {
        var (valido, valor, _) = LecturaDeValor.Leer(escrito, decimales: 2);

        valido.Should().BeTrue();
        valor.Should().Be(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Mas_decimales_de_los_admitidos_se_dice()
    {
        LecturaDeValor.Leer("1,234", decimales: 2).Valido.Should().BeFalse();
    }

    [Fact]
    public void Vacio_es_nulo_y_es_valido()
    {
        LecturaDeValor.Leer("   ").Should().Be((true, (decimal?)null, (string?)null));
    }

    [Fact]
    public void El_negativo_solo_si_se_admite()
    {
        LecturaDeValor.Leer("-5.000").Valido.Should().BeFalse();
        LecturaDeValor.Leer("-5.000", permiteNegativos: true).Valor.Should().Be(-5000m);
    }

    [Theory]
    [InlineData(50000, 0, "50.000")]
    [InlineData(1234.5, 2, "1.234,50")]
    public void Se_muestra_con_los_miles_de_Colombia(decimal valor, int decimales, string esperado)
    {
        LecturaDeValor.Mostrar(valor, decimales).Should().Be(esperado);
    }

    [Theory]
    [InlineData(46782, "46.782")]
    [InlineData(46782.35, "46.782,35")]
    [InlineData(0.5, "0,50")]
    public void Un_monto_del_cobro_muestra_sus_centavos_solo_si_los_tiene(decimal valor, string esperado)
    {
        // 2026-11-12: con «N0» un total de 46.782,35 se veía 46.782 y lo escrito nunca cuadraba.
        LecturaDeValor.MostrarMonto(valor).Should().Be(esperado);
    }

    [Fact]
    public void Un_monto_del_cobro_se_escribe_con_sus_centavos()
    {
        LecturaDeValor.Leer("46.782,35", LecturaDeValor.DecimalesDeMonto).Valor.Should().Be(46782.35m);
        LecturaDeValor.Leer("46.782", LecturaDeValor.DecimalesDeMonto).Valor.Should().Be(46782m, "el punto con tres dígitos sigue siendo de miles");
        LecturaDeValor.Leer("50000", LecturaDeValor.DecimalesDeMonto).Valor.Should().Be(50000m);
    }
}
