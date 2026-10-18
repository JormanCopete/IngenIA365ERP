using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US3 (T082, T092; data-model §2.5, §3.5; FR-040, FR-049): el motor puro de las cuotas de aportes —el calendario de períodos por
/// periodicidad con su vencimiento, el valor por modo con el redondeo de la línea y el reparto con el ahorro permanente— y el estado calculado de una
/// cuota exigible.
/// </summary>
public class CuotasDeAportesTests
{
    [Fact]
    public void Mensual_con_dia_de_vencimiento_genera_un_periodo_por_mes_y_vence_ese_dia_o_el_ultimo_del_mes()
    {
        var periodos = CuotasDeAportes.Periodos(RepaymentPeriodicity.Monthly, 31, new DateOnly(2026, 12, 15), new DateOnly(2027, 2, 1));

        periodos.Select(p => (p.Texto, p.Vencimiento)).Should().Equal(
            ("2026-12", new DateOnly(2026, 12, 31)),
            ("2027-01", new DateOnly(2027, 1, 31)),
            ("2027-02", new DateOnly(2027, 2, 28)));
        periodos[0].Clave.Should().Be("2026-12-1");
    }

    [Fact]
    public void Un_periodo_que_todavia_no_empieza_no_se_genera()
    {
        CuotasDeAportes.Periodos(RepaymentPeriodicity.Monthly, 5, new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 31))
            .Select(p => p.Texto).Should().Equal("2027-01");
    }

    [Theory]
    [InlineData(RepaymentPeriodicity.Biweekly, 31, new[] { 1, 16 }, new[] { 15, 31 })]
    [InlineData(RepaymentPeriodicity.TenDay, 31, new[] { 1, 11, 21 }, new[] { 10, 20, 31 })]
    [InlineData(RepaymentPeriodicity.Weekly, 28, new[] { 1, 8, 15, 22 }, new[] { 7, 14, 21, 28 })]
    [InlineData(RepaymentPeriodicity.Monthly, 30, new[] { 1 }, new[] { 30 })]
    public void Los_subperiodos_tienen_limites_fijos_y_el_ultimo_absorbe_los_dias_que_sobran(RepaymentPeriodicity p, int dias, int[] desde, int[] hasta)
    {
        var sub = CuotasDeAportes.Subperiodos(p, dias);
        sub.Select(s => s.DiaDesde).Should().Equal(desde);
        sub.Select(s => s.DiaHasta).Should().Equal(hasta);
    }

    [Fact]
    public void Quincenal_vence_al_fin_de_cada_quincena_y_la_segunda_lleva_su_subperiodo()
    {
        var periodos = CuotasDeAportes.Periodos(RepaymentPeriodicity.Biweekly, null, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 20));
        periodos.Select(p => (p.Texto, p.Vencimiento)).Should().Equal(("2027-01", new DateOnly(2027, 1, 15)), ("2027-01-2", new DateOnly(2027, 1, 31)));
    }

    [Fact]
    public void Trimestral_va_por_bloques_de_tres_meses_desde_enero_y_vence_en_el_ultimo_mes()
    {
        var p = CuotasDeAportes.PeriodoDe(RepaymentPeriodicity.Quarterly, 10, new DateOnly(2027, 5, 20))!;
        (p.Mes, p.Desde, p.Hasta, p.Vencimiento).Should().Be((4, new DateOnly(2027, 4, 1), new DateOnly(2027, 6, 30), new DateOnly(2027, 6, 10)));
        CuotasDeAportes.PeriodoDe(RepaymentPeriodicity.SinglePayment, null, new DateOnly(2027, 5, 20)).Should().BeNull("el pago único no tiene cuotas");
    }

    [Fact]
    public void Valor_fijo_sin_ahorro_permanente()
    {
        var (c, problema) = CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(ContributionAmountMode.FixedAmount, 50_000m, null, null, null, 1m,
            RoundingMode.AwayFromZero, null));
        problema.Should().BeNull();
        (c!.Total, c.Aportes, c.Permanente).Should().Be((50_000m, 50_000m, 0m));
    }

    [Fact]
    public void Fraccion_del_ingreso_al_mil_y_reparto_con_el_permanente_que_cuadra()
    {
        // 3 % de 2.345.678 = 70.370,34 → al mil hacia arriba 71.000; 40 % al permanente = 28.400; aportes 42.600.
        var (c, _) = CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(ContributionAmountMode.IncomeFraction, null, 0.03m, 2_345_678m, null, 1000m,
            RoundingMode.Up, 0.4m));
        (c!.Total, c.Permanente, c.Aportes, c.Base).Should().Be((71_000m, 28_400m, 42_600m, 2_345_678m));
        c.Explicacion.Should().Contain("Ingreso registrado").And.Contain("Ahorro permanente");
    }

    [Fact]
    public void Fraccion_del_salario_minimo()
    {
        var (c, _) = CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(ContributionAmountMode.MinimumWageFraction, null, 0.05m, null, 1_750_905m, 100m,
            RoundingMode.AwayFromZero, null));
        c!.Total.Should().Be(87_500m, "1.750.905 × 5 % = 87.545,25 → a la centena 87.500");
    }

    [Fact]
    public void Sin_la_base_que_pide_el_modo_no_hay_cuota()
    {
        CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(ContributionAmountMode.IncomeFraction, null, 0.03m, null, null, 1m, RoundingMode.Up, null))
            .Problema!.Codigo.Should().Be(CuotasDeAportes.CodigoSinIngreso);
        CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(ContributionAmountMode.MinimumWageFraction, null, 0.03m, null, null, 1m, RoundingMode.Up, null))
            .Problema!.Codigo.Should().Be(CuotasDeAportes.CodigoSinSalarioMinimo);
    }

    [Fact]
    public void El_estado_de_la_cuota_se_calcula_con_lo_pagado()
    {
        var cuota = new AccountInstallment { MemberAccountId = 1, PeriodYear = 2027, PeriodMonth = 1, DueDate = new DateOnly(2027, 1, 5), Amount = 50_000m };

        cuota.Estado(0m, false).Should().Be(EstadoDeCuotaDeCuenta.Pending);
        cuota.Estado(0m, true).Should().Be(EstadoDeCuotaDeCuenta.NotDeducted);
        cuota.Estado(30_000m, true).Should().Be(EstadoDeCuotaDeCuenta.PartiallyPaid);
        cuota.Pendiente(30_000m).Should().Be(20_000m);
        cuota.Estado(50_000m, false).Should().Be(EstadoDeCuotaDeCuenta.Paid);
        cuota.Periodo.Should().Be("2027-01");

        cuota.Anular(DateTime.UtcNow, "Retiro del asociado", null);
        cuota.Estado(0m, false).Should().Be(EstadoDeCuotaDeCuenta.Cancelled);
        cuota.Pendiente(0m).Should().Be(0m);
        var otra = () => cuota.Anular(DateTime.UtcNow, "Otra vez", null);
        otra.Should().Throw<InvalidOperationException>("la anulación se escribe una vez");
    }
}
