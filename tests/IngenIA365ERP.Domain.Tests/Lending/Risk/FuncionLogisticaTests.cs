using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a (T1012; research R55, T26): <see cref="FuncionLogistica"/> y la exponencial de <see cref="AritmeticaDecimal"/>, sólo en
/// <c>decimal</c>. La referencia de cada punto se calculó con <c>decimal</c> de 60 dígitos de Python (<c>Casos/riesgo-pe.py</c>, sin el C#) y
/// se redondeó a 18 decimales. Que el código no use coma flotante lo vigila <c>ElMotorDeCarteraEsDecimal</c> sobre el fuente.
/// </summary>
public class FuncionLogisticaTests
{
    [Theory]
    [InlineData("-50", "0.000000000000000000")]
    [InlineData("-20", "0.000000002061153618")]
    [InlineData("-10", "0.000045397868702434")]
    [InlineData("-3.133", "0.041766376028679083")]
    [InlineData("-2.189", "0.100742650970096172")]
    [InlineData("-1.523", "0.179020178890809887")]
    [InlineData("-1", "0.268941421369995121")]
    [InlineData("-0.5", "0.377540668798145435")]
    [InlineData("-0.001", "0.499750000020833331")]
    [InlineData("0", "0.500000000000000000")]
    [InlineData("0.001", "0.500249999979166669")]
    [InlineData("0.5", "0.622459331201854565")]
    [InlineData("1", "0.731058578630004879")]
    [InlineData("2.495", "0.923790557201660213")]
    [InlineData("3.722", "0.976385579366252159")]
    [InlineData("10", "0.999954602131297566")]
    [InlineData("20", "0.999999997938846382")]
    [InlineData("50", "1.000000000000000000")]
    public void Coincide_con_la_referencia_a_18_decimales(string z, string esperado)
    {
        var f = FuncionLogistica.Evaluar(decimal.Parse(z, System.Globalization.CultureInfo.InvariantCulture));
        decimal.Round(f, 18, MidpointRounding.ToEven).Should().Be(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Es_simetrica_monotona_y_queda_en_el_intervalo_abierto()
    {
        var anterior = 0m;
        for (var z = -50m; z <= 50m; z += 0.25m)
        {
            var f = FuncionLogistica.Evaluar(z);
            f.Should().BeGreaterThan(0m, $"z = {z}").And.BeLessThan(1m, $"z = {z}");
            (FuncionLogistica.Evaluar(-z) + f).Should().Be(1m, $"f(−z) = 1 − f(z) en z = {z}");
            f.Should().BeGreaterThanOrEqualTo(anterior, $"monótona en z = {z}");
            anterior = f;
        }
        FuncionLogistica.Evaluar(0m).Should().Be(0.5m);
    }

    [Fact]
    public void Fuera_del_rango_de_decimal_vale_cero_o_uno_sin_desbordarse()
    {
        FuncionLogistica.Evaluar(500m).Should().Be(1m);
        FuncionLogistica.Evaluar(-500m).Should().Be(0m);
    }

    [Theory]
    [InlineData("0.5", "1.648721270700128146848650787814")]
    [InlineData("1", "2.718281828459045235360287471353")]
    [InlineData("2.5", "12.182493960703473438070175951168")]
    [InlineData("-3.7", "0.024723526470339391202757382983")]
    [InlineData("20", "485165195.409790277969106830541541")]
    public void La_exponencial_reduce_el_argumento_y_suma_la_serie(string x, string referencia)
    {
        var valor = AritmeticaDecimal.Exponencial(decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
        var esperado = decimal.Parse(referencia, System.Globalization.CultureInfo.InvariantCulture);
        Math.Abs(valor - esperado).Should().BeLessThan(Math.Abs(esperado) * 0.000000000000000000000001m, "24 cifras significativas");
    }

    [Fact]
    public void La_exponencial_de_cero_es_uno_y_la_de_un_negativo_enorme_es_cero()
    {
        AritmeticaDecimal.Exponencial(0m).Should().Be(1m);
        AritmeticaDecimal.Exponencial(-1000m).Should().Be(0m);
        var accion = () => AritmeticaDecimal.Exponencial(1000m);
        accion.Should().Throw<OverflowException>();
        decimal.Round(AritmeticaDecimal.E, 24).Should().Be(2.718281828459045235360287m);
    }

    [Fact]
    public void El_puntaje_se_redondea_a_la_escala_del_documento_y_nada_mas()
    {
        // E9: la función no redondea; el motor lleva el puntaje a la escala del Anexo 2 (4 decimales en la versión sembrada).
        var f = FuncionLogistica.Evaluar(-1.523m);
        f.Scale.Should().BeGreaterThan(18);
        decimal.Round(f, 4, MidpointRounding.AwayFromZero).Should().Be(0.1790m);
    }
}
