using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// 2026-11-24 (feature 025, HU2; FR-007, FR-008, Principio VIII): la forma de la tasa en mensual vencida en <see cref="CreditLineVersionInputValidator"/>
/// y su paso al borrador del dominio: con base MV, tasa fija, su MV (hasta ocho decimales de fracción), sin E.A. digitada y en 30/360 lineal; en E.A.,
/// sin MV. El borrador lleva la MV y deja la E.A. para que el dominio la derive. (nuevo)
/// </summary>
public class TasaMensualEnLaVersionDeLineaTests
{
    private static CreditLineVersionInput Version(RateBasis basis, decimal? mv, decimal? ea = null, DayCountConvention dias = DayCountConvention.Thirty360,
        RateEquivalence equivalencia = RateEquivalence.Linear, RateKind clase = RateKind.Fixed) => new()
    {
        ValidFrom = new DateOnly(2026, 12, 1),
        Reason = "Tasa en MV",
        ModalityCode = "CONSUMO",
        Rates = new CreditLineRatesInput
        {
            Current = new CurrentRateInput(clase, ea) { Basis = basis, MonthlyRate = mv },
            Bands = [new RateBandInput(AmountFrom: 0m, AmountTo: 5_000_000m) { MonthlyRate = mv }],
        },
        Amounts = new AmountRangeInput(500_000m, 30_000_000m),
        Terms = new TermRangeInput(1, 60),
        PaymentForms = [RepaymentChannel.Cashier],
        DayCount = dias,
        RateEquivalence = equivalencia,
    };

    private static IEnumerable<string> Errores(CreditLineVersionInput v) =>
        new CreditLineVersionInputValidator().Validate(v).Errors.Select(e => e.ErrorMessage);

    [Fact]
    public void Una_MV_en_30_360_lineal_es_valida_y_el_borrador_la_lleva()
    {
        var v = Version(RateBasis.MonthlyNominal, 0.018m);
        Errores(v).Should().BeEmpty();
        var b = v.Borrador(new Dictionary<Guid, int>());
        b.RateBasis.Should().Be(RateBasis.MonthlyNominal);
        b.MonthlyRate.Should().Be(0.018m);
        b.AnnualRate.Should().BeNull("la E.A. la deriva el dominio");
        b.RateBands.Single().MonthlyRate.Should().Be(0.018m);
        b.Normalizada().AnnualRate.Should().Be(0.238721m);
    }

    [Fact]
    public void Una_MV_fuera_de_30_360_lineal_no_pasa()
    {
        var real = new CreditLineVersionInputValidator().Validate(Version(RateBasis.MonthlyNominal, 0.018m, dias: DayCountConvention.Actual365));
        real.Errors.Should().Contain(e => e.ErrorCode == "Lending.Rate.MonthlyRequiresThirty360");
        Errores(Version(RateBasis.MonthlyNominal, 0.018m, equivalencia: RateEquivalence.Compound)).Should().Contain(e => e.Contains("lineal"));
    }

    [Fact]
    public void La_MV_es_obligatoria_con_ocho_decimales_sin_EA_y_solo_fija()
    {
        Errores(Version(RateBasis.MonthlyNominal, null)).Should().Contain("Indicá la tasa mensual vencida.");
        Errores(Version(RateBasis.MonthlyNominal, 0.018123456m)).Should().Contain(e => e.Contains("cuatro decimales"));
        Errores(Version(RateBasis.MonthlyNominal, 0.018m, ea: 0.24m)).Should().Contain(e => e.Contains("no se digita la E.A."));
        Errores(Version(RateBasis.MonthlyNominal, 0.018m, clase: RateKind.Variable)).Should().Contain("La tasa variable se pacta en efectiva anual.");
    }

    [Fact]
    public void En_EA_no_va_la_MV_y_lo_de_siempre_sigue_igual()
    {
        Errores(Version(RateBasis.EffectiveAnnual, 0.018m, ea: 0.24m, dias: DayCountConvention.Actual365, equivalencia: RateEquivalence.Compound))
            .Should().Contain("La tasa mensual sólo va con la base «mensual vencida».");
        var ea = Version(RateBasis.EffectiveAnnual, null, ea: 0.24m, dias: DayCountConvention.Actual365, equivalencia: RateEquivalence.Compound);
        Errores(ea).Should().BeEmpty();
        var b = ea.Borrador(new Dictionary<Guid, int>());
        (b.RateBasis, b.MonthlyRate, b.AnnualRate).Should().Be((RateBasis.EffectiveAnnual, (decimal?)null, (decimal?)0.24m));
    }

    [Fact]
    public void El_plan_tipo_de_una_linea_en_MV_calcula_con_la_MV_y_controla_la_EA_derivada()
    {
        var b = Version(RateBasis.MonthlyNominal, 0.018m).Borrador(new Dictionary<Guid, int>()) with
        {
            Periodicities = [RepaymentPeriodicity.Monthly],
            DefaultPeriodicity = RepaymentPeriodicity.Monthly,
            AmortizationSystems = [AmortizationSystem.LevelPayment],
            DefaultAmortizationSystem = AmortizationSystem.LevelPayment,
        };
        PlanTipoDeLinea.TasaMensual(b, null).Should().Be(0.018m);
        PlanTipoDeLinea.TasaEfectiva(b, null, null).Should().Be(0.238721m, "la E.A. derivada que se sella en AnnualRate");
        PlanTipoDeLinea.CampoDeLaVersion(b).Should().Be("monthlyRate");
        var (_, plan, tasa) = PlanTipoDeLinea.PlanDeConsulta(b, null, new DateOnly(2026, 12, 1), 10_000_000m, 12, RepaymentPeriodicity.Monthly);
        plan.TasaMensual.Should().Be(0.018m);
        tasa.Should().Be(0.238721m);
        Domain.Lending.MotorDeCartera.GenerarPlan(plan).TasaDiaria.Should().Be(0.0006m);
    }
}
