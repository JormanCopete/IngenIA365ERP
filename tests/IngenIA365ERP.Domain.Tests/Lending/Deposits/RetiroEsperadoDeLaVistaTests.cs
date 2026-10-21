using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>El retiro esperado del ahorro a la vista con la historia (respuesta 100 B del dueño, E250): percentil al nivel de confianza por la raíz del horizonte.</summary>
public class RetiroEsperadoDeLaVistaTests
{
    [Fact]
    public void El_percentil_95_de_cien_dias_es_el_dia_95_ordenado_y_se_escala_por_la_raiz_de_30()
    {
        // Cien días de 1.000 a 100.000: el día 95 por rango más cercano es 95.000.
        var salidas = Enumerable.Range(1, 100).Select(i => i * 1_000m).Reverse().ToList();

        var esperado = RetiroEsperadoDeLaVista.Calcular(salidas, 0.95m, 30);

        esperado.Should().Be(decimal.Round(95_000m * (decimal)Math.Sqrt(30), 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Un_dia_con_mas_consignado_que_retirado_cuenta_cero_y_sin_dias_el_retiro_es_cero()
    {
        RetiroEsperadoDeLaVista.Calcular([-5_000m, -1m, -9m], 0.95m, 30).Should().Be(0m);
        RetiroEsperadoDeLaVista.Calcular([], 0.95m, 30).Should().Be(0m);
    }

    [Fact]
    public void La_fraccion_nunca_pasa_de_uno_y_sin_saldo_es_cero()
    {
        RetiroEsperadoDeLaVista.Fraccion(2_000m, 1_000m).Should().Be(1m);
        RetiroEsperadoDeLaVista.Fraccion(100m, 1_000m).Should().Be(0.1m);
        RetiroEsperadoDeLaVista.Fraccion(100m, 0m).Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void La_confianza_va_entre_cero_y_uno_sin_incluirlos(double confianza)
    {
        var acto = () => RetiroEsperadoDeLaVista.Calcular([1m], (decimal)confianza, 30);

        acto.Should().Throw<ArgumentOutOfRangeException>();
    }
}
