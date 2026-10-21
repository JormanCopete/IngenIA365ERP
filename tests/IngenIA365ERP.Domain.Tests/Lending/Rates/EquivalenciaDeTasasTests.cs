using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Tests.Lending.Rates;

/// <summary>
/// Feature 013, T0327 (T26, R34; FR-010, FR-020; D1): <see cref="EquivalenciaDeTasas"/>. Los valores esperados se calcularon con
/// Python <c>decimal</c> de 40 dígitos (<c>Lending/Casos/calcular.py</c>: raíz a 24 decimales al par, menos 1, a 18 al par), no
/// con el código que se prueba.
/// </summary>
public class EquivalenciaDeTasasTests
{
    [Theory]
    [InlineData("0.24", DayCountConvention.Thirty360, "0.000597710167622401")]
    [InlineData("0.24", DayCountConvention.Actual360, "0.000597710167622401")]
    [InlineData("0.20", DayCountConvention.Actual365, "0.000499635890955618")]
    [InlineData("0.25", DayCountConvention.Thirty360, "0.000620035340587494")]
    [InlineData("0.32", DayCountConvention.Thirty360, "0.000771496718943941")]
    [InlineData("0", DayCountConvention.Thirty360, "0")]
    public void La_diaria_compuesta_es_la_raiz_B_esima_a_18_decimales(string ea, DayCountConvention convencion, string esperada)
    {
        var diaria = EquivalenciaDeTasas.TasaDiaria(D(ea), convencion, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);
        diaria.Should().Be(D(esperada));
        decimal.Round(diaria, EquivalenciaDeTasas.DecimalesDeTasa).Should().Be(diaria, "la diaria va a 18 decimales");
    }

    [Fact]
    public void La_unica_aproximacion_queda_por_debajo_de_10_a_la_menos_15_al_volver_a_la_EA()
    {
        // (1 + 0,000597710167622401)^360 − 1 = 0,239999999999999976… (Python, 40 dígitos).
        var diaria = EquivalenciaDeTasas.TasaDiaria(0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);
        var ea = EquivalenciaDeTasas.EfectivaDeTasaDiaria(diaria, 360);
        ea.Should().Be(D("0.239999999999999976"));
        Math.Abs(ea - 0.24m).Should().BeLessThan(new decimal(1, 0, 0, false, 15));
    }

    [Fact]
    public void La_tasa_del_periodo_compuesta_es_la_potencia_de_la_diaria()
    {
        var diaria = D("0.000597710167622401");
        var mes = EquivalenciaDeTasas.TasaDelPeriodo(diaria, 30, RateEquivalence.Compound);
        // Python: (1 + r_d)^30 − 1 = 0,018087582483510672919716629204431263171…
        decimal.Round(mes, 24).Should().Be(D("0.018087582483510672919717"));
        // Un mes de 30 días da la nominal del período (1,24)^(1/12) − 1 salvo la aproximación de la diaria.
        Math.Abs(mes - EquivalenciaDeTasas.NominalDelPeriodo(0.24m, RepaymentPeriodicity.Monthly)).Should().BeLessThan(new decimal(1, 0, 0, false, 16));
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, 0, RateEquivalence.Compound).Should().Be(0m);
    }

    [Fact]
    public void La_lineal_divide_la_nominal_anual_por_la_base_D1()
    {
        // NA_12 = 12·((1,26)^(1/12) − 1) = 0,233351603535090428…; r_d = NA_12 / 360 = 0,000648198898708585.
        EquivalenciaDeTasas.NominalAnual(0.26m, RepaymentPeriodicity.Monthly).Should().Be(D("0.233351603535090428"));
        var diaria = EquivalenciaDeTasas.TasaDiaria(0.26m, DayCountConvention.Thirty360, RateEquivalence.Linear, RepaymentPeriodicity.Monthly);
        diaria.Should().Be(D("0.000648198898708585"));
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, 50, RateEquivalence.Linear).Should().Be(diaria * 50, "lineal: r_d·n");

        // Un mes comercial completo da (casi) lo mismo que la compuesta; 50 días no.
        var compuesta = EquivalenciaDeTasas.TasaDiaria(0.26m, DayCountConvention.Thirty360, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);
        var mesLineal = EquivalenciaDeTasas.TasaDelPeriodo(diaria, 30, RateEquivalence.Linear);
        var mesCompuesto = EquivalenciaDeTasas.TasaDelPeriodo(compuesta, 30, RateEquivalence.Compound);
        Math.Abs(mesLineal - mesCompuesto).Should().BeLessThan(new decimal(1, 0, 0, false, 14));
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, 50, RateEquivalence.Linear)
            .Should().BeLessThan(EquivalenciaDeTasas.TasaDelPeriodo(compuesta, 50, RateEquivalence.Compound));
    }

    [Fact]
    public void La_lineal_semanal_usa_las_semanas_enteras_del_anio_real()
    {
        EquivalenciaDeTasas.PeriodosPorAnio(RepaymentPeriodicity.Weekly).Should().Be(365 / (int)RepaymentPeriodicity.Weekly);
        EquivalenciaDeTasas.TasaDiaria(0.24m, DayCountConvention.Actual365, RateEquivalence.Linear, RepaymentPeriodicity.Weekly)
            .Should().Be(D("0.000590566919347713"));
    }

    [Theory]
    [InlineData(RepaymentPeriodicity.SinglePayment, 1)]
    [InlineData(RepaymentPeriodicity.TenDay, 36)]
    [InlineData(RepaymentPeriodicity.Biweekly, 24)]
    [InlineData(RepaymentPeriodicity.Monthly, 12)]
    [InlineData(RepaymentPeriodicity.Bimonthly, 6)]
    [InlineData(RepaymentPeriodicity.Quarterly, 4)]
    [InlineData(RepaymentPeriodicity.SemiAnnual, 2)]
    [InlineData(RepaymentPeriodicity.Annual, 1)]
    public void Los_periodos_del_anio_salen_del_valor_del_enum(RepaymentPeriodicity periodicidad, int esperados) =>
        EquivalenciaDeTasas.PeriodosPorAnio(periodicidad).Should().Be(esperados);

    [Fact]
    public void La_nominal_del_periodo_es_informativa_y_va_por_la_raiz()
    {
        EquivalenciaDeTasas.NominalDelPeriodo(0.24m, RepaymentPeriodicity.Monthly).Should().Be(D("0.018087582483510675"));
        EquivalenciaDeTasas.NominalDelPeriodo(0.20m, RepaymentPeriodicity.SinglePayment).Should().Be(0.20m, "pago único: la propia EA");
    }

    [Fact]
    public void La_EA_de_un_indice_nominal_capitaliza_sus_periodos()
    {
        // (1 + 0,135/12)^12 − 1 = 0,143674440742746293 (Python, 40 dígitos).
        EquivalenciaDeTasas.EfectivaDeNominal(0.135m, 12).Should().Be(D("0.143674440742746293"));
    }

    [Theory]
    [InlineData("0.245000", true)]
    [InlineData("0.2450001", false)]
    [InlineData("0.123456", true)]
    [InlineData("0.1234567", false)]
    public void Una_tasa_pactada_con_mas_de_seis_decimales_no_es_pactable(string tasa, bool pactable) =>
        EquivalenciaDeTasas.EsTasaPactable(D(tasa)).Should().Be(pactable);

    [Fact]
    public void Una_EA_negativa_es_un_error_de_quien_llama()
    {
        var accion = () => EquivalenciaDeTasas.TasaDiaria(-0.01m, DayCountConvention.Thirty360, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);
        accion.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
