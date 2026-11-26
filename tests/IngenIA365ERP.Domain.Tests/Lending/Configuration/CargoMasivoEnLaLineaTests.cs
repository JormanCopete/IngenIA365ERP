using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;

namespace IngenIA365ERP.Domain.Tests.Lending.Configuration;

/// <summary>Feature 026 (2026-11-25, HU1): la marca «Admite cargo masivo» en la versión de la línea, sus incompatibilidades y la huella.</summary>
public class CargoMasivoEnLaLineaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateTime Ahora = new(2026, 12, 1, 10, 0, 0);

    private static BorradorDeVersionDeLinea Fdosol() => new(Hoy, "Fondo de solidaridad", "consumo")
    {
        AnnualRate = 0m,
        DefaultRateFactor = 1m,
        MinAmount = 1_000m,
        MaxAmount = 100_000m,
        MinTermMonths = 1,
        MaxTermMonths = 12,
        MaxInstallments = 12,
        Periodicities = [RepaymentPeriodicity.Monthly],
        AmortizationSystems = [AmortizationSystem.LevelPayment],
        RepaymentChannels = [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll],
        AllowsBulkCharge = true,
        BulkChargeDefaultAmount = 13_557m,
        BulkChargeDefaultInstallments = 1,
    };

    [Fact]
    public void Una_linea_sin_requisitos_de_solicitud_admite_cargo_masivo()
    {
        CreditLineVersion.Validar(Fdosol(), Hoy).Should().BeNull();
        var r = CreditLineVersion.Registrar(5, Fdosol(), 1, Hoy, Ahora);
        r.Version.AllowsBulkCharge.Should().BeTrue();
        r.Version.BulkChargeDefaultAmount.Should().Be(13_557m);
        r.Version.BulkChargeReportsToBureaus.Should().BeFalse("de fábrica no se reporta a centrales (D4)");
        r.Version.BulkChargeEntersCollection.Should().BeFalse("de fábrica no entra a cobranza (D4)");
    }

    [Fact]
    public void No_puede_exigir_codeudores_documentos_vinculo_ni_centrales()
    {
        CreditLineVersion.Validar(Fdosol() with { CollateralRules = [new BorradorDeGarantia(1, 1)] }, Hoy)!.Codigo
            .Should().Be(CreditLineVersion.CodigoCargoMasivoIncompatible);
        CreditLineVersion.Validar(Fdosol() with { DocumentRequirements = [new BorradorDeDocumento("cedula", "Cédula", true, 1)] }, Hoy)!.Codigo
            .Should().Be(CreditLineVersion.CodigoCargoMasivoIncompatible);
        CreditLineVersion.Validar(Fdosol() with { RequiresEmployment = true }, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoCargoMasivoIncompatible);
        CreditLineVersion.Validar(Fdosol() with { RequiresBureauQuery = true }, Hoy)!.Codigo.Should().Be(CreditLineVersion.CodigoCargoMasivoIncompatible);
    }

    [Fact]
    public void El_valor_y_las_cuotas_por_defecto_caben_en_la_linea()
    {
        CreditLineVersion.Validar(Fdosol() with { BulkChargeDefaultAmount = 500m }, Hoy).Should().NotBeNull();
        CreditLineVersion.Validar(Fdosol() with { BulkChargeDefaultInstallments = 13 }, Hoy).Should().NotBeNull();
    }

    [Fact]
    public void Sin_la_marca_no_van_los_valores_del_cargo() =>
        CreditLineVersion.Validar(Fdosol() with { AllowsBulkCharge = false }, Hoy).Should().NotBeNull();

    [Fact]
    public void La_marca_entra_a_la_huella_solo_si_esta_puesta()
    {
        var sin = Fdosol() with { AllowsBulkCharge = false, BulkChargeDefaultAmount = null, BulkChargeDefaultInstallments = null };
        CreditLineVersion.Huella(sin).Should().NotBe(CreditLineVersion.Huella(Fdosol()));
        CreditLineVersion.Huella(Fdosol() with { BulkChargeDefaultAmount = 14_000m }).Should().NotBe(CreditLineVersion.Huella(Fdosol()));
    }
}
