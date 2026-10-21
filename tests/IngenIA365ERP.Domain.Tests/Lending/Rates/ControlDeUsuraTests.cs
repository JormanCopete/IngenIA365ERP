using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Rates;

/// <summary>
/// Feature 013, T0328 (T28, R36; FR-021, SC-002): <see cref="ControlDeUsura"/> compara la <b>TIR diaria</b> de los flujos con fechas
/// reales y los cargos computables, anualizada sobre 365, nunca la nominal; un seguro computa si la cooperativa retiene prima o se
/// autoasegura; el resultado trae efectiva, usura (valor y PublicId), holgura, cargos y explicación; la sobretasa de cargos es la
/// efectiva con cargos menos la sin cargos; sin usura dice «sin usura». Las efectivas esperadas son las de los casos dorados (03 y
/// 04), calculadas con Python <c>decimal</c> de 40 dígitos.
/// </summary>
public class ControlDeUsuraTests
{
    private static readonly Guid Usura252 = Guid.Parse("0b7a3c8e-1d1a-4f6e-9a01-000000000003");

    private static EntradaDePlan Real360(decimal ea = 0.25m, IReadOnlyList<SeguroDelPlan>? seguros = null) =>
        new(3_000_000m, new DateOnly(2027, 3, 10), 12, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, ea,
            DayCountConvention.Actual360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo)
        { Seguros = seguros ?? [] };

    private static UsuraCertificada Usura(decimal tasa) => new(tasa, Usura252, "CONSUMO", new DateOnly(2027, 3, 1));

    [Fact]
    public void Real_360_sobre_una_EA_bajo_la_usura_la_supera_por_la_efectiva_y_bloquea()
    {
        var plan = GeneradorDePlan.Generar(Real360());
        var r = ControlDeUsura.Evaluar(plan, Usura(0.252m));

        // La pactada (0,25) y la nominal anual (12 × 0,018769…) pasan; la efectiva resultante no.
        (12 * plan.TasaNominalDelPeriodo).Should().BeLessThan(0.252m);
        plan.Entrada.TasaEfectivaAnual.Should().BeLessThan(0.252m);
        decimal.Round(r.EfectivaConCargos, 6).Should().Be(0.253880m);
        r.Veredicto.Should().Be(VeredictoDeUsura.Excede);
        r.Pasa.Should().BeFalse();
        decimal.Round(r.Holgura!.Value, 6).Should().Be(-0.001880m);
        decimal.Round(r.Diferencia!.Value, 6).Should().Be(0.001880m);
        r.Usura.Should().Be(0.252m);
        r.UsuraPublicId.Should().Be(Usura252);
        r.ClaveDeUsura.Should().Be("CONSUMO");
        r.Explicacion.Texto.Should().Contain("TIR diaria").And.Contain("la supera");
    }

    [Fact]
    public void La_misma_EA_en_30_360_pasa()
    {
        var plan = GeneradorDePlan.Generar(Real360() with { Convencion = DayCountConvention.Thirty360 });
        var r = ControlDeUsura.Evaluar(plan, Usura(0.252m));
        r.Veredicto.Should().Be(VeredictoDeUsura.Pasa);
        r.Holgura.Should().BePositive();
    }

    [Fact]
    public void Un_seguro_que_no_retiene_no_computa_y_uno_que_retiene_si()
    {
        var sinRetencion = new SeguroDelPlan("VIDA", InsuranceBase.OutstandingBalance, 0.0005m, RepaymentPeriodicity.Monthly, Computa: false);
        var conRetencion = sinRetencion with { Computa = true };
        var planBase = GeneradorDePlan.Generar(Real360(0.24m));
        var planSin = GeneradorDePlan.Generar(Real360(0.24m, [sinRetencion]));
        var planCon = GeneradorDePlan.Generar(Real360(0.24m, [conRetencion]));

        var baseR = ControlDeUsura.Evaluar(planBase, Usura(0.252m));
        var sin = ControlDeUsura.Evaluar(planSin, Usura(0.252m));
        var con = ControlDeUsura.Evaluar(planCon, Usura(0.252m));

        sin.EfectivaConCargos.Should().Be(baseR.EfectivaConCargos, "el seguro que paga entera la prima a la aseguradora no computa");
        sin.CargosComputables.Should().Be(0m);
        sin.SobretasaDeCargos.Should().Be(0m);

        con.EfectivaConCargos.Should().BeGreaterThan(con.EfectivaSinCargos);
        con.SobretasaDeCargos.Should().Be(con.EfectivaConCargos - con.EfectivaSinCargos, "la sella el desembolso de US3");
        con.EfectivaSinCargos.Should().Be(baseR.EfectivaConCargos);
        con.CargosComputables.Should().Be(planCon.TotalSeguro);
        con.Cargos.Should().ContainSingle(c => c.Codigo == "VIDA");
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("0", false, false)]
    [InlineData("0.150000", false, true)]
    [InlineData(null, true, true)]
    public void Computa_el_seguro_que_retiene_prima_o_se_autoasegura(string? retenida, bool autoaseguro, bool computa) =>
        CreditLineInsurance.ComputaParaUsura(retenida is null ? null : decimal.Parse(retenida, System.Globalization.CultureInfo.InvariantCulture), autoaseguro)
            .Should().Be(computa);

    [Fact]
    public void Con_el_seguro_que_computa_del_caso_04_la_efectiva_sube_y_pasa_con_holgura()
    {
        var entrada = new EntradaDePlan(2_400_000m, new DateOnly(2027, 5, 20), 6, RepaymentPeriodicity.Monthly, AmortizationSystem.ConstantPrincipal,
            0.18m, DayCountConvention.Thirty360, RateEquivalence.Compound, new RedondeoDeCartera(1m, RoundingMode.AwayFromZero, RoundingMoment.PerConcept))
        {
            Seguros = [new SeguroDelPlan("DEUDORES", InsuranceBase.OutstandingBalance, 0.001m, RepaymentPeriodicity.Monthly, Computa: true)],
        };
        var r = ControlDeUsura.Evaluar(GeneradorDePlan.Generar(entrada), Usura(0.26m));
        decimal.Round(r.EfectivaConCargos, 6).Should().Be(0.192309m);
        decimal.Round(r.EfectivaSinCargos, 6).Should().Be(0.178402m);
        r.Veredicto.Should().Be(VeredictoDeUsura.Pasa);
        decimal.Round(r.Holgura!.Value, 6).Should().Be(0.067691m);
        r.CargosComputables.Should().Be(8400m);
    }

    [Fact]
    public void Un_cargo_al_desembolso_reduce_lo_recibido_y_sube_la_efectiva()
    {
        var plan = GeneradorDePlan.Generar(Real360(0.24m) with { Convencion = DayCountConvention.Thirty360 });
        var sin = ControlDeUsura.Evaluar(plan, Usura(0.30m));
        var con = ControlDeUsura.Evaluar(plan, Usura(0.30m), [new CargoDeUsura("ESTUDIO", 30_000m)]);
        con.EfectivaConCargos.Should().BeGreaterThan(sin.EfectivaConCargos);
        con.EfectivaSinCargos.Should().Be(sin.EfectivaSinCargos);
        con.CargosComputables.Should().Be(30_000m);
    }

    [Fact]
    public void Sin_usura_certificada_el_resultado_lo_dice_y_trae_la_efectiva()
    {
        var r = ControlDeUsura.Evaluar(GeneradorDePlan.Generar(Real360()), usura: null);
        r.Veredicto.Should().Be(VeredictoDeUsura.SinUsura, "la aplicación lo traduce a NationalValues.Missing");
        r.Usura.Should().BeNull();
        r.UsuraPublicId.Should().BeNull();
        r.Holgura.Should().BeNull();
        decimal.Round(r.EfectivaConCargos, 6).Should().Be(0.253880m);
    }

    [Fact]
    public void La_efectiva_de_un_solo_flujo_es_la_pactada_en_real_365()
    {
        // Caso 06: 181 días; la efectiva resultante es la EA pactada al sexto decimal.
        var entrada = new EntradaDePlan(2_000_000m, new DateOnly(2026, 11, 10), 1, RepaymentPeriodicity.SinglePayment, AmortizationSystem.Bullet,
            0.20m, DayCountConvention.Actual365, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo) { PrimerVencimiento = new DateOnly(2027, 5, 10) };
        var r = ControlDeUsura.Evaluar(GeneradorDePlan.Generar(entrada), Usura(0.265m));
        decimal.Round(r.EfectivaConCargos, 6).Should().Be(0.200000m);
    }

    [Fact]
    public void La_efectiva_anual_de_flujos_fechados_anualiza_sobre_365()
    {
        // −100 hoy y +110 a los 365 días: TIR diaria = 1,1^(1/365) − 1 y efectiva 0,10.
        var hoy = new DateOnly(2027, 1, 1);
        var e = ControlDeUsura.EfectivaAnual(hoy, [(hoy, -100m), (hoy.AddDays(ControlDeUsura.DiasDelAnio), 110m)]);
        decimal.Round(e, 12).Should().Be(0.10m);
    }
}
