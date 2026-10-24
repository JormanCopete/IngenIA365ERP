using FluentAssertions;
using IngenIA365ERP.Domain.Common.Taxation;

namespace IngenIA365ERP.Domain.Tests.Common.Taxation;

/// <summary>
/// Feature 015, T0125 (R-25; FR-100): el cálculo común del GMF, puro y en decimal. La tarifa llega como fracción; la cero da cero; al peso redondea con la mitad
/// hacia arriba; sin redondeo deja dos decimales; base negativa o tarifa fuera de [0, 1] es un error de programación. Prueba escrita, sin correr.
/// </summary>
public class CalculoDeGmfTests
{
    [Theory]
    [InlineData(1_000_000, 0.004, RedondeoDeGmf.AlPeso, 4_000)]
    [InlineData(123_625, 0.004, RedondeoDeGmf.AlPeso, 495)]
    [InlineData(123_625, 0.004, RedondeoDeGmf.SinRedondeo, 494.5)]
    [InlineData(123_624, 0.004, RedondeoDeGmf.AlPeso, 494)]
    [InlineData(500_000, 0, RedondeoDeGmf.AlPeso, 0)]
    [InlineData(0, 0.004, RedondeoDeGmf.AlPeso, 0)]
    public void Calcula_con_la_tarifa_como_fraccion(double baseGravable, double tarifa, RedondeoDeGmf redondeo, double esperado) =>
        CalculoDeGmf.Calcular((decimal)baseGravable, (decimal)tarifa, redondeo).Should().Be((decimal)esperado);

    [Fact]
    public void Rechaza_base_negativa_y_tarifa_fuera_de_rango()
    {
        var negativa = () => CalculoDeGmf.Calcular(-1m, 0.004m, RedondeoDeGmf.AlPeso);
        var mayor = () => CalculoDeGmf.Calcular(1m, 1.5m, RedondeoDeGmf.AlPeso);
        negativa.Should().Throw<ArgumentOutOfRangeException>();
        mayor.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("SinRedondeo", RedondeoDeGmf.SinRedondeo)]
    [InlineData("AlPeso", RedondeoDeGmf.AlPeso)]
    [InlineData(null, RedondeoDeGmf.AlPeso)]
    [InlineData("otro", RedondeoDeGmf.AlPeso)]
    public void Lee_el_parametro_de_redondeo(string? texto, RedondeoDeGmf esperado) => CalculoDeGmf.Redondeo(texto).Should().Be(esperado);
}
