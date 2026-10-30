using FluentAssertions;
using IngenIA365ERP.Domain.Common.Cuadres;
using Xunit;

namespace IngenIA365ERP.Domain.Tests.Common;

/// <summary>
/// Feature 015, US13 (T0288; FR-158, FR-228): el motor puro del cuadre de apertura. Una línea comparable cuadra con diferencia cero o sin carga ni dato; la informativa
/// siempre cuadra y no suma; el definitivo sin diferencia es <c>Squared</c>, con diferencia sólo con acta; la fecha límite del provisional va después del corte.
/// Prueba escrita, sin correr.
/// </summary>
public class CalculoDelCuadreDeAperturaTests
{
    [Fact]
    public void Calcula_diferencias_por_linea_y_totales_solo_de_las_comparables()
    {
        var c = CalculoDelCuadreDeApertura.Calcular(
        [
            new LineaPropuestaDeCuadre("Banco", "BCO01", null, 1_000m, 1_000m),
            new LineaPropuestaDeCuadre("Boveda", "001", null, 500m, 450m),
            new LineaPropuestaDeCuadre("ChequesGiradosEnTransito", "BCO01", null, 300m, null, Comparable: false),
        ]);

        c.TotalCargado.Should().Be(1_500m);
        c.TotalContable.Should().Be(1_450m);
        c.TotalDiferencia.Should().Be(50m);
        c.Cuadra.Should().BeFalse();
        c.Lineas.Single(l => l.Concepto == "ChequesGiradosEnTransito").Cuadra.Should().BeTrue("la informativa no se compara");
    }

    [Theory]
    [InlineData(0, null, true)]
    [InlineData(100, null, false)]
    [InlineData(100, 100, true)]
    [InlineData(100, 99, false)]
    public void Una_linea_sin_dato_cuadra_solo_si_no_hay_nada_cargado(int cargado, int? contable, bool cuadra) =>
        CalculoDelCuadreDeApertura.Calcular([new LineaPropuestaDeCuadre("Boveda", "001", null, cargado, contable)]).Cuadra.Should().Be(cuadra);

    [Fact]
    public void El_definitivo_con_diferencia_exige_acta()
    {
        var conDiferencia = CalculoDelCuadreDeApertura.Calcular([new LineaPropuestaDeCuadre("Boveda", "001", null, 500m, 450m)]);
        CalculoDelCuadreDeApertura.EstadoDelDefinitivo(conDiferencia, conActa: false).Should().BeNull();
        CalculoDelCuadreDeApertura.EstadoDelDefinitivo(conDiferencia, conActa: true).Should().Be(OpeningSquareState.AcceptedWithMinutes);
        var cuadrado = CalculoDelCuadreDeApertura.Calcular([new LineaPropuestaDeCuadre("Boveda", "001", null, 500m, 500m)]);
        CalculoDelCuadreDeApertura.EstadoDelDefinitivo(cuadrado, conActa: false).Should().Be(OpeningSquareState.Squared);
    }

    [Fact]
    public void La_fecha_limite_del_provisional_va_despues_del_corte()
    {
        CalculoDelCuadreDeApertura.FechaLimiteValida(new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 30)).Should().BeFalse();
        CalculoDelCuadreDeApertura.FechaLimiteValida(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 4)).Should().BeTrue();
    }
}
