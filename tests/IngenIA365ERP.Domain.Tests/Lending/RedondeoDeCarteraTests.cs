using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US4 (T0467; T35, R44, M15; FR-010): <see cref="RedondeoDeCartera"/> en la causación. Unidad y modo (<c>AwayFromZero</c>,
/// <c>ToEven</c>, <c>Up</c>, <c>Down</c>, con los empates y los dirigidos a mano); el momento decide si el libro se registra a la unidad
/// (por concepto) o al centavo (por cuota y por total); el residuo de la última cuota queda visible; una tasa pactada con más de seis
/// decimales de fracción se rechaza con <c>Lending.Rate.TooPrecise</c>, nunca se redondea.
/// </summary>
public class RedondeoDeCarteraTests
{
    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("2059.205", "0.01", RoundingMode.AwayFromZero, "2059.21")]
    [InlineData("2059.205", "0.01", RoundingMode.ToEven, "2059.20")]
    [InlineData("2059.215", "0.01", RoundingMode.ToEven, "2059.22")]
    [InlineData("2059.201", "0.01", RoundingMode.Up, "2059.21")]
    [InlineData("2059.209", "0.01", RoundingMode.Down, "2059.20")]
    [InlineData("-2059.205", "0.01", RoundingMode.AwayFromZero, "-2059.21")]
    [InlineData("-2059.201", "0.01", RoundingMode.Up, "-2059.20")]
    [InlineData("-2059.209", "0.01", RoundingMode.Down, "-2059.21")]
    [InlineData("2059.5", "1", RoundingMode.ToEven, "2060")]
    [InlineData("2050", "100", RoundingMode.ToEven, "2000")]
    [InlineData("2001", "1000", RoundingMode.Up, "3000")]
    [InlineData("2999", "10", RoundingMode.Down, "2990")]
    public void Lleva_a_la_unidad_con_el_modo_sellado(string valor, string unidad, RoundingMode modo, string esperado) =>
        RedondeoDeCarteraTestsHelper.Unidad(D(unidad), modo).Redondear(D(valor)).Should().Be(D(esperado));

    [Fact]
    public void El_momento_decide_la_unidad_del_libro_de_causacion()
    {
        // Por concepto: cada día del libro es U(A(d)) − U(A(d−1)) a la unidad sellada (aquí, el peso).
        var alPeso = Causado(new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerConcept));
        alPeso.Should().NotBeEmpty().And.OnlyContain(m => m.Monto == decimal.Round(m.Monto, 0));
        // Por cuota o por total: el libro va al centavo (la unidad se aplica al total de la cuota o de la liquidación).
        var porCuota = Causado(new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerInstallment));
        porCuota.Should().Contain(m => m.Monto != decimal.Round(m.Monto, 0)).And.OnlyContain(m => m.Monto == decimal.Round(m.Monto, 2));
        Causado(new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal)).Sum(m => m.Monto)
            .Should().Be(porCuota.Sum(m => m.Monto), "por total el libro también va al centavo");
        // Sin deriva: la suma de los días es el redondeo del acumulado del período, no la suma de redondeos diarios.
        Causacion.DelDia(RedondeoDeCartera.AlCentavo, 0m, 1.005m).Should().Be(1.01m);
        Causacion.DelDia(RedondeoDeCartera.AlCentavo, 1.005m, 2.010m).Should().Be(1.00m, "U(2,010) − U(1,005) = 2,01 − 1,01");
    }

    [Fact]
    public void El_residuo_de_la_ultima_cuota_queda_visible()
    {
        var plan = MotorDeCartera.GenerarPlan(new EntradaDePlan(1_000_000m, new DateOnly(2027, 1, 31), 6, RepaymentPeriodicity.Monthly,
            AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound,
            new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerInstallment)));
        plan.Cuotas.Take(5).Should().OnlyContain(c => c.Total == decimal.Round(c.Total / 100m, 0) * 100m, "cada cuota a la centena");
        plan.ResiduoDeLaUltimaCuota.Should().NotBe(0m);
        plan.Explicacion.Lineas.Should().Contain(l => l.Contains("absorbe el residuo") && l.Contains(ExplicacionDeCartera.Numero(plan.ResiduoDeLaUltimaCuota)));
        plan.TotalCapital.Should().Be(1_000_000m);
    }

    [Theory]
    [InlineData("0.245", true)]
    [InlineData("0.245123", true)]
    [InlineData("0.2451234", false)]
    [InlineData("0.000000001", false)]
    public void Una_tasa_con_mas_de_seis_decimales_se_rechaza_y_no_se_redondea(string tasa, bool pactable)
    {
        EquivalenciaDeTasas.EsTasaPactable(D(tasa)).Should().Be(pactable);
        EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa.Should().Be("Lending.Rate.TooPrecise");
    }

    [Fact]
    public void El_motor_no_reconstruye_con_una_tasa_sellada_demasiado_precisa()
    {
        var simple = CreditosDePrueba.Simple();
        var conSiete = simple with { Condiciones = simple.Condiciones with { TasaEfectivaAnual = 0.2400001m } };
        var reconstruir = () => MotorDeCartera.Reconstruir(conSiete, new DateOnly(2027, 2, 1));
        reconstruir.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa);
        var margen = simple with { Condiciones = simple.Condiciones with { Margen = 0.0500001m } };
        ((Action)(() => MotorDeCartera.Reconstruir(margen, new DateOnly(2027, 2, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Problema.Datos!["field"].Should().Be("margin");
    }

    private static IReadOnlyList<MovimientoDeLibro> Causado(RedondeoDeCartera redondeo)
    {
        var simple = CreditosDePrueba.Simple();
        var entrada = simple with
        {
            Condiciones = simple.Condiciones with { Redondeo = redondeo },
            Plan = simple.Plan! with { Redondeo = redondeo },
        };
        return MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 2, 14), new DateOnly(2027, 1, 16)).Libro.Movimientos
            .Where(m => m.Clase == LoanTransactionKind.Accrual).ToList();
    }
}

/// <summary>Atajos de <see cref="RedondeoDeCarteraTests"/>.</summary>
internal static class RedondeoDeCarteraTestsHelper
{
    public static RedondeoDeCartera Unidad(decimal unidad, RoundingMode modo) => new(unidad, modo, RoundingMoment.PerConcept);
}
