using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Schedules;

/// <summary>
/// Feature 013, T0329 (T27, R35, M6; FR-022; CH39): <see cref="FechasDeVencimiento"/> de las nueve <see cref="RepaymentPeriodicity"/>
/// con la regla del día de pago (si el mes no lo tiene, el último día) y el primer período irregular. Los días de la semana y de la
/// década salen del valor del enum, nunca de un literal. Las fechas esperadas se contaron a mano en el calendario.
/// </summary>
public class FechasDeVencimientoTests
{
    private static DateOnly F(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<DateOnly> Fs(params string[] isos) => isos.Select(F).ToList();

    [Fact]
    public void Mensual_con_dia_31_vence_el_ultimo_dia_del_mes_que_no_lo_tiene()
    {
        FechasDeVencimiento.Generar(F("2027-01-31"), RepaymentPeriodicity.Monthly, 4)
            .Should().Equal(Fs("2027-02-28", "2027-03-31", "2027-04-30", "2027-05-31"));
        FechasDeVencimiento.Generar(F("2027-12-31"), RepaymentPeriodicity.Monthly, 3)
            .Should().Equal(Fs("2028-01-31", "2028-02-29", "2028-03-31"), "en 2028 febrero tiene 29");
    }

    [Fact]
    public void Bimestral_trimestral_semestral_y_anual_avanzan_por_meses()
    {
        FechasDeVencimiento.Generar(F("2027-01-15"), RepaymentPeriodicity.Bimonthly, 3).Should().Equal(Fs("2027-03-15", "2027-05-15", "2027-07-15"));
        FechasDeVencimiento.Generar(F("2027-01-31"), RepaymentPeriodicity.Quarterly, 3).Should().Equal(Fs("2027-04-30", "2027-07-31", "2027-10-31"));
        FechasDeVencimiento.Generar(F("2027-08-31"), RepaymentPeriodicity.SemiAnnual, 2).Should().Equal(Fs("2028-02-29", "2028-08-31"));
        FechasDeVencimiento.Generar(F("2028-02-29"), RepaymentPeriodicity.Annual, 4)
            .Should().Equal(Fs("2029-02-28", "2030-02-28", "2031-02-28", "2032-02-29"));
    }

    [Fact]
    public void Con_dia_de_pago_el_primer_periodo_es_irregular_y_nunca_menor_que_un_periodo()
    {
        // Desembolso el 20-12: el primer día 5 que no cae antes del 20-01 es el 05-02 (45 días en 30/360): caso dorado 07.
        FechasDeVencimiento.Generar(F("2026-12-20"), RepaymentPeriodicity.Monthly, 3, diaDePago: 5)
            .Should().Equal(Fs("2027-02-05", "2027-03-05", "2027-04-05"));
        // Desembolso el 01-12 con día 5: el 05-01 ya no cae antes del 01-01 (35 días, el ejemplo de api.md §4.1).
        FechasDeVencimiento.Generar(F("2026-12-01"), RepaymentPeriodicity.Monthly, 2, diaDePago: 5)
            .Should().Equal(Fs("2027-01-05", "2027-02-05"));
        // Día 31 en meses cortos.
        FechasDeVencimiento.Generar(F("2027-01-10"), RepaymentPeriodicity.Monthly, 3, diaDePago: 31)
            .Should().Equal(Fs("2027-02-28", "2027-03-31", "2027-04-30"));
    }

    [Fact]
    public void El_primer_vencimiento_explicito_fija_el_periodo_irregular()
    {
        FechasDeVencimiento.Generar(F("2027-02-20"), RepaymentPeriodicity.Monthly, 3, primerVencimiento: F("2027-03-15"))
            .Should().Equal(Fs("2027-03-15", "2027-04-15", "2027-05-15"));
        FechasDeVencimiento.Generar(F("2027-02-20"), RepaymentPeriodicity.Monthly, 2, diaDePago: 30, primerVencimiento: F("2027-03-30"))
            .Should().Equal(Fs("2027-03-30", "2027-04-30"));
    }

    [Fact]
    public void Quincenal_parte_el_mes_en_dos_ranuras_y_febrero_vence_el_28()
    {
        // Día 15: ranuras 15 y 30; desembolso el 10-01: la primera que no cae antes del 25-01 es el 30-01.
        FechasDeVencimiento.Generar(F("2027-01-10"), RepaymentPeriodicity.Biweekly, 6, diaDePago: 15)
            .Should().Equal(Fs("2027-01-30", "2027-02-15", "2027-02-28", "2027-03-15", "2027-03-30", "2027-04-15"));
        // Día 5: ranuras 5 y 20.
        FechasDeVencimiento.Generar(F("2027-03-01"), RepaymentPeriodicity.Biweekly, 3, diaDePago: 5)
            .Should().Equal(Fs("2027-03-20", "2027-04-05", "2027-04-20"));
    }

    [Fact]
    public void Decadal_parte_el_mes_en_tres_ranuras()
    {
        FechasDeVencimiento.Generar(F("2027-02-01"), RepaymentPeriodicity.TenDay, 4, diaDePago: 5)
            .Should().Equal(Fs("2027-02-15", "2027-02-25", "2027-03-05", "2027-03-15"));
        // Día 30: ranuras 10, 20 y 30; la tercera de febrero es el 28.
        FechasDeVencimiento.Generar(F("2027-01-25"), RepaymentPeriodicity.TenDay, 4, diaDePago: 30)
            .Should().Equal(Fs("2027-02-10", "2027-02-20", "2027-02-28", "2027-03-10"));
    }

    [Fact]
    public void Semanal_y_decadal_toman_sus_dias_del_valor_del_enum()
    {
        var semanal = FechasDeVencimiento.Generar(F("2027-01-01"), RepaymentPeriodicity.Weekly, 5);
        semanal.Should().Equal(Fs("2027-01-08", "2027-01-15", "2027-01-22", "2027-01-29", "2027-02-05"));
        for (var i = 1; i < semanal.Count; i++)
            (semanal[i].DayNumber - semanal[i - 1].DayNumber).Should().Be((int)RepaymentPeriodicity.Weekly);

        var decadal = FechasDeVencimiento.Generar(F("2027-04-01"), RepaymentPeriodicity.TenDay, 3, diaDePago: 1);
        for (var i = 1; i < decadal.Count; i++)
            (decadal[i].DayNumber - decadal[i - 1].DayNumber).Should().Be((int)RepaymentPeriodicity.TenDay, "abril tiene sus décadas completas");
    }

    [Fact]
    public void Pago_unico_vence_en_la_fecha_dada_y_solo_tiene_una()
    {
        FechasDeVencimiento.Generar(F("2026-11-10"), RepaymentPeriodicity.SinglePayment, 1, primerVencimiento: F("2027-05-10"))
            .Should().Equal(Fs("2027-05-10"));
        var sinFecha = () => FechasDeVencimiento.Generar(F("2026-11-10"), RepaymentPeriodicity.SinglePayment, 1);
        sinFecha.Should().Throw<ArgumentException>();
        var dos = () => FechasDeVencimiento.Generar(F("2026-11-10"), RepaymentPeriodicity.SinglePayment, 2, primerVencimiento: F("2027-05-10"));
        dos.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(RepaymentPeriodicity.Weekly)]
    [InlineData(RepaymentPeriodicity.TenDay)]
    [InlineData(RepaymentPeriodicity.Biweekly)]
    [InlineData(RepaymentPeriodicity.Monthly)]
    [InlineData(RepaymentPeriodicity.Bimonthly)]
    [InlineData(RepaymentPeriodicity.Quarterly)]
    [InlineData(RepaymentPeriodicity.SemiAnnual)]
    [InlineData(RepaymentPeriodicity.Annual)]
    public void Los_vencimientos_crecen_y_ninguno_cae_antes_del_desembolso_mas_un_periodo(RepaymentPeriodicity periodicidad)
    {
        foreach (var dia in new int?[] { null, 1, 15, 28, 31 })
        {
            var desembolso = F("2027-01-31");
            var fechas = FechasDeVencimiento.Generar(desembolso, periodicidad, 24, dia);
            fechas.Should().HaveCount(24).And.BeInAscendingOrder().And.OnlyHaveUniqueItems();
            fechas[0].Should().BeAfter(desembolso);
        }
    }

    [Fact]
    public void Datos_imposibles_son_errores_de_quien_llama()
    {
        var sinPeriodos = () => FechasDeVencimiento.Generar(F("2027-01-01"), RepaymentPeriodicity.Monthly, 0);
        sinPeriodos.Should().Throw<ArgumentOutOfRangeException>();
        var dia32 = () => FechasDeVencimiento.Generar(F("2027-01-01"), RepaymentPeriodicity.Monthly, 2, diaDePago: 32);
        dia32.Should().Throw<ArgumentOutOfRangeException>();
        var primeroAntes = () => FechasDeVencimiento.Generar(F("2027-01-01"), RepaymentPeriodicity.Monthly, 2, primerVencimiento: F("2027-01-01"));
        primeroAntes.Should().Throw<ArgumentException>();
    }
}
