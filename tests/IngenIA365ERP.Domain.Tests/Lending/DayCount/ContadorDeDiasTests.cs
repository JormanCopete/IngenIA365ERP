using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;

namespace IngenIA365ERP.Domain.Tests.Lending.DayCount;

/// <summary>
/// Feature 013, T0326 (T27, R35, M6; FR-010; D3; quickstart §1.4): <see cref="ContadorDeDias"/> cuenta de <c>a</c> excluida a
/// <c>b</c> incluida en las tres convenciones. Los días esperados se contaron a mano en el calendario.
/// </summary>
public class ContadorDeDiasTests
{
    private static DateOnly F(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(DayCountConvention.Thirty360)]
    [InlineData(DayCountConvention.Actual365)]
    [InlineData(DayCountConvention.Actual360)]
    public void De_a_excluida_a_b_incluida_dos_tramos_seguidos_suman_el_total(DayCountConvention convencion)
    {
        var a = F("2027-01-17");
        var b = F("2027-03-04");
        var c = F("2027-06-30");
        ContadorDeDias.Dias(convencion, a, a).Should().Be(0, "el mismo día no causa");
        (ContadorDeDias.Dias(convencion, a, b) + ContadorDeDias.Dias(convencion, b, c)).Should().Be(ContadorDeDias.Dias(convencion, a, c));
    }

    [Theory]
    [InlineData("2027-01-30", "2027-01-31", 0)]   // el 31 no causa
    [InlineData("2027-01-31", "2027-02-01", 1)]   // el 31 vale 30: del 30 al 1 hay uno
    [InlineData("2027-01-15", "2027-02-15", 30)]
    [InlineData("2027-01-31", "2027-02-28", 30)]  // el fin de febrero completa 30
    [InlineData("2027-02-27", "2027-02-28", 3)]   // el 28 de febrero de 2027 vale 30
    [InlineData("2027-02-28", "2027-03-31", 30)]
    [InlineData("2027-02-28", "2027-03-01", 1)]
    [InlineData("2028-02-28", "2028-02-29", 2)]   // en bisiesto el fin de febrero es el 29
    [InlineData("2028-02-29", "2028-03-31", 30)]
    [InlineData("2027-03-31", "2027-04-30", 30)]
    [InlineData("2026-12-20", "2027-02-05", 45)]  // primer período irregular del caso dorado 07
    public void Treinta_360_el_31_no_causa_y_el_fin_de_febrero_completa_30(string a, string b, int esperado) =>
        ContadorDeDias.Dias(DayCountConvention.Thirty360, F(a), F(b)).Should().Be(esperado);

    [Fact]
    public void Treinta_360_da_360_dias_en_un_anio_y_30_en_cada_mes_del_anio()
    {
        ContadorDeDias.Dias(DayCountConvention.Thirty360, F("2027-01-31"), F("2028-01-31")).Should().Be(ContadorDeDias.AnioComercial);
        var fin = new[] { "2027-01-31", "2027-02-28", "2027-03-31", "2027-04-30", "2027-05-31", "2027-06-30" }.Select(F).ToList();
        for (var i = 1; i < fin.Count; i++)
            ContadorDeDias.Dias(DayCountConvention.Thirty360, fin[i - 1], fin[i]).Should().Be(ContadorDeDias.MesComercial);
    }

    [Fact]
    public void Real_365_de_base_fija_cuenta_el_29_de_febrero_de_2028()
    {
        ContadorDeDias.Dias(DayCountConvention.Actual365, F("2028-02-15"), F("2028-03-15")).Should().Be(29, "D3: el 29 de febrero causa");
        ContadorDeDias.Dias(DayCountConvention.Actual365, F("2028-02-28"), F("2028-02-29")).Should().Be(1);
        ContadorDeDias.Dias(DayCountConvention.Actual365, F("2028-01-01"), F("2029-01-01")).Should().Be(366, "un año bisiesto tiene 366 días de interés");
        ContadorDeDias.Base(DayCountConvention.Actual365).Should().Be(365, "la base sigue fija aunque el año sea bisiesto");
        ContadorDeDias.Dias(DayCountConvention.Actual365, F("2027-02-15"), F("2027-03-15")).Should().Be(28);
    }

    [Fact]
    public void Real_360_cuenta_dias_reales_sobre_base_360()
    {
        ContadorDeDias.Dias(DayCountConvention.Actual360, F("2027-01-31"), F("2027-02-28")).Should().Be(28);
        ContadorDeDias.Dias(DayCountConvention.Actual360, F("2027-03-10"), F("2027-04-10")).Should().Be(31);
        ContadorDeDias.Base(DayCountConvention.Actual360).Should().Be(360);
        ContadorDeDias.DiasDelAnio(DayCountConvention.Actual360).Should().Be(365, "el año calendario tiene 365 días que causan sobre base 360");
        ContadorDeDias.Base(DayCountConvention.Thirty360).Should().Be(360);
        ContadorDeDias.DiasDelAnio(DayCountConvention.Thirty360).Should().Be(360);
    }

    [Theory]
    [InlineData(DayCountConvention.Thirty360, 30)]
    [InlineData(DayCountConvention.Actual365, 31)]
    [InlineData(DayCountConvention.Actual360, 31)]
    public void Un_tramo_que_cruza_fin_de_anio(DayCountConvention convencion, int esperado)
    {
        ContadorDeDias.Dias(convencion, F("2026-12-15"), F("2027-01-15")).Should().Be(esperado);
        // Partido en el 31 de diciembre suma lo mismo.
        (ContadorDeDias.Dias(convencion, F("2026-12-15"), F("2026-12-31")) + ContadorDeDias.Dias(convencion, F("2026-12-31"), F("2027-01-15")))
            .Should().Be(esperado);
    }

    [Fact]
    public void Un_tramo_al_reves_es_un_error_de_quien_llama()
    {
        var accion = () => ContadorDeDias.Dias(DayCountConvention.Actual365, F("2027-02-01"), F("2027-01-31"));
        accion.Should().Throw<ArgumentException>();
    }
}
