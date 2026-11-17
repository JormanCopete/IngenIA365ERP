using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0351 (data-model §1.2–§1.9; FR-019, FR-021, FR-032, D20, T44): el comportamiento de <see cref="CreditLine"/> (estados,
/// cierre con motivo, bloqueo y desbloqueo por usura, código inmutable), la fábrica de <see cref="CreditLineVersion"/> con sus hijas
/// (validación, sólo inserción, <c>ContentSha256</c>) y <see cref="CommercialTermEquivalence"/>.
/// </summary>
public class LineaDeCreditoTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateTime Ahora = new(2026, 12, 1, 10, 0, 0);

    private static BorradorDeVersionDeLinea Version() => new(Hoy, "Tasa aprobada por el consejo", "consumo")
    {
        SubsegmentCode = "SinLibranza",
        AnnualRate = 0.245000m,
        DefaultRateFactor = 1.5m,
        MinAmount = 500_000m,
        MaxAmount = 30_000_000m,
        MinTermMonths = 1,
        MaxTermMonths = 60,
        Periodicities = [RepaymentPeriodicity.Monthly, RepaymentPeriodicity.Biweekly],
        PaymentDay = 5,
        AmortizationSystems = [AmortizationSystem.LevelPayment, AmortizationSystem.ConstantPrincipal],
        DayCount = DayCountConvention.Actual365,
        RepaymentChannels = [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll],
        RateBands =
        [
            new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, MinTermMonths = 1, MaxTermMonths = 24, AnnualRate = 0.260000m },
            new BorradorDeBanda(2) { MinAmount = 5_000_000.01m, MaxAmount = null, AnnualRate = 0.240000m },
        ],
        DocumentRequirements = [new BorradorDeDocumento("cedula", "Copia de cédula", true, 1)],
        Insurances = [new BorradorDeSeguro("vida", "Vida deudores", 77, InsuranceBase.OutstandingBalance, 0.000450m, RepaymentPeriodicity.Monthly) { IsMandatory = true }],
        CollateralRules = [new BorradorDeGarantia(1, 1) { MinAmount = 10_000_001m, AdmittedKinds = [CollateralKind.Mortgage, CollateralKind.CoDebtor], MinimumCoverage = 1.2m }],
    };

    // ------------------------------------------------------------------------------------------------------ CreditLine --

    [Fact]
    public void Una_linea_nace_en_borrador_con_el_codigo_normalizado()
    {
        var linea = CreditLine.Nueva(" conlib ", "Consumo con libranza", "Libre inversión");
        linea.Code.Should().Be("CONLIB");
        linea.Status.Should().Be(CreditLineStatus.Draft);
        linea.AdmiteCreditosNuevos.Should().BeFalse("en borrador no admite créditos");
        typeof(CreditLine).GetProperty(nameof(CreditLine.Code))!.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
            .Should().Contain(typeof(System.Runtime.CompilerServices.IsExternalInit), "el código es inmutable (init)");
    }

    [Fact]
    public void Activar_cerrar_y_reabrir()
    {
        var linea = CreditLine.Nueva("CONLIB", "Consumo", null);
        linea.Activar();
        linea.Status.Should().Be(CreditLineStatus.Active);
        linea.AdmiteCreditosNuevos.Should().BeTrue();

        linea.ValidarReapertura()!.Codigo.Should().Be(CreditLine.CodigoNoCerrada);
        linea.Cerrar(CreditLineClosedReason.Manual, "Se reemplaza por CONLIB2", 9, Ahora);
        linea.Status.Should().Be(CreditLineStatus.Closed);
        linea.ClosedReason.Should().Be(CreditLineClosedReason.Manual);
        linea.CloseNotes.Should().Be("Se reemplaza por CONLIB2");
        linea.ClosedByUserId.Should().Be(9);
        linea.AdmiteCreditosNuevos.Should().BeFalse();
        linea.ValidarCierre()!.Codigo.Should().Be(CreditLine.CodigoYaCerrada);
        linea.ValidarActivacion()!.Codigo.Should().Be(CreditLine.CodigoCerrada);
        var activar = () => linea.Activar();
        activar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(CreditLine.CodigoCerrada);

        linea.Reabrir();
        linea.Status.Should().Be(CreditLineStatus.Active);
        linea.ClosedReason.Should().BeNull();
        linea.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void El_cierre_manual_lleva_motivo_y_el_de_modalidad_retirada_no_lleva_usuario()
    {
        var linea = CreditLine.Nueva("CONLIB", "Consumo", null);
        var sinMotivo = () => linea.Cerrar(CreditLineClosedReason.Manual, " ", 9, Ahora);
        sinMotivo.Should().Throw<ArgumentException>();
        linea.Cerrar(CreditLineClosedReason.ModalityRetired, null, null, Ahora);
        linea.ClosedReason.Should().Be(CreditLineClosedReason.ModalityRetired);
        linea.ClosedByUserId.Should().BeNull();
    }

    [Fact]
    public void El_bloqueo_por_usura_no_es_cierre_y_no_se_marca_dos_veces()
    {
        var linea = CreditLine.Nueva("CONLIB", "Consumo", null);
        linea.Activar();
        var usura = Guid.NewGuid();
        linea.BloquearPorUsura(new DateOnly(2027, 1, 1), usura).Should().BeTrue();
        linea.Status.Should().Be(CreditLineStatus.Active, "bloquear no cierra");
        linea.AdmiteCreditosNuevos.Should().BeFalse();
        linea.UsuryBlockedByValuePublicId.Should().Be(usura);

        linea.BloquearPorUsura(new DateOnly(2027, 2, 1), Guid.NewGuid()).Should().BeFalse("una línea ya marcada no se vuelve a alertar");
        linea.UsuryBlockedSince.Should().Be(new DateOnly(2027, 1, 1));

        linea.DesbloquearPorUsura().Should().BeTrue();
        linea.AdmiteCreditosNuevos.Should().BeTrue();
        linea.DesbloquearPorUsura().Should().BeFalse();
    }

    // ----------------------------------------------------------------------------------------------- CreditLineVersion --

    [Fact]
    public void La_version_completa_se_registra_con_sus_hijas_y_su_huella()
    {
        var b = Version();
        CreditLineVersion.Validar(b, Hoy).Should().BeNull();
        var r = CreditLineVersion.Registrar(3, b, 1, Hoy, Ahora);
        var v = r.Version;
        v.CreditLineId.Should().Be(3);
        v.ModalityCode.Should().Be("CONSUMO");
        v.Periodicities.Should().Be("[\"Biweekly\",\"Monthly\"]", "un conjunto se escribe por nombre y ordenado por número");
        v.LeerPeriodicidades().Should().Equal(RepaymentPeriodicity.Biweekly, RepaymentPeriodicity.Monthly);
        v.ConceptPriority.Should().Be("[\"CollectionFee\",\"Insurance\",\"DefaultInterest\",\"CurrentInterest\",\"Principal\"]");
        v.ContentSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        v.Redondeo().Unidad.Should().Be(RedondeoDeCartera.Centavo);

        var bandas = r.Bandas(40);
        bandas.Should().HaveCount(2).And.OnlyContain(x => x.CreditLineVersionId == 40);
        r.Documentos(40).Single().Code.Should().Be("CEDULA");
        r.Seguros(40).Single().Code.Should().Be("VIDA");
        r.Garantias(40).Single().AdmittedKinds.Should().Be("[\"CoDebtor\",\"Mortgage\"]");
        var sinId = () => r.Bandas(0);
        sinId.Should().Throw<ArgumentOutOfRangeException>();

        // La huella de lo guardado coincide con la del borrador; el motivo no es contenido.
        v.Id = 40; // lo asigna la base al guardar
        v.HuellaDeLoGuardado(bandas, r.Documentos(40), r.Seguros(40), r.Garantias(40)).Should().Be(v.ContentSha256);
        CreditLineVersion.Huella(b with { Reason = "Otro motivo" }).Should().Be(v.ContentSha256);
        CreditLineVersion.Huella(b with { AnnualRate = 0.246000m }).Should().NotBe(v.ContentSha256);
    }

    [Fact]
    public void Pedir_la_fecha_del_primer_pago_se_guarda_y_sale_en_la_huella_solo_si_esta_puesta()
    {
        var b = Version();
        var pide = b with { AsksFirstDueDate = true };
        var r = CreditLineVersion.Registrar(3, pide, 1, Hoy, Ahora);

        r.Version.AsksFirstDueDate.Should().BeTrue();
        r.Version.Id = 40;
        r.Version.HuellaDeLoGuardado(r.Bandas(40), r.Documentos(40), r.Seguros(40), r.Garantias(40)).Should().Be(r.Version.ContentSha256);
        CreditLineVersion.Huella(pide).Should().NotBe(CreditLineVersion.Huella(b));
        CreditLineVersion.Huella(b with { AsksFirstDueDate = false }).Should().Be(CreditLineVersion.Huella(b), "las versiones anteriores conservan su huella");
    }

    [Fact]
    public void Una_version_retroactiva_se_rechaza() =>
        CreditLineVersion.Validar(Version() with { ValidFrom = Hoy.AddDays(-1) }, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoRetroactiva);

    [Fact]
    public void Una_tasa_con_mas_de_seis_decimales_se_rechaza_nunca_se_redondea()
    {
        var p = CreditLineVersion.Validar(Version() with { AnnualRate = 0.2450001m }, Hoy)!;
        p.Codigo.Should().Be("Lending.Rate.TooPrecise");
        p.Datos!["field"].Should().Be("annualRate");
        var registrar = () => CreditLineVersion.Registrar(3, Version() with { AnnualRate = 0.2450001m }, 1, Hoy, Ahora);
        registrar.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void Bandas_del_mismo_segmento_que_se_cruzan_se_rechazan()
    {
        var b = Version() with
        {
            RateBands =
            [
                new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, AnnualRate = 0.26m },
                new BorradorDeBanda(2) { MinAmount = 4_000_000m, MaxAmount = 9_000_000m, AnnualRate = 0.25m },
            ],
        };
        CreditLineVersion.Validar(b, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoBandasCruzadas);

        // De segmentos distintos no se cruzan.
        var distintas = b with
        {
            RateBands =
            [
                new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, AnnualRate = 0.26m, ForEmployees = true },
                new BorradorDeBanda(2) { MinAmount = 4_000_000m, MaxAmount = 9_000_000m, AnnualRate = 0.25m },
            ],
        };
        CreditLineVersion.Validar(distintas, Hoy).Should().BeNull();
    }

    [Fact]
    public void Origen_comercial_con_interes_sin_concepto_tributario_se_rechaza()
    {
        var b = Version() with { AllowsCommercialOrigin = true };
        CreditLineVersion.Validar(b, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoSinConceptoTributario);
        CreditLineVersion.Validar(b with { InterestTaxTreatment = InterestTaxTreatment.NotTaxed, InterestTaxSupportAttachmentPublicId = Guid.NewGuid() }, Hoy)
            .Should().BeNull();
        CreditLineVersion.Validar(b with { AnnualRate = 0m, RateBands = [] }, Hoy).Should().BeNull("sin interés no hay concepto que pedir");
    }

    [Theory]
    [InlineData("minAmount")]
    [InlineData("defaultPeriodicity")]
    [InlineData("conceptPriority")]
    [InlineData("indexCode")]
    [InlineData("roundingUnit")]
    [InlineData("graceInterestTreatment")]
    public void Lo_demas_responde_Validation_Invalid_con_el_campo(string campo)
    {
        var b = campo switch
        {
            "minAmount" => Version() with { MinAmount = 40_000_000m },
            "defaultPeriodicity" => Version() with { DefaultPeriodicity = RepaymentPeriodicity.Weekly },
            "conceptPriority" => Version() with { ConceptPriority = [LoanConcept.Principal, LoanConcept.Principal] },
            "indexCode" => Version() with { RateKind = RateKind.Variable, AnnualRate = null, RateBands = [] },
            "roundingUnit" => Version() with { RoundingUnit = 50m },
            _ => Version() with { AllowedGraceKind = GraceKind.Total, MaxGraceInstallments = 3 },
        };
        var p = CreditLineVersion.Validar(b, Hoy)!;
        p.Codigo.Should().Be(CreditLineVersion.CodigoInvalida);
        p.Datos!["field"].Should().Be(campo);
    }

    [Fact]
    public void La_banda_casa_por_monto_plazo_y_segmento()
    {
        var banda = CreditLineRateBand.Nueva(1, new BorradorDeBanda(1) { MinAmount = 0m, MaxAmount = 5_000_000m, MinTermMonths = 1, MaxTermMonths = 24, ForEmployees = true, AnnualRate = 0.2m });
        banda.Casa(3_000_000m, 12, DebtorKind.Associate, esEmpleado: true, antiguedadMeses: null).Should().BeTrue();
        banda.Casa(3_000_000m, 12, DebtorKind.Associate, esEmpleado: false, antiguedadMeses: null).Should().BeFalse();
        banda.Casa(6_000_000m, 12, DebtorKind.Associate, esEmpleado: true, antiguedadMeses: null).Should().BeFalse();
    }

    [Fact]
    public void El_seguro_de_la_linea_llega_al_plan_con_lo_que_computa()
    {
        var seguro = CreditLineInsurance.Nuevo(1, new BorradorDeSeguro("VIDA", "Vida", 7, InsuranceBase.InitialAmount, 0.0004m, RepaymentPeriodicity.Monthly) { SelfInsured = true });
        var delPlan = seguro.ParaElPlan();
        delPlan.Computa.Should().BeTrue();
        delPlan.Base.Should().Be(InsuranceBase.InitialAmount);
        delPlan.Tarifa.Should().Be(0.0004m);
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración es una clase de cargo de la línea. Computa siempre para la usura (la
    /// retiene entera la cooperativa), llega al plan con su clase y la cuota la lleva aparte del seguro, dentro del total; la huella de una versión
    /// con sólo seguros no cambia, y la administración no puede ser el seguro de vida deudores.
    /// </summary>
    [Fact]
    public void La_administracion_es_una_clase_de_cargo_que_computa_siempre_y_va_en_su_cubeta_del_plan()
    {
        var vida = new BorradorDeSeguro("VIDA", "Vida", 7, InsuranceBase.OutstandingBalance, 0.0006m, RepaymentPeriodicity.Monthly);
        var admon = new BorradorDeSeguro("ADMON", "Administración", 7, InsuranceBase.InitialAmount, 0.0015m, RepaymentPeriodicity.Monthly)
        {
            Kind = InsuranceKind.Administration,
        };
        CreditLineInsurance.Nuevo(1, vida).Kind.Should().Be(InsuranceKind.Insurance, "de fábrica, seguro");
        CreditLineInsurance.Nuevo(1, vida).Computa().Should().BeFalse();
        var administracion = CreditLineInsurance.Nuevo(1, admon);
        administracion.Kind.Should().Be(InsuranceKind.Administration);
        administracion.Computa().Should().BeTrue("la administración es de la cooperativa: computa para la usura sin retención ni autoaseguro");
        administracion.ParaElPlan().Clase.Should().Be(InsuranceKind.Administration);

        var b = Version();
        CreditLineVersion.Huella(b with { Insurances = [b.Insurances[0] with { Kind = InsuranceKind.Insurance }] }).Should().Be(CreditLineVersion.Huella(b),
            "la clase entra a la huella sólo si es administración");
        var conAdministracion = b with { Insurances = [.. b.Insurances, admon] };
        CreditLineVersion.Validar(conAdministracion, Hoy).Should().BeNull();
        CreditLineVersion.Huella(conAdministracion).Should().NotBe(CreditLineVersion.Huella(b with { Insurances = [.. b.Insurances, admon with { Kind = InsuranceKind.Insurance }] }));
        CreditLineVersion.Validar(b with { Insurances = [admon with { IsDebtorLifeInsurance = true }] }, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoInvalida);

        var plan = MotorDeCartera.GenerarPlan(new EntradaDePlan(1_200_000m, new DateOnly(2027, 1, 15), 6, RepaymentPeriodicity.Monthly,
            AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo)
        {
            Seguros = [CreditLineInsurance.Nuevo(1, vida).ParaElPlan(), administracion.ParaElPlan()],
        });
        var primera = plan.Cuotas[0];
        primera.Seguro.Should().Be(720m, "0,06 % del saldo inicial: sólo el seguro");
        primera.Administracion.Should().Be(1800m, "0,15 % del monto inicial, aparte del seguro");
        primera.Total.Should().Be(primera.Capital + primera.Interes + primera.Seguro + primera.Administracion);
        primera.SeguroComputable.Should().Be(1800m, "computa la administración; el seguro de vida no");
        plan.TotalAdministracion.Should().Be(6 * 1800m);
    }

    // --------------------------------------------------------------------------------------- CommercialTermEquivalence --

    [Fact]
    public void Una_equivalencia_de_periodicidad_y_una_de_unidad_de_plazo()
    {
        var mensual = CommercialTermEquivalence.Nueva(CommercialTermKind.Periodicity, " mensual ", RepaymentPeriodicity.Monthly, null, Hoy, 1, Ahora);
        mensual.CommercialCode.Should().Be("MENSUAL");
        var meses = CommercialTermEquivalence.Nueva(CommercialTermKind.TermUnit, "MESES", null, 30, Hoy, 1, Ahora);
        meses.DaysPerUnit.Should().Be(30);
    }

    [Fact]
    public void Una_periodicidad_desconocida_y_un_lote_con_duplicados_se_rechazan()
    {
        CommercialTermEquivalence.Validar(CommercialTermKind.Periodicity, "LUNAR", null, null)!.Codigo
            .Should().Be(CommercialTermEquivalence.CodigoPeriodicidadDesconocida);
        CommercialTermEquivalence.Validar(CommercialTermKind.Periodicity, "LUNAR", (RepaymentPeriodicity)45, null)!.Codigo
            .Should().Be(CommercialTermEquivalence.CodigoPeriodicidadDesconocida);
        CommercialTermEquivalence.Validar(CommercialTermKind.TermUnit, "MESES", null, 0)!.Codigo.Should().Be("Validation.Invalid");
        CommercialTermEquivalence.ValidarLote([(CommercialTermKind.Periodicity, "MENSUAL"), (CommercialTermKind.Periodicity, " mensual")])!
            .Codigo.Should().Be(CommercialTermEquivalence.CodigoDuplicada);
        CommercialTermEquivalence.ValidarLote([(CommercialTermKind.Periodicity, "MENSUAL"), (CommercialTermKind.TermUnit, "MENSUAL")])
            .Should().BeNull("la misma palabra puede ser periodicidad y unidad de plazo");
    }
}
