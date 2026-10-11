using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Schedules;

/// <summary>
/// Feature 013, T0332 (propiedad, SC-017; T29, R37): para montos, plazos, sistemas, convenciones, equivalencias, periodicidades,
/// redondeos, extraordinarias, gracias y seguros al azar —con semilla fija, para que una falla se reproduzca— la suma del capital del
/// plan es el monto al centavo, la última cuota absorbe el residuo y deja el saldo en cero, ningún capital es negativo y
/// <c>Lending.Schedule.PrincipalMismatch</c> nunca sale del generador. El control de usura corre sobre cada plan sin fallar.
/// </summary>
public class SumaDeCapitalDelPlanTests
{
    private const int Semilla = 20261010;
    private const int Planes = 600;

    private static readonly RepaymentPeriodicity[] Periodicidades =
    [
        RepaymentPeriodicity.Weekly, RepaymentPeriodicity.TenDay, RepaymentPeriodicity.Biweekly, RepaymentPeriodicity.Monthly,
        RepaymentPeriodicity.Bimonthly, RepaymentPeriodicity.Quarterly, RepaymentPeriodicity.SemiAnnual, RepaymentPeriodicity.Annual,
    ];

    [Fact]
    public void La_suma_del_capital_es_el_monto_al_centavo_en_planes_al_azar()
    {
        var azar = new Random(Semilla);
        var casos = 0;
        using var _ = new AssertionScope();
        for (var i = 0; i < Planes; i++)
        {
            var entrada = EntradaAlAzar(azar);
            PlanDePagos plan;
            try
            {
                plan = GeneradorDePlan.Generar(entrada);
            }
            catch (ReglaDeCarteraException ex) when (ex.Codigo == GeneradorDePlan.CodigoCapitalDescuadrado)
            {
                throw new Xunit.Sdk.XunitException($"Plan {i}: el generador descuadró el capital ({ex.Message}) con {entrada}.");
            }

            var donde = $"plan {i} ({entrada.Sistema}, {entrada.Periodicidad}, {entrada.Periodos} períodos, {entrada.Convencion}, "
                + $"{entrada.Equivalencia}, {entrada.Redondeo.Unidad} {entrada.Redondeo.Modo} {entrada.Redondeo.Momento}, gracia {entrada.Gracia})";
            plan.TotalCapital.Should().Be(entrada.Monto, donde);
            plan.Cuotas[^1].SaldoFinal.Should().Be(0m, donde);
            plan.Cuotas.Should().OnlyContain(c => c.Capital >= 0m && c.Interes >= 0m && c.SaldoFinal >= 0m, donde);
            plan.Cuotas.Select(c => c.Vencimiento).Should().BeInAscendingOrder(donde);
            plan.Cuotas.Should().HaveCount(entrada.Gracia == GraceKind.Total ? entrada.Periodos - entrada.PeriodosDeGracia : entrada.Periodos, donde);
            plan.Cuotas.Sum(c => c.InteresDeGracia).Should().Be(plan.InteresDeGracia, $"{donde}: la gracia total se cobra entera, ni más ni menos");
            plan.Explicacion.Lineas.Should().Contain(l => l.Contains("absorbe el residuo"), donde);

            if (i % 10 == 0)
            {
                var usura = ControlDeUsura.Evaluar(plan, new UsuraCertificada(0.40m, Guid.Empty, "CONSUMO", entrada.FechaDesembolso));
                usura.Veredicto.Should().NotBe(VeredictoDeUsura.SinUsura, donde);
            }
            casos++;
        }
        casos.Should().Be(Planes);
    }

    private static EntradaDePlan EntradaAlAzar(Random azar)
    {
        var monto = azar.Next(100_000, 50_000_000) + azar.Next(0, 100) / 100m;
        var ea = azar.Next(0, 400_000) / 1_000_000m;
        var desembolso = new DateOnly(2026, 1, 1).AddDays(azar.Next(0, 1_500));
        var convencion = (DayCountConvention)azar.Next(1, 4);
        var equivalencia = azar.Next(0, 4) == 0 ? RateEquivalence.Linear : RateEquivalence.Compound;
        var redondeo = new RedondeoDeCartera(
            RedondeoDeCartera.UnidadesAdmitidas[azar.Next(RedondeoDeCartera.UnidadesAdmitidas.Count)],
            (RoundingMode)azar.Next(1, 5), (RoundingMoment)azar.Next(1, 4));

        if (azar.Next(0, 12) == 0)
        {
            return new EntradaDePlan(monto, desembolso, 1, RepaymentPeriodicity.SinglePayment, AmortizationSystem.Bullet, ea, convencion,
                equivalencia, redondeo) { PrimerVencimiento = desembolso.AddDays(azar.Next(30, 1_100)) };
        }

        var periodicidad = Periodicidades[azar.Next(Periodicidades.Length)];
        var periodos = azar.Next(1, 61);
        var sistema = azar.Next(0, 3) == 0 ? AmortizationSystem.ConstantPrincipal : AmortizationSystem.LevelPayment;

        var gracia = GraceKind.None;
        var periodosDeGracia = 0;
        GraceInterestTreatment? tratamiento = null;
        int? distribucion = null;
        if (periodos > 2 && azar.Next(0, 4) == 0)
        {
            gracia = azar.Next(0, 2) == 0 ? GraceKind.Principal : GraceKind.Total;
            periodosDeGracia = azar.Next(1, Math.Min(6, periodos - 1) + 1);
            if (gracia == GraceKind.Total)
            {
                tratamiento = azar.Next(0, 2) == 0 ? GraceInterestTreatment.FirstInstallment : GraceInterestTreatment.Distributed;
                if (tratamiento == GraceInterestTreatment.Distributed) distribucion = azar.Next(1, periodos - periodosDeGracia + 1);
            }
        }

        var pagos = gracia == GraceKind.Total ? periodos - periodosDeGracia : periodos;
        var primeraQueAmortiza = gracia == GraceKind.Principal ? periodosDeGracia + 1 : 1;
        var extraordinarias = new List<CuotaExtraordinaria>();
        if (pagos >= primeraQueAmortiza + 1 && azar.Next(0, 3) == 0)
        {
            var cuantas = azar.Next(1, 3);
            for (var k = 0; k < cuantas; k++)
            {
                var cuota = azar.Next(primeraQueAmortiza, pagos + 1);
                extraordinarias.Add(new CuotaExtraordinaria(cuota, decimal.Round(monto * azar.Next(1, 15) / 100m, 2)));
            }
        }

        var seguros = new List<SeguroDelPlan>();
        if (azar.Next(0, 3) == 0)
        {
            seguros.Add(new SeguroDelPlan("VIDA", azar.Next(0, 2) == 0 ? InsuranceBase.OutstandingBalance : InsuranceBase.InitialAmount,
                azar.Next(1, 2_000) / 1_000_000m, azar.Next(0, 2) == 0 ? periodicidad : RepaymentPeriodicity.Monthly, azar.Next(0, 2) == 0));
        }

        return new EntradaDePlan(monto, desembolso, periodos, periodicidad, sistema, ea, convencion, equivalencia, redondeo)
        {
            DiaDePago = azar.Next(0, 3) == 0 ? null : azar.Next(1, 32),
            Gracia = gracia,
            PeriodosDeGracia = periodosDeGracia,
            TratamientoDeGracia = tratamiento,
            CuotasDeDistribucion = distribucion,
            Extraordinarias = extraordinarias,
            Seguros = seguros,
        };
    }
}
