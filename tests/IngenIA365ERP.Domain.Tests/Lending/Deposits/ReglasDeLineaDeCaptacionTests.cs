using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, T064 (US1; data-model §2.1, §2.2, §2.5; FR-007, FR-014, FR-018, FR-028, FR-029, FR-038, FR-043): las invariantes de las versiones de
/// las líneas de captación y de aportes —sin retroactivas, clase permitida por el perfil, topes de un fondo de empleados, tramos sin solape, bloques
/// de cada clase completos y sólo en su clase, tasas con seis decimales— y la huella de la versión.
/// </summary>
public class ReglasDeLineaDeCaptacionTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static BorradorDeVersionDeCaptacion ALaVista(params BorradorDeTramoDeCaptacion[] tramos) =>
        new(Hoy, "Alta", DepositClass.Demand)
        {
            AnnualRate = tramos.Length == 0 ? 0.02m : null,
            AllowsPartialWithdrawals = true,
            RateTiers = tramos,
        };

    private static BorradorDeVersionDeCaptacion Cdat() =>
        new(Hoy, "Alta", DepositClass.TermDeposit)
        {
            AnnualRate = 0.09m,
            MinTermDays = 30,
            MaxTermDays = 540,
            InterestPeriodicities = [RepaymentPeriodicity.SinglePayment, RepaymentPeriodicity.Monthly],
            InterestTimings = [InterestTiming.Arrears],
            AutoRollover = true,
            RolloverRateRule = RolloverRateRule.CurrentTableRate,
            CapitalizesOnRollover = false,
            GraceBusinessDays = 3,
            GraceAccrues = false,
            AfterMaturityTreatment = AfterMaturityTreatment.StopAccruing,
            EarlyRedemptionAllowed = false,
        };

    private static BorradorDeVersionDeAportes Aportes() =>
        new(Hoy, "Alta")
        {
            MinimumMode = ContributionAmountMode.FixedAmount,
            MinimumAmount = 500_000m,
            InstallmentMode = ContributionAmountMode.IncomeFraction,
            InstallmentFraction = 0.03m,
        };

    // ------------------------------------------------------------------------------------------------------- captación --

    [Fact]
    public void Una_version_a_la_vista_bien_formada_pasa()
    {
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista(), Hoy).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, 999_999.99m, AnnualRate: 0.01m),
            new BorradorDeTramoDeCaptacion(2, 1_000_000m, null, AnnualRate: 0.03m)), Hoy).Should().BeNull();
    }

    [Fact]
    public void Una_version_no_rige_antes_de_hoy()
    {
        var problema = ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { ValidFrom = Hoy.AddDays(-1) }, Hoy);

        problema!.Codigo.Should().Be("Lending.DepositLine.Retroactive");
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { ValidFrom = Hoy.AddDays(-1) }, Hoy)!.Codigo
            .Should().Be("Lending.ContributionLine.Retroactive");
    }

    [Fact]
    public void Los_tramos_de_saldo_que_se_cruzan_o_dejan_hueco_son_RateTiersOverlap()
    {
        var cruzados = ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, 1_000_000m, AnnualRate: 0.01m),
            new BorradorDeTramoDeCaptacion(2, 1_000_000m, null, AnnualRate: 0.03m));
        var conHueco = ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, 999_999.99m, AnnualRate: 0.01m),
            new BorradorDeTramoDeCaptacion(2, 1_500_000m, null, AnnualRate: 0.03m));
        var sinCero = ALaVista(new BorradorDeTramoDeCaptacion(1, 100m, null, AnnualRate: 0.01m));
        var abiertoEnMedio = ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, null, AnnualRate: 0.01m),
            new BorradorDeTramoDeCaptacion(2, 1_000_000m, null, AnnualRate: 0.03m));

        foreach (var b in new[] { cruzados, conHueco, sinCero, abiertoEnMedio })
            ReglasDeLineaDeCaptacion.ValidarVersion(b, Hoy)!.Codigo.Should().Be("Lending.Savings.RateTiersOverlap");
    }

    [Fact]
    public void Una_tasa_con_mas_de_seis_decimales_es_TooPrecise_y_una_negativa_es_invalida()
    {
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { AnnualRate = 0.0123456m }, Hoy)!.Codigo.Should().Be("Lending.Rate.TooPrecise");
        var negativa = ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { AnnualRate = -0.01m }, Hoy)!;
        negativa.Codigo.Should().Be("Lending.DepositLine.Invalid");
        negativa.Datos!["field"].Should().Be("annualRate");
    }

    [Fact]
    public void Tasa_fija_y_variable_llevan_lo_suyo()
    {
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { AnnualRate = null }, Hoy)!.Datos!["field"].Should().Be("annualRate");
        var variable = ALaVista() with { RateKind = RateKind.Variable, AnnualRate = null, IndexCode = "IBR", IndexKey = "3M", Margin = 0.01m };
        ReglasDeLineaDeCaptacion.ValidarVersion(variable, Hoy).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarVersion(variable with { IndexKey = null }, Hoy)!.Datos!["field"].Should().Be("indexCode");
        ReglasDeLineaDeCaptacion.ValidarVersion(variable with { RateFloor = 0.05m, RateCeiling = 0.04m }, Hoy)!.Datos!["field"].Should().Be("rateFloor");
    }

    [Fact]
    public void El_cdat_completo_pasa_y_sin_sus_condiciones_es_LineIncomplete()
    {
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat(), Hoy).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { MaxTermDays = null }, Hoy)!.Codigo.Should().Be("Lending.TermDeposit.LineIncomplete");
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { InterestTimings = [] }, Hoy)!.Codigo.Should().Be("Lending.TermDeposit.LineIncomplete");
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { AfterMaturityTreatment = AfterMaturityTreatment.MoveToDemand }, Hoy)!.Datos!["field"]
            .Should().Be("moveToDemandLineId");
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { EarlyRedemptionAllowed = true, EarlyRedemptionPenaltyMode = EarlyRedemptionPenaltyMode.PenaltyRate },
            Hoy)!.Datos!["field"].Should().Be("penaltyAnnualRate");
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { BalanceMethod = BalanceMethod.AverageBalance }, Hoy)!.Datos!["field"].Should().Be("balanceMethod");
        ReglasDeLineaDeCaptacion.ValidarVersion(Cdat() with { AllowsPartialWithdrawals = true }, Hoy)!.Datos!["field"].Should().Be("allowsPartialWithdrawals");
    }

    [Fact]
    public void Las_condiciones_de_una_clase_no_van_en_otra()
    {
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { MinTermDays = 30 }, Hoy)!.Datos!["field"].Should().Be("termDeposit");
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { MinInstallmentAmount = 50_000m }, Hoy)!.Datos!["field"].Should().Be("contractual");
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { WithdrawableOnlyOnMembershipWithdrawal = true }, Hoy)!.Datos!["field"].Should().Be("permanent");
    }

    [Fact]
    public void El_contractual_dice_su_cuota_plazo_e_incumplimiento()
    {
        var contractual = new BorradorDeVersionDeCaptacion(Hoy, "Alta", DepositClass.Contractual)
        {
            AnnualRate = 0.05m, InstallmentPeriodicity = RepaymentPeriodicity.Monthly, MinInstallmentAmount = 50_000m, MinTermMonths = 6,
            MaxTermMonths = 36, BreachEffect = ContractualBreachEffect.MoveToDemand,
        };

        ReglasDeLineaDeCaptacion.ValidarVersion(contractual, Hoy).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarVersion(contractual with { BreachEffect = null }, Hoy)!.Codigo.Should().Be("Lending.TermDeposit.LineIncomplete");
        ReglasDeLineaDeCaptacion.ValidarVersion(contractual with { BreachEffect = ContractualBreachEffect.BreachRate }, Hoy)!.Datos!["field"]
            .Should().Be("breachAnnualRate");
    }

    [Fact]
    public void La_tarifa_de_retencion_la_sugiere_la_clase()
    {
        ReglasDeLineaDeCaptacion.TarifaDeRetencionDe(ALaVista()).Should().Be(ParametrosDeCartera.RetencionTarifaAhorro);
        ReglasDeLineaDeCaptacion.TarifaDeRetencionDe(Cdat()).Should().Be(ParametrosDeCartera.RetencionTarifaCdat);
        ReglasDeLineaDeCaptacion.ValidarVersion(ALaVista() with { WithholdingRateParameterCode = "Otra.Clave" }, Hoy)!.Datos!["field"]
            .Should().Be("withholdingRateParameterCode");
    }

    [Fact]
    public void La_clase_no_cambia_entre_versiones()
    {
        ReglasDeLineaDeCaptacion.ValidarClaseInmutable(DepositClass.Demand, DepositClass.Demand).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarClaseInmutable(DepositClass.Demand, DepositClass.TermDeposit)!.Codigo.Should().Be("Lending.DepositLine.ClassImmutable");
    }

    [Fact]
    public void US1_1_sin_perfil_que_autorice_captar_la_linea_no_se_activa()
    {
        var problema = ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(DepositClass.Demand, false, "MULTIACTIVA", Guid.NewGuid());

        problema!.Codigo.Should().Be("Lending.Savings.LineNotAllowedByProfile");
        problema.Mensaje.Should().Contain("MULTIACTIVA").And.Contain("Sólo puede ofrecer aportes");
        problema.Datos!["allowedClasses"].Should().BeEquivalentTo(Array.Empty<string>());
        ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(DepositClass.Demand, true, "AHORRO_CREDITO", null).Should().BeNull();
    }

    [Fact]
    public void FR_038_el_cambio_de_tasa_espera_la_anticipacion_del_aviso()
    {
        ReglasDeLineaDeCaptacion.ValidarAnticipacionDelAviso(Hoy.AddDays(14), Hoy, 15)!.Codigo.Should().Be("Lending.Savings.RateChangeNoticeTooShort");
        ReglasDeLineaDeCaptacion.ValidarAnticipacionDelAviso(Hoy.AddDays(15), Hoy, 15).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarAnticipacionDelAviso(Hoy, Hoy, 0).Should().BeNull();
    }

    [Fact]
    public void Cambiar_la_tasa_se_distingue_de_cambiar_otra_condicion()
    {
        var base_ = ALaVista();
        base_.CambiaLaTasaRespectoDe(base_ with { MinBalance = 10_000m, Reason = "Otra" }).Should().BeFalse();
        base_.CambiaLaTasaRespectoDe(base_ with { AnnualRate = 0.025m }).Should().BeTrue();
        var conTramos = ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, null, AnnualRate: 0.01m));
        conTramos.CambiaLaTasaRespectoDe(ALaVista(new BorradorDeTramoDeCaptacion(5, 0m, null, AnnualRate: 0.01m))).Should().BeFalse("el orden no es tasa");
        conTramos.CambiaLaTasaRespectoDe(ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, null, AnnualRate: 0.015m))).Should().BeTrue();
    }

    // --------------------------------------------------------------------------------------------------------- aportes --

    [Fact]
    public void Una_version_de_aportes_bien_formada_pasa_y_cada_modo_lleva_lo_suyo()
    {
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes(), Hoy).Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { InstallmentFraction = null }, Hoy)!.Datos!["field"]
            .Should().Be("installmentFraction");
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { InstallmentAmount = 10_000m }, Hoy)!.Datos!["field"]
            .Should().Be("installmentAmount");
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { InstallmentPeriodicity = RepaymentPeriodicity.SinglePayment }, Hoy)!.Datos!["field"]
            .Should().Be("installmentPeriodicity");
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { PermanentSavingsLineId = 4 }, Hoy)!.Datos!["field"].Should().Be("permanentSavings");
        ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(Aportes() with { MinimumMode = ContributionAmountMode.MinimumWageFraction, MinimumAmount = null,
            MinimumFraction = 1.5m }, Hoy).Should().BeNull("el mínimo puede ser más de un salario mínimo");
    }

    [Fact]
    public void US1_3_en_un_fondo_de_empleados_la_cuota_sobre_el_tope_es_InstallmentCapExceeded_con_el_tope_y_su_fuente()
    {
        var b = Aportes() with { InstallmentFraction = 0.12m, PermanentSavingsLineId = 4, PermanentSavingsFraction = 0.5m };

        var problema = ReglasDeLineaDeCaptacion.ValidarTopesDeFondoDeEmpleados(b, 0.10m, "Ley 2496 de 2025", 0.10m, "Ley 2496 de 2025");

        problema!.Codigo.Should().Be("Lending.Contributions.InstallmentCapExceeded");
        problema.Mensaje.Should().Contain("12 %").And.Contain("10 %").And.Contain("Ley 2496 de 2025");
        problema.Datos!["capPercent"].Should().Be(0.10m);
        problema.Datos["source"].Should().Be("Ley 2496 de 2025");
        ReglasDeLineaDeCaptacion.ValidarTopesDeFondoDeEmpleados(b with { InstallmentFraction = 0.10m }, 0.10m, "Ley", 0.10m, "Ley").Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarTopesDeFondoDeEmpleados(b, null, null, null, null).Should().BeNull("sin tope no aplica");
    }

    [Fact]
    public void En_un_fondo_de_empleados_la_parte_de_aportes_no_baja_de_la_minima()
    {
        var b = Aportes() with { InstallmentFraction = 0.05m, PermanentSavingsLineId = 4, PermanentSavingsFraction = 0.95m };

        var problema = ReglasDeLineaDeCaptacion.ValidarTopesDeFondoDeEmpleados(b, 0.10m, "Ley", 0.10m, "Ley 2496 de 2025");

        problema!.Codigo.Should().Be("Lending.Contributions.FractionBelowLegal");
        problema.Datos!["contributionsShareMin"].Should().Be(0.10m);
    }

    [Fact]
    public void El_limite_de_participacion_no_es_menos_estricto_que_el_legal()
    {
        var b = Aportes() with { MaxParticipationFraction = 0.2m };

        ReglasDeLineaDeCaptacion.ValidarLimiteDeParticipacion(b, 0.1m, "Aportes.LimiteParticipacionPersonaNatural", "Ley 79 de 1988")!.Codigo
            .Should().Be("Lending.Parameter.BelowLegalFloor");
        ReglasDeLineaDeCaptacion.ValidarLimiteDeParticipacion(b with { MaxParticipationFraction = 0.05m }, 0.1m, "x", "y").Should().BeNull();
        ReglasDeLineaDeCaptacion.ValidarLimiteDeParticipacion(b, null, null, null).Should().BeNull();
    }

    // ----------------------------------------------------------------------------------------------------------- huella --

    [Fact]
    public void La_version_sella_su_huella_y_el_motivo_no_es_contenido()
    {
        var b = ALaVista(new BorradorDeTramoDeCaptacion(1, 0m, 999_999.99m, AnnualRate: 0.01m), new BorradorDeTramoDeCaptacion(2, 1_000_000m, null, AnnualRate: 0.03m));

        var version = DepositLineVersion.Registrar(3, b, 1, Hoy, new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        var tramos = DepositLineVersion.Tramos(7, b).ToList();

        version.ContentSha256.Should().HaveLength(64).And.Be(DepositLineVersion.Huella(b with { Reason = "Otro motivo" }));
        version.RateTierMode.Should().Be(RateTierMode.WholeBalance, "con tramos y sin modo rige «todo el saldo» (D8)");
        version.WithholdingRateParameterCode.Should().Be(ParametrosDeCartera.RetencionTarifaAhorro);
        version.HuellaDeLoGuardado(tramos).Should().Be(version.ContentSha256);
        DepositLineVersion.Huella(b with { MinBalance = 1m }).Should().NotBe(version.ContentSha256);

        var aportes = ContributionLineVersion.Registrar(3, Aportes(), 1, Hoy, DateTime.UtcNow);
        aportes.HuellaDeLoGuardado().Should().Be(aportes.ContentSha256);
    }

    [Fact]
    public void Registrar_una_version_invalida_lanza()
    {
        var registrar = () => DepositLineVersion.Registrar(3, ALaVista() with { ValidFrom = Hoy.AddDays(-1) }, 1, Hoy, DateTime.UtcNow);

        registrar.Should().Throw<ReglaDeCarteraException>();
    }
}
