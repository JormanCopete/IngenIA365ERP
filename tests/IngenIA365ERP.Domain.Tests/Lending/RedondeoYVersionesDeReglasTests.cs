using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, T0348 (M8, M11, M15; T35): <see cref="RedondeoDeCartera"/> (unidad, modo y momento), <see cref="VersionesDeReglas"/>
/// (<c>LND-AAAA.n</c>, la vigente <c>LND-2026.1</c>) y <see cref="ExplicacionDeCartera"/>.
/// </summary>
public class RedondeoYVersionesDeReglasTests
{
    [Theory]
    [InlineData("1234.565", "0.01", RoundingMode.AwayFromZero, "1234.57")]
    [InlineData("1234.565", "0.01", RoundingMode.ToEven, "1234.56")]
    [InlineData("1234.561", "0.01", RoundingMode.Up, "1234.57")]
    [InlineData("1234.569", "0.01", RoundingMode.Down, "1234.56")]
    [InlineData("1250", "100", RoundingMode.AwayFromZero, "1300")]
    [InlineData("1250", "100", RoundingMode.ToEven, "1200")]
    [InlineData("1201", "100", RoundingMode.Up, "1300")]
    [InlineData("1299", "1000", RoundingMode.Down, "1000")]
    [InlineData("15.5", "10", RoundingMode.AwayFromZero, "20")]
    [InlineData("2.5", "1", RoundingMode.ToEven, "2")]
    public void Redondea_a_la_unidad_con_el_modo_sellado(string valor, string unidad, RoundingMode modo, string esperado)
    {
        var r = new RedondeoDeCartera(D(unidad), modo, RoundingMoment.PerConcept);
        r.Redondear(D(valor)).Should().Be(D(esperado));
    }

    [Fact]
    public void El_momento_decide_si_un_concepto_va_a_la_unidad_o_al_centavo()
    {
        new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerConcept).Concepto(1234.567m).Should().Be(1200m);
        new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerInstallment).Concepto(1234.567m).Should().Be(1234.57m);
        new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerTotal).Concepto(1234.567m).Should().Be(1234.57m);
        new RedondeoDeCartera(100m, RoundingMode.AwayFromZero, RoundingMoment.PerInstallment).PorCuota.Should().BeTrue();
    }

    [Fact]
    public void Solo_admite_las_cinco_unidades()
    {
        RedondeoDeCartera.UnidadesAdmitidas.Should().Equal(0.01m, 1m, 10m, 100m, 1000m);
        var cincuenta = () => new RedondeoDeCartera(50m, RoundingMode.AwayFromZero, RoundingMoment.PerConcept);
        cincuenta.Should().Throw<ArgumentOutOfRangeException>();
        var modo = () => new RedondeoDeCartera(1m, (RoundingMode)99, RoundingMoment.PerConcept);
        modo.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void La_version_de_reglas_vigente_es_LND_2026_1()
    {
        VersionesDeReglas.Vigente.Should().Be("LND-2026.1");
        VersionesDeReglas.Conocidas.Should().Contain(VersionesDeReglas.Vigente);
        VersionesDeReglas.EsConocida("LND-2026.1").Should().BeTrue();
        VersionesDeReglas.EsConocida("LND-2027.1").Should().BeFalse("una versión que este programa no sabe calcular");
        MotorDeCartera.VersionDeReglas.Should().Be(VersionesDeReglas.Vigente);
    }

    [Theory]
    [InlineData("LND-2026.1", true)]
    [InlineData("LND-2031.12", true)]
    [InlineData("LND-2026.0", false)]
    [InlineData("LND-26.1", false)]
    [InlineData("PAY-2026.1", false)]
    [InlineData("lnd-2026.1", false)]
    [InlineData(null, false)]
    public void El_formato_es_LND_AAAA_n(string? version, bool valida) => VersionesDeReglas.TieneFormato(version).Should().Be(valida);

    [Fact]
    public void La_explicacion_lleva_lineas_y_tramos_en_cultura_invariante()
    {
        var e = new ExplicacionDeCartera()
            .Linea("Tasa diaria 0,000597710167622401.")
            .Tramo(new TramoDeCartera(new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), 30, 0.000597710167622401m, 1_200_000m, 21_705.10m, "CurrentInterest", 1));
        e.Tramos.Should().ContainSingle();
        e.Texto.Should().Contain("2027-01-31..2027-02-28: 30 días × tasa diaria 0.000597710167622401 sobre 1200000 → CurrentInterest 21705.1 (cuota 1)");
        new ExplicacionDeCartera().Agregar(e).Lineas.Should().Equal(e.Lineas);
    }

    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
