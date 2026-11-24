using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Rates;

/// <summary>
/// 2026-11-24 (feature 025, HU2; FR-007–FR-009): la tasa pactada en mensual vencida. La diaria es MV / 30, lineal, sólo en 30/360; la del
/// período es días × MV / 30 (mensual = MV, quincenal = MV / 2, semanal = 7·MV/30); la E.A. derivada (1 + MV)^12 − 1 es la que se compara con
/// la usura, y si la pasa la tasa del día es la mensual equivalente de la usura entre 30. La versión de línea la valida y la guarda con la
/// E.A. derivada a seis decimales; la huella sólo cambia si la versión está en MV. Esperados con Python <c>decimal</c> de 40 dígitos. (nuevo)
/// </summary>
public class TasaMensualVencidaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateTime Ahora = new(2026, 12, 1, 10, 0, 0);

    [Fact]
    public void La_diaria_de_una_MV_es_la_MV_entre_treinta_y_el_periodo_es_lineal()
    {
        var diaria = EquivalenciaDeTasas.TasaDiaria(TasaPactada.Mensual(0.018m), DayCountConvention.Thirty360, RateEquivalence.Linear,
            RepaymentPeriodicity.Biweekly);
        diaria.Should().Be(0.0006m);
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, (int)RepaymentPeriodicity.Monthly, RateEquivalence.Linear).Should().Be(0.018m, "mensual = MV");
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, (int)RepaymentPeriodicity.Biweekly, RateEquivalence.Linear).Should().Be(0.009m, "quincenal = MV / 2");
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, (int)RepaymentPeriodicity.TenDay, RateEquivalence.Linear).Should().Be(0.006m, "decadal = MV / 3");
        EquivalenciaDeTasas.TasaDelPeriodo(diaria, (int)RepaymentPeriodicity.Weekly, RateEquivalence.Linear).Should().Be(0.0042m, "semanal = 7·MV/30");
        (10_000_000m * EquivalenciaDeTasas.TasaDelPeriodo(diaria, 30, RateEquivalence.Linear)).Should().Be(180_000m);
    }

    [Fact]
    public void La_EA_derivada_de_una_MV()
    {
        // (1,018)^12 − 1 = 0,238720531575527997… (Python decimal de 40 dígitos).
        EquivalenciaDeTasas.EfectivaDeMensual(0.018m).Should().Be(0.238720531575527997m);
        EquivalenciaDeTasas.EfectivaInformativaDeMensual(0.018m).Should().Be(0.238721m, "23,8721 % E.A., la que se guarda en AnnualRate");
        TasaPactada.Mensual(0.018m).EfectivaAnual.Should().Be(0.238720531575527997m);
        TasaPactada.Efectiva(0.25m).EfectivaAnual.Should().Be(0.25m);
        EquivalenciaDeTasas.NominalDelPeriodo(TasaPactada.Mensual(0.018m), RepaymentPeriodicity.Biweekly).Should().Be(0.009m);
    }

    [Theory]
    [InlineData(DayCountConvention.Actual365, RateEquivalence.Linear)]
    [InlineData(DayCountConvention.Actual360, RateEquivalence.Linear)]
    [InlineData(DayCountConvention.Thirty360, RateEquivalence.Compound)]
    public void Una_MV_exige_30_360_y_lineal(DayCountConvention convencion, RateEquivalence equivalencia)
    {
        var calcular = () => EquivalenciaDeTasas.TasaDiaria(TasaPactada.Mensual(0.018m), convencion, equivalencia, RepaymentPeriodicity.Monthly);
        calcular.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(EquivalenciaDeTasas.CodigoMensualExige30360);
    }

    [Fact]
    public void Una_MV_admite_ocho_decimales_y_no_mas()
    {
        EquivalenciaDeTasas.EsTasaMensualPactable(0.01812345m).Should().BeTrue("1,812345 % MV");
        EquivalenciaDeTasas.EsTasaPactable(0.01812345m).Should().BeFalse("en E.A. sólo seis");
        EquivalenciaDeTasas.EsTasaMensualPactable(0.018123456m).Should().BeFalse();
        var calcular = () => EquivalenciaDeTasas.ValidarMensual(0.018123456m, DayCountConvention.Thirty360, RateEquivalence.Linear);
        calcular.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa);
    }

    private static EntradaDeTasaDelDia Entrada(decimal mv, decimal? usura) =>
        new(EquivalenciaDeTasas.EfectivaInformativaDeMensual(mv), DefaultRateKind.RelativeToCurrent, null, 1m, usura, 0m,
            DayCountConvention.Thirty360, RateEquivalence.Linear, RepaymentPeriodicity.Biweekly)
        {
            CorrienteMensual = mv,
        };

    [Fact]
    public void Bajo_la_usura_la_MV_causa_su_diaria()
    {
        var r = TasaDelDia.Calcular(Entrada(0.018m, usura: 0.265m));
        r.CorrienteTopada.Should().BeFalse("23,87 % E.A. no pasa la usura de 26,5 %");
        r.TasaDiariaCorriente.Should().Be(0.0006m);
        r.CorrienteEA.Should().Be(0.238720531575527997m, "la E.A. derivada sin redondear a seis");
        r.MoraEA.Should().Be(0.238720531575527997m, "la mora relativa es factor × la E.A. derivada (D4: la mora sigue en E.A.)");
        r.Explicacion.Texto.Should().Contain("MV");
    }

    [Fact]
    public void Sobre_la_usura_la_MV_se_topa_con_la_mensual_de_la_usura()
    {
        // (1,265)^(1/12) − 1 = 0,019782473741388876…; entre 30 = 0,000659415791379629 (el tope de los casos 40 a 43).
        var r = TasaDelDia.Calcular(Entrada(0.022m, usura: 0.265m));
        r.CorrienteTopada.Should().BeTrue("(1,022)^12 − 1 = 29,84 % E.A. pasa la usura");
        r.TasaDiariaCorriente.Should().Be(0.000659415791379629m);
        r.TopeDiarioCorriente.Should().Be(0.000659415791379629m);
    }

    [Fact]
    public void El_plan_en_MV_cobra_saldo_por_MV()
    {
        var entrada = new EntradaDePlan(10_000_000m, new DateOnly(2027, 1, 10), 12, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment,
            0.238721m, DayCountConvention.Thirty360, RateEquivalence.Linear, RedondeoDeCartera.AlCentavo)
        {
            DiaDePago = 10,
            TasaMensual = 0.018m,
        };
        var plan = GeneradorDePlan.Generar(entrada);
        plan.TasaDiaria.Should().Be(0.0006m);
        plan.Cuotas[0].Interes.Should().Be(180_000m);
        plan.Cuotas[1].Interes.Should().Be(decimal.Round(plan.Cuotas[1].SaldoInicial * 0.018m, 2, MidpointRounding.AwayFromZero));

        var quincenal = GeneradorDePlan.Generar(entrada with { Periodicidad = RepaymentPeriodicity.Biweekly });
        quincenal.Cuotas[0].Interes.Should().Be(90_000m);
    }

    // ------------------------------------------------------------------------------------------------ versión de la línea --

    private static BorradorDeVersionDeLinea Version() => new(Hoy, "Tasa en MV aprobada por el consejo", "consumo")
    {
        SubsegmentCode = "SinLibranza",
        RateBasis = RateBasis.MonthlyNominal,
        MonthlyRate = 0.018m,
        DefaultRateFactor = 1.5m,
        MinAmount = 500_000m,
        MaxAmount = 30_000_000m,
        MinTermMonths = 1,
        MaxTermMonths = 60,
        Periodicities = [RepaymentPeriodicity.Monthly, RepaymentPeriodicity.Biweekly],
        AmortizationSystems = [AmortizationSystem.LevelPayment],
        DayCount = DayCountConvention.Thirty360,
        RateEquivalence = RateEquivalence.Linear,
        RepaymentChannels = [RepaymentChannel.Cashier],
        RateBands = [new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, MonthlyRate = 0.0195m }],
    };

    [Fact]
    public void La_version_en_MV_se_guarda_con_la_EA_derivada_y_su_huella_incluye_la_MV()
    {
        CreditLineVersion.Validar(Version(), Hoy).Should().BeNull();
        var r = CreditLineVersion.Registrar(3, Version(), 1, Hoy, Ahora);
        var v = r.Version;
        v.RateBasis.Should().Be(RateBasis.MonthlyNominal);
        v.MonthlyRate.Should().Be(0.018m);
        v.AnnualRate.Should().Be(0.238721m);
        v.Id = 40;
        var bandas = r.Bandas(40);
        bandas[0].MonthlyRate.Should().Be(0.0195m);
        bandas[0].AnnualRate.Should().Be(EquivalenciaDeTasas.EfectivaInformativaDeMensual(0.0195m));
        v.HuellaDeLoGuardado(bandas, r.Documentos(40), r.Seguros(40), r.Garantias(40)).Should().Be(v.ContentSha256);
        CreditLineVersion.Huella(Version() with { MonthlyRate = 0.0181m }).Should().NotBe(v.ContentSha256);
    }

    [Fact]
    public void En_EA_la_huella_no_cambia_por_la_base()
    {
        var ea = Version() with
        {
            RateBasis = RateBasis.EffectiveAnnual, MonthlyRate = null, AnnualRate = 0.238721m,
            RateBands = [new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, AnnualRate = 0.26m }],
        };
        var mv = Version();
        CreditLineVersion.Huella(ea).Should().NotBe(CreditLineVersion.Huella(mv with
        {
            RateBands = [new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, MonthlyRate = 0.0195m }],
        }), "en MV la base y la MV entran a la huella");
        // En E.A. no entran: la huella es la de antes de la feature 025 (sin «rateBasis» ni «monthlyRate»).
        CreditLineVersion.Huella(ea).Should().Be(CreditLineVersion.Huella(ea with { RateBasis = RateBasis.EffectiveAnnual }));
    }

    [Theory]
    [InlineData(DayCountConvention.Actual365, RateEquivalence.Linear)]
    [InlineData(DayCountConvention.Thirty360, RateEquivalence.Compound)]
    public void La_version_en_MV_exige_30_360_lineal(DayCountConvention convencion, RateEquivalence equivalencia)
    {
        CreditLineVersion.Validar(Version() with { DayCount = convencion, RateEquivalence = equivalencia }, Hoy)!.Codigo
            .Should().Be(EquivalenciaDeTasas.CodigoMensualExige30360);
    }

    [Fact]
    public void La_version_en_MV_rechaza_lo_que_no_cuadra()
    {
        CreditLineVersion.Validar(Version() with { MonthlyRate = 0.018123456m }, Hoy)!.Codigo.Should().Be(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa);
        CreditLineVersion.Validar(Version() with { MonthlyRate = null }, Hoy)!.Datos!["field"].Should().Be("monthlyRate");
        CreditLineVersion.Validar(Version() with { RateBands = [new BorradorDeBanda(1) { AnnualRate = 0.25m }] }, Hoy)!.Datos!["field"]
            .Should().Be("rateBands[1]");
        CreditLineVersion.Validar(Version() with { RateBasis = RateBasis.EffectiveAnnual, AnnualRate = 0.25m }, Hoy)!.Datos!["field"]
            .Should().Be("monthlyRate", "en E.A. no va la MV");
    }
}
