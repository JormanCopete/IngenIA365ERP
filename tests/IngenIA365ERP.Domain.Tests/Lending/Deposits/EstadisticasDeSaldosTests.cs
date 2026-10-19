using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US17 (T220, T223): la serie de saldos diarios (lo anterior al rango es el saldo inicial; cada movimiento cuenta desde su fecha valor) y el
/// resumen que piden el formato de saldos diarios, el promedio del 9013 y la exógena 1019 (promedio, mediana par e impar, máximo, mínimo), a mano.
/// </summary>
public class EstadisticasDeSaldosTests
{
    [Fact]
    public void La_serie_arranca_con_lo_anterior_y_suma_cada_dia_lo_suyo()
    {
        var serie = EstadisticasDeSaldos.SerieDiaria(
        [
            (new DateOnly(2026, 11, 20), 1_000m),
            (new DateOnly(2026, 12, 2), 500m),
            (new DateOnly(2026, 12, 2), -200m),
            (new DateOnly(2026, 12, 4), 100m),
            (new DateOnly(2026, 12, 9), 999m),
        ], new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5));

        serie.Select(d => d.Saldo).Should().Equal(1_000m, 1_300m, 1_300m, 1_400m, 1_400m);
        serie[0].Dia.Should().Be(new DateOnly(2026, 12, 1));
        serie[^1].Dia.Should().Be(new DateOnly(2026, 12, 5));
    }

    [Fact]
    public void Un_rango_al_reves_no_tiene_dias()
    {
        EstadisticasDeSaldos.SerieDiaria([(new DateOnly(2026, 1, 1), 1m)], new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31)).Should().BeEmpty();
    }

    [Fact]
    public void El_resumen_de_una_cantidad_impar_tiene_la_mediana_del_medio()
    {
        var r = EstadisticasDeSaldos.Resumir([300m, 100m, 200m]);
        r.Should().Be(new ResumenDeSerie(3, 600m, 200m, 200m, 300m, 100m));
    }

    [Fact]
    public void La_mediana_de_una_cantidad_par_es_el_promedio_de_los_dos_del_medio()
    {
        var r = EstadisticasDeSaldos.Resumir([10m, 40m, 20m, 30m]);
        r.Mediana.Should().Be(25m);
        r.Promedio.Should().Be(25m);
        r.Maximo.Should().Be(40m);
        r.Minimo.Should().Be(10m);
        r.Total.Should().Be(100m);
    }

    [Fact]
    public void Una_serie_vacia_resume_en_ceros_y_los_pesos_redondean_la_mitad_hacia_arriba()
    {
        EstadisticasDeSaldos.Resumir([]).Should().Be(new ResumenDeSerie(0, 0m, 0m, 0m, 0m, 0m));
        EstadisticasDeSaldos.APesos(1_234.5m).Should().Be(1_235m);
        EstadisticasDeSaldos.APesos(1_234.49m).Should().Be(1_234m);
        EstadisticasDeSaldos.APesos(-10.5m).Should().Be(-11m);
    }
}
