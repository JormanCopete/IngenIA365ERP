using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9 (T0705; contracts/mensajes.md §10.17, C08): el deterioro del cierre se contabiliza <b>en delta</b> frente al cierre anterior; una
/// subida es <c>Increase</c> y una bajada se parte, por defecto, primero contra lo que se gastó en el ejercicio (<c>DecreaseSameYear</c>) y el resto a
/// recuperación de ejercicios anteriores (<c>DecreasePriorYears</c>); con <c>TodoARecuperacion</c> toda la bajada va a recuperación.
/// </summary>
public class MovimientosDelDeterioroTests
{
    [Fact]
    public void Una_subida_es_aumento_y_sin_cambio_no_hay_partidas()
    {
        var m = MovimientosDelDeterioro.Clasificar(100m, 150m, 0m, DisminucionDeDeterioro.MismoEjercicioPrimero);

        m.Should().Be(new MovimientoDelDeterioro(50m, 0m, 0m));
        m.Partidas().Should().Equal((MovimientosDelDeterioro.Aumentar, 50m));
        MovimientosDelDeterioro.Clasificar(100m, 100m, 30m, DisminucionDeDeterioro.MismoEjercicioPrimero).Partidas().Should().BeEmpty();
    }

    [Fact]
    public void Una_bajada_va_primero_contra_el_gasto_del_ejercicio_y_el_resto_a_recuperacion()
    {
        var m = MovimientosDelDeterioro.Clasificar(200m, 50m, 60m, DisminucionDeDeterioro.MismoEjercicioPrimero);

        m.Should().Be(new MovimientoDelDeterioro(0m, 60m, 90m));
        (m.Disminucion, m.Neto).Should().Be((150m, -150m));
        m.Partidas().Should().Equal((MovimientosDelDeterioro.DisminuirDelEjercicio, 60m), (MovimientosDelDeterioro.DisminuirDeEjerciciosAnteriores, 90m));
    }

    [Fact]
    public void Con_TodoARecuperacion_la_bajada_entera_es_de_ejercicios_anteriores()
    {
        MovimientosDelDeterioro.Clasificar(200m, 50m, 60m, DisminucionDeDeterioro.TodoARecuperacion).Should().Be(new MovimientoDelDeterioro(0m, 0m, 150m));
    }

    [Fact]
    public void El_gasto_del_ejercicio_se_reconstruye_de_la_historia_de_cierres()
    {
        // Apertura del ejercicio 100; sube a 180 (+80 de gasto), baja a 150 (-30 contra el gasto, quedan 50), sube a 170 (+20, 70).
        IReadOnlyList<decimal> historia = [100m, 180m, 150m, 170m];

        MovimientosDelDeterioro.GastoDelEjercicio(historia, DisminucionDeDeterioro.MismoEjercicioPrimero).Should().Be(70m);
        MovimientosDelDeterioro.GastoDelEjercicio(historia, DisminucionDeDeterioro.TodoARecuperacion).Should().Be(100m);
        MovimientosDelDeterioro.DelCierre(historia, 40m, DisminucionDeDeterioro.MismoEjercicioPrimero).Should().Be(new MovimientoDelDeterioro(0m, 70m, 60m));
    }

    [Theory]
    [InlineData("TodoARecuperacion", DisminucionDeDeterioro.TodoARecuperacion)]
    [InlineData("MismoEjercicioPrimero", DisminucionDeDeterioro.MismoEjercicioPrimero)]
    [InlineData(null, DisminucionDeDeterioro.MismoEjercicioPrimero)]
    [InlineData("otra", DisminucionDeDeterioro.MismoEjercicioPrimero)]
    public void La_regla_se_lee_del_parametro_con_el_defecto_seguro(string? texto, DisminucionDeDeterioro esperada) =>
        MovimientosDelDeterioro.Regla(texto).Should().Be(esperada);

    [Fact]
    public void Un_gasto_negativo_o_una_historia_vacia_son_errores_de_programa()
    {
        FluentActions.Invoking(() => MovimientosDelDeterioro.Clasificar(1m, 0m, -1m, DisminucionDeDeterioro.MismoEjercicioPrimero))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => MovimientosDelDeterioro.DelCierre([], 0m, DisminucionDeDeterioro.MismoEjercicioPrimero)).Should().Throw<ArgumentException>();
    }
}

/// <summary>
/// Feature 013, US9 (T0705): el deterioro de intereses y de otros conceptos de una línea se reparte entre sus conceptos según los saldos (corriente
/// antes que mora; seguro, cobranza y otro cargo en ese orden) y lo que sobra va al último; el de capital va entero a capital.
/// </summary>
public class DeterioroPorConceptoTests
{
    [Fact]
    public void Reparte_por_saldos_y_el_sobrante_va_al_ultimo_concepto()
    {
        var saldos = new Dictionary<LoanConcept, decimal> { [LoanConcept.CurrentInterest] = 30m, [LoanConcept.DefaultInterest] = 5m, [LoanConcept.Insurance] = 10m };

        var r = DeterioroPorConcepto.Repartir(1_000m, 50m, 12m, saldos);

        r.Should().Equal(
            (LoanConcept.Principal, 1_000m),
            (LoanConcept.CurrentInterest, 30m),
            (LoanConcept.DefaultInterest, 20m),
            (LoanConcept.Insurance, 10m),
            (LoanConcept.OtherCharge, 2m));
    }

    [Fact]
    public void Sin_deterioro_no_hay_partidas_y_un_saldo_negativo_no_absorbe()
    {
        DeterioroPorConcepto.Repartir(0m, 0m, 0m, new Dictionary<LoanConcept, decimal>()).Should().BeEmpty();
        DeterioroPorConcepto.Repartir(0m, 10m, 0m, new Dictionary<LoanConcept, decimal> { [LoanConcept.CurrentInterest] = -5m })
            .Should().Equal((LoanConcept.DefaultInterest, 10m));
    }
}
