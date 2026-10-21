using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Tests.Lending.Rates;

/// <summary>
/// Feature 013, T0327 (T28, R36; FR-020, FR-021; D10): <see cref="TasaDelDia"/>. La corriente del día es
/// <c>min(pactada o reajustada, Tope(U − sobretasa))</c> y la mora <c>min(pactada o relativa, Tope(U))</c>; la variable suma el
/// margen en la expresión del índice, la lleva a EA y aplica piso y techo antes de la usura. Esperados con Python <c>decimal</c> de
/// 40 dígitos.
/// </summary>
public class TasaDelDiaTests
{
    private static EntradaDeTasaDelDia Entrada(decimal corriente, decimal? usura, decimal sobretasa = 0m,
        DayCountConvention convencion = DayCountConvention.Thirty360, DefaultRateKind mora = DefaultRateKind.RelativeToCurrent,
        decimal? moraFija = null, decimal? factor = 1.5m) =>
        new(corriente, mora, moraFija, factor, usura, sobretasa, convencion, RateEquivalence.Compound, RepaymentPeriodicity.Monthly);

    [Fact]
    public void Bajo_la_usura_la_corriente_no_se_topa()
    {
        var r = TasaDelDia.Calcular(Entrada(0.25m, usura: 0.30m, factor: 1m));
        r.CorrienteTopada.Should().BeFalse();
        r.TasaDiariaCorriente.Should().Be(D("0.000620035340587494"));
        r.TopeDiarioCorriente.Should().Be(D("0.000729055255204437"), "RaizEntera(1,30; 360) − 1");
        r.Topada.Should().BeFalse();
    }

    [Fact]
    public void Sobre_la_usura_la_corriente_causa_con_el_tope_y_lo_indica()
    {
        var r = TasaDelDia.Calcular(Entrada(0.32m, usura: 0.30m));
        r.CorrienteTopada.Should().BeTrue("la usura nueva quedó por debajo de la pactada (US1-4)");
        r.TasaDiariaCorriente.Should().Be(D("0.000729055255204437"));
        r.Explicacion.Texto.Should().Contain("se causa con el tope");
    }

    [Fact]
    public void La_sobretasa_de_cargos_baja_el_tope_de_la_corriente_pero_no_el_de_la_mora()
    {
        // Tope de la corriente = diaria de 0,30 − 0,02 = 0,28: RaizEntera(1,28; 360) − 1 = 0,000685957600078890.
        var r = TasaDelDia.Calcular(Entrada(0.29m, usura: 0.30m, sobretasa: 0.02m, factor: 1m));
        r.TopeDiarioCorriente.Should().Be(D("0.000685957600078890"));
        r.CorrienteTopada.Should().BeTrue("0,29 supera la usura menos la sobretasa (0,28)");
        r.TasaDiariaCorriente.Should().Be(D("0.000685957600078890"));
        r.TopeDiarioMora.Should().Be(D("0.000729055255204437"));
        r.MoraTopada.Should().BeFalse("la mora relativa (1 × 0,29) no supera la usura");
    }

    [Fact]
    public void La_mora_relativa_se_topa_en_la_usura()
    {
        // 1,5 × 0,25 = 0,375 > 0,30: la mora causa con RaizEntera(1,30; 360) − 1.
        var r = TasaDelDia.Calcular(Entrada(0.25m, usura: 0.30m, factor: 1.5m));
        r.MoraEA.Should().Be(0.375m);
        r.MoraTopada.Should().BeTrue();
        r.TasaDiariaMora.Should().Be(D("0.000729055255204437"));
        r.CorrienteTopada.Should().BeFalse();
    }

    [Fact]
    public void La_mora_fija_bajo_la_usura_causa_con_su_tasa()
    {
        var r = TasaDelDia.Calcular(Entrada(0.20m, usura: 0.30m, mora: DefaultRateKind.Fixed, moraFija: 0.25m, factor: null));
        r.MoraTopada.Should().BeFalse();
        r.TasaDiariaMora.Should().Be(D("0.000620035340587494"));
    }

    [Fact]
    public void En_real_360_el_tope_es_la_pactable_cuya_efectiva_no_supera_la_usura()
    {
        // La pactable en real/360: ((1,30)^(1/365))^360 − 1 = 0,295336145712629946; su diaria = RaizEntera(1,30; 365) − 1.
        TasaDelDia.TopePactable(0.30m, DayCountConvention.Actual360).Should().Be(D("0.295336145712629946"));
        TasaDelDia.TopeDiario(0.30m, DayCountConvention.Actual360, RateEquivalence.Compound, RepaymentPeriodicity.Monthly)
            .Should().Be(D("0.000719064607110198"));
        TasaDelDia.TopePactable(0.30m, DayCountConvention.Thirty360).Should().Be(0.30m, "en 30/360 la pactable es la misma usura");
        TasaDelDia.TopePactable(0.30m, DayCountConvention.Actual365).Should().Be(0.30m, "en real/365 de base fija también (D3)");

        // Una EA de 0,30 en real/360 supera la usura de 0,30: se topa.
        var r = TasaDelDia.Calcular(Entrada(0.30m, usura: 0.30m, convencion: DayCountConvention.Actual360, factor: 1m));
        r.CorrienteTopada.Should().BeTrue();
        r.TasaDiariaCorriente.Should().Be(D("0.000719064607110198"));
    }

    [Fact]
    public void Sin_usura_conocida_no_topa_y_lo_dice()
    {
        var r = TasaDelDia.Calcular(Entrada(0.32m, usura: null));
        r.Topada.Should().BeFalse();
        r.TopeDiarioCorriente.Should().BeNull();
        r.TasaDiariaCorriente.Should().Be(D("0.000771496718943941"));
        r.Explicacion.Texto.Should().Contain("Sin usura conocida");
    }

    [Fact]
    public void La_variable_suma_el_margen_en_la_expresion_del_indice_y_luego_aplica_piso_y_techo_D10()
    {
        // IBR nominal mes vencido 9,50 % + margen 4,00 % = 13,50 % NMV → (1 + 0,135/12)^12 − 1 = 0,143674440742746293 EA.
        var ibr = new IndiceDeReferencia(0.095m, 12);
        TasaDelDia.TasaVariable(ibr, 0.04m).Should().Be(D("0.143674440742746293"));
        TasaDelDia.TasaVariable(ibr, 0.04m, piso: 0.10m, techo: 0.20m).Should().Be(D("0.143674440742746293"), "dentro de piso y techo");
        TasaDelDia.TasaVariable(ibr, 0.04m, techo: 0.14m).Should().Be(0.14m, "el techo se aplica a la EA ya convertida");
        TasaDelDia.TasaVariable(ibr, 0.04m, piso: 0.15m).Should().Be(0.15m, "el piso también");

        // Sumar el margen a la EA del índice daría otra tasa: (1 + 0,095/12)^12 − 1 + 0,04 ≠ 0,14367…
        var sobreLaEA = EquivalenciaDeTasas.EfectivaDeNominal(0.095m, 12) + 0.04m;
        sobreLaEA.Should().NotBe(D("0.143674440742746293"));

        // Un índice que ya es efectiva anual suma el margen en EA.
        TasaDelDia.TasaVariable(new IndiceDeReferencia(0.0520m), 0.0800m).Should().Be(0.132m);
    }

    [Fact]
    public void Piso_sobre_techo_es_un_error_de_quien_llama()
    {
        var accion = () => TasaDelDia.TasaVariable(new IndiceDeReferencia(0.09m, 12), 0.04m, piso: 0.20m, techo: 0.10m);
        accion.Should().Throw<ArgumentException>();
    }

    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
