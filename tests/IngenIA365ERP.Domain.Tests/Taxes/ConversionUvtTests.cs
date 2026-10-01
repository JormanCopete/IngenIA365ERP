using FluentAssertions;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Tests.Taxes;

/// <summary>
/// Feature 012, T099 (T19, T23; FR-013): una base mínima en UVT se convierte a pesos con la UVT vigente y el redondeo
/// de <c>Tributario.RedondeoUvtAPesos</c> (<c>Peso</c>, <c>Centena</c>, <c>Mil</c>), siempre
/// <see cref="MidpointRounding.AwayFromZero"/>; y la retención procede cuando la base es <b>igual o superior</b> al
/// mínimo.
/// </summary>
public class ConversionUvtTests
{
    private const decimal Uvt2026 = 52_374m;

    [Theory]
    [InlineData(RedondeoUvt.Peso, 1_414_098)]     // 27 × 52.374 = 1.414.098
    [InlineData(RedondeoUvt.Centena, 1_414_100)]
    [InlineData(RedondeoUvt.Mil, 1_414_000)]
    public void Convierte_la_base_minima_a_pesos_con_el_redondeo_elegido(RedondeoUvt redondeo, int esperado)
    {
        ConversionUvt.APesos(Uvt2026, 27m, redondeo).Should().Be(esperado);
    }

    [Theory]
    [InlineData(RedondeoUvt.Peso, 0.5, 1)]          // media unidad se aleja de cero
    [InlineData(RedondeoUvt.Centena, 50, 100)]
    [InlineData(RedondeoUvt.Mil, 500, 1_000)]
    [InlineData(RedondeoUvt.Mil, 499.99, 0)]
    public void El_punto_medio_se_aleja_de_cero(RedondeoUvt redondeo, double enPesos, int esperado)
    {
        // Una UVT de 1 peso deja el valor en UVT igual al valor en pesos.
        ConversionUvt.APesos(1m, (decimal)enPesos, redondeo).Should().Be(esperado);
    }

    [Fact]
    public void Una_base_en_UVT_con_decimales_tambien_se_redondea()
    {
        // 4 UVT de servicios con una UVT de 49.799: 199.196.
        ConversionUvt.APesos(49_799m, 4m, RedondeoUvt.Peso).Should().Be(199_196m);
        // 0,5 UVT: 26.187 exactos con la UVT 2026.
        ConversionUvt.APesos(Uvt2026, 0.5m, RedondeoUvt.Peso).Should().Be(26_187m);
    }

    [Theory]
    [InlineData(1_414_098, true)]    // igual: sí («igual o superior», FR-013)
    [InlineData(1_414_099, true)]
    [InlineData(1_414_097, false)]
    public void Procede_con_base_igual_o_superior_al_minimo(int baseGravable, bool procede)
    {
        ConversionUvt.Procede(baseGravable, 1_414_098m).Should().Be(procede);
    }

    [Theory]
    [InlineData("Peso", RedondeoUvt.Peso)]
    [InlineData("Centena", RedondeoUvt.Centena)]
    [InlineData("mil", RedondeoUvt.Mil)]
    [InlineData(null, RedondeoUvt.Peso)]
    [InlineData("", RedondeoUvt.Peso)]
    public void Lee_el_valor_del_parametro(string? valor, RedondeoUvt esperado)
    {
        ConversionUvt.Redondeo(valor).Should().Be(esperado);
    }
}
