using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Tests.Lending.Rates;

/// <summary>
/// Feature 013, T0130 (T26, M5; FR-010): la aritmética exacta del motor de Cartera. Los valores esperados se calcularon con
/// <c>decimal</c> de 60 dígitos (módulo <c>decimal</c> de Python, Newton hasta 10^-55) y se escriben aquí redondeados a 24 decimales
/// con redondeo al par; nunca con una hoja de cálculo. Ningún resultado pasa por <c>double</c>.
/// </summary>
public class AritmeticaDecimalTests
{
    [Fact]
    public void La_potencia_entera_por_cuadrados_es_exacta()
    {
        AritmeticaDecimal.Potencia(1.5m, 10).Should().Be(57.6650390625m);
        AritmeticaDecimal.Potencia(1.01m, 12).Should().Be(1.126825030131969720661201m);
        AritmeticaDecimal.Potencia(2m, 0).Should().Be(1m);
        AritmeticaDecimal.Potencia(0.5m, -3).Should().Be(8m);
        AritmeticaDecimal.Potencia(-3m, 3).Should().Be(-27m);
    }

    [Fact]
    public void Cero_con_exponente_negativo_no_tiene_potencia()
    {
        var accion = () => AritmeticaDecimal.Potencia(0m, -1);
        accion.Should().Throw<DivideByZeroException>();
    }

    [Theory]
    // (1 + EA)^(1/B) con EA 26,5 % y 0,0001 %, B = 360 y 365.
    [InlineData("1.265", 360, "1.000653191353786403900922")]
    [InlineData("1.265", 365, "1.000644240645368419592772")]
    [InlineData("1.000001", 360, "1.000000002777776392747836")]
    [InlineData("1.000001", 365, "1.000000002739724661288205")]
    public void La_raiz_entera_por_Newton_llega_a_24_decimales(string x, int n, string esperada)
    {
        var raiz = AritmeticaDecimal.RaizEntera(decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture), n);

        raiz.Should().Be(decimal.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));
        decimal.Round(raiz, AritmeticaDecimal.DecimalesDeRaiz).Should().Be(raiz, "la raíz ya viene redondeada a 24 decimales");
    }

    [Fact]
    public void La_raiz_de_uno_y_la_de_indice_uno_son_triviales()
    {
        AritmeticaDecimal.RaizEntera(1m, 365).Should().Be(1m);
        AritmeticaDecimal.RaizEntera(1.265m, 1).Should().Be(1.265m);
        AritmeticaDecimal.RaizEntera(0m, 12).Should().Be(0m);
    }

    [Fact]
    public void La_raiz_rechaza_negativos_e_indices_menores_que_uno()
    {
        ((Action)(() => AritmeticaDecimal.RaizEntera(-1m, 2))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => AritmeticaDecimal.RaizEntera(2m, 0))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void La_TIR_por_Newton_coincide_con_la_calculada_a_60_digitos()
    {
        // Un millón prestado y doce cuotas de 88.848,79: la tasa mensual de la cuota fija.
        decimal[] flujos = [-1_000_000m, .. Enumerable.Repeat(88_848.79m, 12)];

        var tir = AritmeticaDecimal.Tir(flujos);

        tir.Metodo.Should().Be(MetodoDeTir.Newton);
        Math.Abs(tir.Tasa - 0.010000002354341354697344m).Should().BeLessThan(0.00000000000000000001m);
    }

    [Fact]
    public void Si_Newton_no_converge_la_TIR_cae_a_biseccion()
    {
        decimal[] flujos = [-500_000m, 100_000m, 150_000m, 200_000m, 120_000m];

        // Una semilla fuera del dominio (1 + r ≤ 0) hace que Newton se rinda en el primer paso.
        var tir = AritmeticaDecimal.Tir(flujos, semilla: -2m);

        tir.Metodo.Should().Be(MetodoDeTir.Biseccion);
        Math.Abs(tir.Tasa - 0.052292893386198721224817m).Should().BeLessThan(0.00000000000000000001m);
        AritmeticaDecimal.Tir(flujos).Tasa.Should().BeApproximately(tir.Tasa, 0.00000000000000000001m,
            "Newton desde la semilla por defecto llega a la misma tasa");
    }

    [Fact]
    public void La_TIR_exige_flujos_de_los_dos_signos()
    {
        ((Action)(() => AritmeticaDecimal.Tir([100m, 200m]))).Should().Throw<ArgumentException>();
    }
}
