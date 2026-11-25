using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;

namespace IngenIA365ERP.Domain.Tests.Lending.AccrualProcesses;

/// <summary>
/// Feature 025, fase 5 (2026-11-24; HU5, FR-016, FR-018): el tramo del calendario que contiene un vencimiento con <c>Causacion.Periodo =
/// PeriodoCalendario</c> —mes; quincena 1–15 y 16–fin; década 1–10, 11–20 y 21–fin; semana de lunes a domingo; lo de más de un mes, el mes— y el ciclo
/// de descuento por el día del vencimiento.
/// </summary>
public class TramoDelCalendarioTests
{
    [Theory]
    [InlineData(RepaymentPeriodicity.Monthly, "2026-11-09", "2026-11-01", "2026-11-30")]
    [InlineData(RepaymentPeriodicity.Monthly, "2027-02-28", "2027-02-01", "2027-02-28")]
    [InlineData(RepaymentPeriodicity.Biweekly, "2026-11-15", "2026-11-01", "2026-11-15")]
    [InlineData(RepaymentPeriodicity.Biweekly, "2026-11-16", "2026-11-16", "2026-11-30")]
    [InlineData(RepaymentPeriodicity.Biweekly, "2026-12-31", "2026-12-16", "2026-12-31")]
    [InlineData(RepaymentPeriodicity.Biweekly, "2027-02-25", "2027-02-16", "2027-02-28")]
    [InlineData(RepaymentPeriodicity.TenDay, "2026-11-10", "2026-11-01", "2026-11-10")]
    [InlineData(RepaymentPeriodicity.TenDay, "2026-11-11", "2026-11-11", "2026-11-20")]
    [InlineData(RepaymentPeriodicity.TenDay, "2026-12-31", "2026-12-21", "2026-12-31")]
    [InlineData(RepaymentPeriodicity.Weekly, "2026-11-19", "2026-11-16", "2026-11-22")]
    [InlineData(RepaymentPeriodicity.Weekly, "2026-11-22", "2026-11-16", "2026-11-22")]
    [InlineData(RepaymentPeriodicity.Quarterly, "2026-11-09", "2026-11-01", "2026-11-30")]
    [InlineData(RepaymentPeriodicity.SinglePayment, "2026-11-09", "2026-11-01", "2026-11-30")]
    public void El_tramo_es_el_del_calendario_que_contiene_el_vencimiento(RepaymentPeriodicity periodicidad, string vencimiento, string desde, string hasta)
    {
        var (d, h) = PeriodoDeCausacion.TramoQueContiene(DateOnly.Parse(vencimiento), periodicidad);

        d.Should().Be(DateOnly.Parse(desde));
        h.Should().Be(DateOnly.Parse(hasta));
    }

    [Fact]
    public void El_tramo_de_la_cuota_del_9_empieza_el_1_y_la_del_mes_siguiente_no_ha_empezado_el_24()
    {
        // HU5, escenario 4: el 24-11 la cuota de noviembre (vence el 9) es la del tramo en curso; la de diciembre, no.
        var dia24 = new DateOnly(2026, 11, 24);
        PeriodoDeCausacion.TramoEmpezado(new DateOnly(2026, 11, 9), RepaymentPeriodicity.Monthly, dia24).Should().BeTrue();
        PeriodoDeCausacion.TramoEmpezado(new DateOnly(2026, 12, 9), RepaymentPeriodicity.Monthly, dia24).Should().BeFalse();
        PeriodoDeCausacion.TramoEmpezado(new DateOnly(2026, 12, 9), RepaymentPeriodicity.Monthly, new DateOnly(2026, 12, 1)).Should().BeTrue();
    }

    [Theory]
    [InlineData(AccrualDiscountCycle.FirstFortnight, 1, true)]
    [InlineData(AccrualDiscountCycle.FirstFortnight, 15, true)]
    [InlineData(AccrualDiscountCycle.FirstFortnight, 16, false)]
    [InlineData(AccrualDiscountCycle.SecondFortnight, 16, true)]
    [InlineData(AccrualDiscountCycle.SecondFortnight, 31, true)]
    [InlineData(AccrualDiscountCycle.FirstTenDay, 10, true)]
    [InlineData(AccrualDiscountCycle.SecondTenDay, 11, true)]
    [InlineData(AccrualDiscountCycle.SecondTenDay, 21, false)]
    [InlineData(AccrualDiscountCycle.ThirdTenDay, 31, true)]
    [InlineData(AccrualDiscountCycle.FirstWeek, 7, true)]
    [InlineData(AccrualDiscountCycle.SecondWeek, 8, true)]
    [InlineData(AccrualDiscountCycle.FourthWeek, 28, true)]
    [InlineData(AccrualDiscountCycle.FourthWeek, 29, false)]
    [InlineData(AccrualDiscountCycle.FifthWeek, 31, true)]
    public void El_ciclo_de_descuento_va_por_el_dia_del_vencimiento(AccrualDiscountCycle ciclo, int dia, bool esperado)
    {
        PeriodoDeCausacion.EnCicloDeDescuento(new DateOnly(2026, 12, dia), ciclo).Should().Be(esperado);
    }
}
