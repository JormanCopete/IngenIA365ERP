using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// 2026-11-24 (feature 025, fase 4, HU4; FR-014): el inicio del período de la primera cuota posterior al corte de una carga con la opción «el período en
/// curso al corte se causa completo» (<see cref="AjusteDelCorteDeCarga.ConPeriodoCompleto"/>). Los montos los fija el caso dorado
/// <c>26-credito-migrado-periodo-completo</c>.
/// </summary>
public class PeriodoCompletoDeLaCargaTests
{
    private static readonly DateOnly Corte = new(2026, 10, 31);

    [Fact]
    public void Con_una_cuota_cargada_anterior_el_inicio_es_su_vencimiento()
    {
        var cuotas = AjusteDelCorteDeCarga.ConPeriodoCompleto(
            [new CuotaMigrada(4, new DateOnly(2026, 10, 9), 100m), new CuotaMigrada(5, new DateOnly(2026, 11, 9), 100m)], Corte,
            RepaymentPeriodicity.Monthly, new DateOnly(2026, 6, 9));

        cuotas.Single(c => c.Numero == 5).Desde.Should().Be(new DateOnly(2026, 10, 9));
        cuotas.Single(c => c.Numero == 4).Desde.Should().BeNull("las demás cuotas quedan como vinieron");
    }

    [Fact]
    public void La_cuota_1_empieza_con_el_desembolso()
    {
        var cuotas = AjusteDelCorteDeCarga.ConPeriodoCompleto([new CuotaMigrada(1, new DateOnly(2026, 11, 2), 100m)], Corte, RepaymentPeriodicity.Monthly,
            new DateOnly(2026, 10, 1));

        cuotas.Single().Desde.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Theory]
    [InlineData(RepaymentPeriodicity.Monthly, "2026-10-09")]
    [InlineData(RepaymentPeriodicity.Biweekly, "2026-10-25")]
    public void Sin_la_anterior_es_un_periodo_antes_del_vencimiento(RepaymentPeriodicity periodicidad, string esperado)
    {
        var cuotas = AjusteDelCorteDeCarga.ConPeriodoCompleto([new CuotaMigrada(5, new DateOnly(2026, 11, 9), 100m)], Corte, periodicidad,
            new DateOnly(2026, 6, 9));

        cuotas.Single().Desde.Should().Be(DateOnly.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Si_el_periodo_empieza_despues_del_corte_no_hay_nada_que_completar()
    {
        var cuotas = AjusteDelCorteDeCarga.ConPeriodoCompleto([new CuotaMigrada(1, new DateOnly(2026, 11, 20), 100m)], Corte, RepaymentPeriodicity.Monthly,
            new DateOnly(2026, 11, 5));

        cuotas.Single().Desde.Should().BeNull();
    }
}
