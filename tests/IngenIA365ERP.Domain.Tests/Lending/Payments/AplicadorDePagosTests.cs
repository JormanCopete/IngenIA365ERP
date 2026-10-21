using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0514 (FR-071–FR-076, D5–D8, D24, T31, M10): las siete modalidades de <see cref="PaymentModality"/> sobre un
/// <see cref="EstadoDelCredito"/> armado a mano: un crédito de 1.200.000 a 12 cuotas desembolsado el 01-08-2026, el 20-10-2026 con las
/// cuotas 1 y 2 vencidas (seguro, mora, corriente y capital) y la corriente de la 3 causándose. Todo pago cubre primero lo exigible; el
/// excedente nunca entra a conceptos; la cancelación usa el anticipo y nunca el saldo a favor; la misma entrada da la misma distribución.
/// </summary>
public class AplicadorDePagosTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 20);
    private static readonly RedondeoDeCartera Redondeo = RedondeoDeCartera.AlCentavo;

    private static readonly PlanDePagos Plan = GeneradorDePlan.Generar(new EntradaDePlan(1_200_000m, new DateOnly(2026, 8, 1), 12,
        RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, Redondeo));

    /// <summary>Cuota 1: 90.437,91 + 21.705,10; cuota 2: 92.073,71 + 20.069,30; cuota 3: 93.739,11 + 18.403,90.</summary>
    private static List<SaldoDeCuota> Saldos(params SaldoDeCuota[] extra)
    {
        var saldos = Plan.Cuotas.Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)).ToList();
        saldos.AddRange(
        [
            new(1, LoanConcept.Insurance, 1_440m), new(1, LoanConcept.DefaultInterest, 3_000m), new(1, LoanConcept.CurrentInterest, 21_705.10m),
            new(2, LoanConcept.DefaultInterest, 1_000m), new(2, LoanConcept.CurrentInterest, 20_069.30m),
            new(3, LoanConcept.CurrentInterest, 11_000m),
        ]);
        saldos.AddRange(extra);
        return saldos;
    }

    private static EstadoDelCredito Estado(params SaldoDeCuota[] extra) => EstadoDelCredito.Armado(Hoy, Plan, Saldos(extra));

    private static decimal ExigibleDeLaCuota1 => 1_440m + 3_000m + 21_705.10m + 90_437.91m;

    private static decimal ExigibleDeLaCuota2 => 1_000m + 20_069.30m + 92_073.71m;

    private static DistribucionDePago Distribuir(EstadoDelCredito estado, PaymentModality modalidad, decimal valor, PrepaymentChoice? eleccion = null,
        int? cuota = null, Prelacion? prelacion = null) =>
        AplicadorDePagos.Distribuir(estado, new SolicitudDeDistribucion(modalidad, valor, Hoy) { Eleccion = eleccion, Cuota = cuota },
            prelacion ?? Prelacion.PorDefecto, Redondeo);

    private static void CuadraElValor(DistribucionDePago d) =>
        (d.Lineas.Sum(l => l.Monto) + d.Anticipo + d.SaldoAFavor + d.Excedente - d.AnticipoUsado.Sum(l => l.Monto)).Should().Be(d.Valor);

    [Fact]
    public void El_valor_libre_cubre_completa_la_cuota_mas_antigua_antes_de_la_siguiente()
    {
        var d = Distribuir(Estado(), PaymentModality.FreeAmount, 150_000m);

        d.Lineas.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().Equal(
            (1, LoanConcept.Insurance, 1_440m), (1, LoanConcept.DefaultInterest, 3_000m), (1, LoanConcept.CurrentInterest, 21_705.10m),
            (1, LoanConcept.Principal, 90_437.91m), (2, LoanConcept.DefaultInterest, 1_000m), (2, LoanConcept.CurrentInterest, 20_069.30m),
            (2, LoanConcept.Principal, 150_000m - ExigibleDeLaCuota1 - 1_000m - 20_069.30m));
        d.Excedente.Should().Be(0m);
        d.Anticipo.Should().Be(0m);
        d.Cancela.Should().BeFalse();
        CuadraElValor(d);
    }

    [Fact]
    public void Concepto_primero_cubre_un_concepto_en_todas_las_cuotas_antes_del_siguiente()
    {
        var d = Distribuir(Estado(), PaymentModality.FreeAmount, 30_000m, prelacion: new Prelacion(AllocationOrder.ConceptFirst, [], true, false));

        d.Lineas.Select(l => (l.Cuota, l.Concepto, l.Monto)).Should().Equal(
            (1, LoanConcept.Insurance, 1_440m), (1, LoanConcept.DefaultInterest, 3_000m), (2, LoanConcept.DefaultInterest, 1_000m),
            (1, LoanConcept.CurrentInterest, 21_705.10m), (2, LoanConcept.CurrentInterest, 30_000m - 1_440m - 3_000m - 1_000m - 21_705.10m));
    }

    [Fact]
    public void A_vencidas_cubre_solo_lo_exigible_y_lo_demas_es_excedente_que_no_entra_a_conceptos()
    {
        var d = Distribuir(Estado(), PaymentModality.PastDue, 500_000m);

        d.Lineas.Should().OnlyContain(l => l.Cuota == 1 || l.Cuota == 2, "la corriente de la cuota 3 todavía no es exigible");
        d.Lineas.Sum(l => l.Monto).Should().Be(ExigibleDeLaCuota1 + ExigibleDeLaCuota2);
        d.Excedente.Should().Be(500_000m - ExigibleDeLaCuota1 - ExigibleDeLaCuota2, "D5: vuelve al recaudador");
        d.Aplicado.Should().Be(ExigibleDeLaCuota1 + ExigibleDeLaCuota2);
        CuadraElValor(d);
    }

    [Fact]
    public void La_cancelacion_usa_el_anticipo_del_credito_y_nunca_el_saldo_a_favor()
    {
        var estado = Estado(new SaldoDeCuota(null, LoanConcept.Advance, 50_000m), new SaldoDeCuota(null, LoanConcept.CreditBalance, 70_000m));
        var total = estado.ValorParaCancelar + 50_000m;

        var d = Distribuir(estado, PaymentModality.FullPayoff, 2_000_000m);

        d.AnticipoUsado.Should().ContainSingle().Which.Should().Be(new LineaDeDistribucion(null, LoanConcept.Advance, 50_000m), "D7");
        d.Lineas.Sum(l => l.Monto).Should().Be(total, "todo lo cobrable, también la corriente de la 3 causada a la fecha");
        d.Lineas.Should().NotContain(l => l.Concepto == LoanConcept.CreditBalance);
        d.Excedente.Should().Be(2_000_000m - estado.ValorParaCancelar, "el saldo a favor no se aplica solo (D7)");
        d.Cancela.Should().BeTrue();
        CuadraElValor(d);
    }

    [Fact]
    public void La_cancelacion_que_no_alcanza_cubre_por_la_prelacion_y_no_cancela()
    {
        var d = Distribuir(Estado(), PaymentModality.FullPayoff, 300_000m);

        d.Cancela.Should().BeFalse();
        d.Excedente.Should().Be(0m);
        d.Lineas.Sum(l => l.Monto).Should().Be(300_000m);
        d.Lineas.Take(4).Select(l => l.Cuota).Should().OnlyContain(c => c == 1);
    }

    [Fact]
    public void La_cuota_del_ciclo_es_lo_vencido_mas_la_del_periodo_como_anticipo_de_esa_cuota()
    {
        var cuota3 = Plan.Cuotas[2];
        var valorDeLa3 = cuota3.Capital + cuota3.Interes + cuota3.Seguro;

        var d = Distribuir(Estado(), PaymentModality.CycleInstallment, 400_000m);

        d.Lineas.Sum(l => l.Monto).Should().Be(ExigibleDeLaCuota1 + ExigibleDeLaCuota2);
        d.Anticipo.Should().Be(valorDeLa3, "D8: no reduce intereses, cubre la cuota 3 cuando venza");
        d.CuotaDelAnticipo.Should().Be(3);
        d.Excedente.Should().Be(400_000m - ExigibleDeLaCuota1 - ExigibleDeLaCuota2 - valorDeLa3);
        d.Avisos.Should().Equal(AplicadorDePagos.AvisoAnticipoNoReduceIntereses);
        CuadraElValor(d);
    }

    [Fact]
    public void La_cuota_del_ciclo_que_vence_ese_dia_es_solo_lo_exigible()
    {
        var vence = Plan.Cuotas[2].Vencimiento;
        var estado = EstadoDelCredito.Armado(vence, Plan, Saldos());

        var d = AplicadorDePagos.Distribuir(estado, new SolicitudDeDistribucion(PaymentModality.CycleInstallment, 400_000m, vence),
            Prelacion.PorDefecto, Redondeo);

        d.Anticipo.Should().Be(0m);
        d.Lineas.Should().Contain(l => l.Cuota == 3 && l.Concepto == LoanConcept.CurrentInterest, "la 3 vence ese día: es exigible");
        d.Lineas.Should().NotContain(l => l.Cuota == 4);
    }

    [Fact]
    public void La_cuota_especifica_con_anteriores_impagas_se_rechaza_con_las_pendientes()
    {
        var pagar = () => Distribuir(Estado(), PaymentModality.SpecificInstallment, 120_000m, cuota: 2);

        var ex = pagar.Should().Throw<ReglaDeCarteraException>().Which;
        ex.Codigo.Should().Be(AplicadorDePagos.CodigoCuotaEspecificaBloqueada);
        ex.Problema.Datos!["pendingBefore"].Should().BeEquivalentTo(new[] { 1 });
        AplicadorDePagos.Problema(Estado(), new SolicitudDeDistribucion(PaymentModality.SpecificInstallment, 120_000m, Hoy) { Cuota = 2 },
            Prelacion.PorDefecto)!.Codigo.Should().Be(AplicadorDePagos.CodigoCuotaEspecificaBloqueada, "el mismo código en la pantalla (C115)");
    }

    [Fact]
    public void La_cuota_especifica_si_la_linea_lo_permite_cubre_solo_esa_cuota()
    {
        var fueraDeOrden = new Prelacion(AllocationOrder.OldestInstallmentFirst, [], true, cuotaEspecificaFueraDeOrden: true);

        var d = Distribuir(Estado(), PaymentModality.SpecificInstallment, 120_000m, cuota: 2, prelacion: fueraDeOrden);

        d.Lineas.Should().OnlyContain(l => l.Cuota == 2);
        d.Lineas.Sum(l => l.Monto).Should().Be(ExigibleDeLaCuota2);
        d.Excedente.Should().Be(120_000m - ExigibleDeLaCuota2);
    }

    [Fact]
    public void La_cuota_especifica_futura_queda_como_anticipo_de_esa_cuota_y_una_inexistente_no_cabe()
    {
        var alDia = EstadoDelCredito.Armado(Hoy, Plan, Plan.Cuotas.Skip(2).Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)).ToList());

        var d = Distribuir(alDia, PaymentModality.SpecificInstallment, 50_000m, cuota: 5);

        d.Anticipo.Should().Be(50_000m);
        d.CuotaDelAnticipo.Should().Be(5);
        var inexistente = () => Distribuir(alDia, PaymentModality.SpecificInstallment, 50_000m, cuota: 40);
        inexistente.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(AplicadorDePagos.CodigoModalidadNoAdmitida);
    }

    [Fact]
    public void El_anticipo_cubre_primero_lo_vencido_y_no_pasa_de_lo_que_falta_por_vencer()
    {
        var porVencer = Plan.Cuotas.Skip(2).Sum(c => c.Capital + c.Interes + c.Seguro);

        var d = Distribuir(Estado(new SaldoDeCuota(null, LoanConcept.Advance, 10_000m)), PaymentModality.Advance, 2_000_000m);

        d.Lineas.Sum(l => l.Monto).Should().Be(ExigibleDeLaCuota1 + ExigibleDeLaCuota2, "FR-072: todo anticipo cubre primero lo vencido");
        d.Anticipo.Should().Be(porVencer - 10_000m);
        d.CuotaDelAnticipo.Should().BeNull();
        d.Excedente.Should().Be(2_000_000m - ExigibleDeLaCuota1 - ExigibleDeLaCuota2 - d.Anticipo);
        d.Avisos.Should().Equal(AplicadorDePagos.AvisoAnticipoNoReduceIntereses);
    }

    [Fact]
    public void El_abono_sin_eleccion_queda_como_saldo_a_favor_y_con_eleccion_regenera_el_plan()
    {
        var sin = Distribuir(Estado(), PaymentModality.ExtraordinaryPrincipal, 400_000m);

        sin.Lineas.Sum(l => l.Monto).Should().Be(ExigibleDeLaCuota1 + ExigibleDeLaCuota2, "cubre primero lo vencido");
        sin.SaldoAFavor.Should().Be(400_000m - ExigibleDeLaCuota1 - ExigibleDeLaCuota2, "D24");
        sin.PlanNuevo.Should().BeNull();
        sin.Avisos.Should().Equal(AplicadorDePagos.AvisoPreferenciaAsumida);

        var con = Distribuir(Estado(), PaymentModality.ExtraordinaryPrincipal, 400_000m, PrepaymentChoice.ReduceTerm);

        con.AbonoACapital.Should().Be(sin.SaldoAFavor);
        con.Lineas[^1].Should().Be(new LineaDeDistribucion(null, LoanConcept.Principal, sin.SaldoAFavor));
        con.PlanNuevo.Should().NotBeNull();
        con.PlanNuevo!.TotalCapital.Should().Be(Plan.TotalCapital - con.AbonoACapital, "SC-017");
        con.PlanNuevo.Cuotas.Count.Should().BeLessThan(Plan.Cuotas.Count, "reducir el plazo quita cuotas del final");
        con.PlanNuevo.PrimeraCuotaRegenerada.Should().Be(3);

        var alternativas = AplicadorDePagos.Alternativas(Estado(), new SolicitudDeDistribucion(PaymentModality.ExtraordinaryPrincipal, 400_000m, Hoy),
            Prelacion.PorDefecto, Redondeo);
        alternativas.Select(a => a.Eleccion).Should().Equal(PrepaymentChoice.ReduceTerm, PrepaymentChoice.ReduceInstallment);
        alternativas[1].PlanNuevo!.Cuotas.Should().HaveCount(Plan.Cuotas.Count, "reducir la cuota conserva las cuotas");
    }

    [Fact]
    public void El_abono_que_alcanza_el_capital_por_vencer_es_una_cancelacion()
    {
        var d = Distribuir(Estado(), PaymentModality.ExtraordinaryPrincipal, 2_000_000m, PrepaymentChoice.ReduceTerm);

        d.Cancela.Should().BeTrue();
        d.Avisos.Should().Equal(AplicadorDePagos.AvisoTratadoComoCancelacion);
        d.PlanNuevo.Should().BeNull();
        d.Excedente.Should().Be(2_000_000m - Estado().ValorParaCancelar, "D5");
    }

    [Fact]
    public void La_misma_entrada_da_la_misma_distribucion_simular_es_aplicar()
    {
        foreach (var modalidad in Enum.GetValues<PaymentModality>().Where(m => m != PaymentModality.SpecificInstallment))
        {
            var uno = Distribuir(Estado(), modalidad, 180_000m, PrepaymentChoice.ReduceInstallment);
            var otro = Distribuir(Estado(), modalidad, 180_000m, PrepaymentChoice.ReduceInstallment);
            otro.Lineas.Should().Equal(uno.Lineas, modalidad.ToString());
            HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, otro)
                .Should().Be(HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, uno), modalidad.ToString());
            CuadraElValor(uno);
            uno.Lineas.Should().OnlyContain(l => l.Monto > 0m, $"{modalidad}: el excedente nunca entra a conceptos");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_valor_que_no_es_positivo_o_con_mas_de_dos_decimales_se_rechaza(int valor)
    {
        ((Action)(() => Distribuir(Estado(), PaymentModality.FreeAmount, valor))).Should().Throw<ArgumentException>();
        ((Action)(() => Distribuir(Estado(), PaymentModality.FreeAmount, 100.001m))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void El_interes_de_la_gracia_repartida_se_cobra_con_la_cuota_a_la_que_el_plan_lo_carga()
    {
        var plan = GeneradorDePlan.Generar(new EntradaDePlan(1_200_000m, new DateOnly(2026, 8, 1), 8, RepaymentPeriodicity.Monthly,
            AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, Redondeo)
        {
            Gracia = GraceKind.Total, PeriodosDeGracia = 2, TratamientoDeGracia = GraceInterestTreatment.Distributed, CuotasDeDistribucion = 3,
        });
        var q1 = plan.Cuotas[0];
        var regular1 = q1.Interes - q1.InteresDeGracia;
        var todoElInteresDeLa1 = regular1 + plan.InteresDeGracia;
        var fecha = q1.Vencimiento.AddDays(5);
        var estado = EstadoDelCredito.Armado(fecha, plan,
            [.. plan.Cuotas.Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)), new SaldoDeCuota(1, LoanConcept.CurrentInterest, todoElInteresDeLa1)]);

        var d = AplicadorDePagos.Distribuir(estado, new SolicitudDeDistribucion(PaymentModality.PastDue, 1_000_000m, fecha), Prelacion.PorDefecto, Redondeo);

        d.Lineas.Single(l => l.Concepto == LoanConcept.CurrentInterest).Should().Be(new LineaDeDistribucion(1, LoanConcept.CurrentInterest, q1.Interes),
            "de la cuota 1 sólo es exigible su interés con su porción de la gracia; las de las cuotas 2 y 3 vencen con ellas");
        plan.AtribuirInteresDeLaPrimera(todoElInteresDeLa1).Should().BeEquivalentTo(new Dictionary<int, decimal>
        {
            [1] = q1.Interes, [2] = plan.Cuotas[1].InteresDeGracia, [3] = plan.Cuotas[2].InteresDeGracia,
        });
    }

    [Fact]
    public void El_anticipo_cubre_la_cuota_al_vencer_parcial_si_no_alcanza_y_libera_lo_que_sobra_de_la_cuota()
    {
        var cuota3 = Plan.Cuotas[2];
        var alVencer = EstadoDelCredito.Armado(cuota3.Vencimiento, Plan,
        [
            .. Plan.Cuotas.Skip(2).Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)),
            new SaldoDeCuota(3, LoanConcept.CurrentInterest, cuota3.Interes),
            new SaldoDeCuota(3, LoanConcept.Advance, 30_000m), new SaldoDeCuota(null, LoanConcept.Advance, 20_000m),
        ]);

        var parcial = AplicadorDePagos.AplicarAnticipo(alVencer, 3, Prelacion.PorDefecto)!;

        parcial.Usado.Should().Equal(new LineaDeDistribucion(3, LoanConcept.Advance, 30_000m), new LineaDeDistribucion(null, LoanConcept.Advance, 20_000m));
        parcial.Lineas.Should().Equal(new LineaDeDistribucion(3, LoanConcept.CurrentInterest, cuota3.Interes),
            new LineaDeDistribucion(3, LoanConcept.Principal, 50_000m - cuota3.Interes));
        parcial.Cubierta.Should().BeFalse("D6: se aplica parcial con la prelación");
        parcial.Liberado.Should().Be(0m);

        var sobrado = EstadoDelCredito.Armado(cuota3.Vencimiento, Plan,
        [
            .. Plan.Cuotas.Skip(2).Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)),
            new SaldoDeCuota(3, LoanConcept.CurrentInterest, cuota3.Interes - 100m), new SaldoDeCuota(3, LoanConcept.Advance, cuota3.Capital + cuota3.Interes),
        ]);
        var cubierta = AplicadorDePagos.AplicarAnticipo(sobrado, 3, Prelacion.PorDefecto)!;
        cubierta.Cubierta.Should().BeTrue();
        cubierta.Liberado.Should().Be(100m, "lo que sobra del anticipo de la cuota pasa al general");
        AplicadorDePagos.AplicarAnticipo(EstadoDelCredito.Armado(cuota3.Vencimiento, Plan, Saldos()), 3, Prelacion.PorDefecto).Should().BeNull();
    }
}
