using FluentAssertions;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0333 (US1; api.md §3.1; data-model §1.2–§1.7; T28, T44, D18, D20; FR-018–FR-021): el alta de una línea con su primera
/// versión por <see cref="CreateCreditLineCommandHandler"/> y las reglas únicas de <see cref="ReglasDeLineaDeCredito"/>. Código repetido,
/// modalidad fuera del catálogo vigente, vivienda, no asociados sin perfil que lo permita, origen comercial sin concepto tributario,
/// tasa demasiado precisa, versión retroactiva, bandas cruzadas y formas mal hechas no guardan nada; la usura se controla con la versión y
/// <b>cada banda</b> sobre el plan tipo, con los seguros que computan; sin usura del día, <c>NationalValues.Missing</c> y alerta. Si todo
/// pasa, la línea nace activa con su versión 1, sus hijas y su huella.
/// </summary>
public sealed class CreateCreditLineCommandHandlerTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    private async Task<string> Codigo(CreditLineVersionInput version, string codigo = "CONLIB")
    {
        var r = await _l.Crear().Handle(LineasDePrueba.Alta(codigo, version), default);
        r.IsFailure.Should().BeTrue("la línea no debía crearse");
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0, "si una regla falla no se guarda nada");
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(0);
        return r.Error.Code;
    }

    [Fact]
    public async Task Una_linea_que_pasa_nace_activa_con_su_version_sus_hijas_y_su_huella()
    {
        await _l.NormativaAsync();
        var version = LineasDePrueba.Version() with
        {
            Insurances = [_l.Seguro()],
            CollateralRules = [new CollateralRuleInput(AmountFrom: 10_000_001m, CoDebtors: 1, AdmittedKinds: [CollateralKind.CoDebtor])],
            Rates = LineasDePrueba.Version().Rates with { Bands = [new RateBandInput(AmountFrom: 0m, AmountTo: 5_000_000m, EffectiveAnnual: 0.22m)] },
        };

        var r = await _l.Crear().Handle(LineasDePrueba.Alta(" conlib ", version), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Code.Should().Be("CONLIB");
        r.Value.Status.Should().Be(CreditLineStatus.Active);
        r.Value.AdmitsNewCredits.Should().BeTrue();
        var actual = r.Value.CurrentVersion!;
        actual.VersionNumber.Should().Be(1);
        actual.ModalityCode.Should().Be("CONSUMO");
        actual.EffectiveAnnual.Should().Be(0.20m);
        actual.Insurances.Should().ContainSingle().Which.InsurerPersonPublicId.Should().Be(_l.Aseguradora.PublicId);
        actual.RateBands.Should().ContainSingle().Which.EffectiveAnnual.Should().Be(0.22m);
        actual.Documents.Should().ContainSingle().Which.Code.Should().Be("CEDULA");

        var guardada = await _l.Db.CreditLineVersions.SingleAsync();
        var leida = (await new VersionVigenteDeLinea(_l.Db).LeerVersionAsync(guardada.Id))!;
        guardada.ContentSha256.Should().HaveLength(64).And.Be(CreditLineVersion.Huella(leida.Borrador()), "la huella cubre la versión con sus hijas");
        (await _l.Db.CreditLineInsurances.SingleAsync()).InsurerPersonId.Should().Be(_l.Aseguradora.Id);
        (await _l.Db.CreditLineCollateralRules.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Un_codigo_vivo_repetido_es_Catalogo_CodigoDuplicado()
    {
        await _l.NormativaAsync();
        await _l.LineaAsync("CONLIB");

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("conlib", LineasDePrueba.Version()), default);

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Línea CONLIB");
    }

    [Fact]
    public async Task Una_combinacion_que_no_esta_en_el_catalogo_vigente_es_ModalityUnknown()
    {
        await _l.NormativaAsync();

        (await Codigo(LineasDePrueba.Version(subsegmento: "Inexistente"))).Should().Be("Lending.CreditLine.ModalityUnknown");
        (await Codigo(LineasDePrueba.Version(modalidad: "FANTASIA", subsegmento: null))).Should().Be("Lending.CreditLine.ModalityUnknown");
    }

    [Fact]
    public async Task Vivienda_es_HousingNotAvailable_antes_de_K6b_y_desde_K6b_pasa_la_Ley_546()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();

        // Antes de K6b (la guarda de entrega, explícita) la vivienda no se crea, como en K1 (D18).
        var antes = await new CreateCreditLineCommandHandler(_l.Db, _l.Reglas(Domain.Lending.EntregaDeCartera.K6a), _l.Alta(), _l.G.Reloj)
            .Handle(LineasDePrueba.Alta("VIV", LineasDePrueba.Vivienda()), default);
        antes.Error.Code.Should().Be("Lending.CreditLine.HousingNotAvailable");
        LineasDePrueba.Datos(antes.Error).GetProperty("availableFrom").GetString().Should().Be("K6b");

        // Con K6b vigente (T1055) se crea si cumple la Ley 546.
        var r = await _l.Crear().Handle(LineasDePrueba.Alta("VIV", LineasDePrueba.Vivienda()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    [Fact]
    public async Task No_asociados_solo_si_el_perfil_regulatorio_lo_permite()
    {
        await _l.NormativaAsync();
        var conNoAsociados = LineasDePrueba.Version() with { AllowsNonAssociates = true };

        (await Codigo(conNoAsociados)).Should().Be("Lending.CreditLine.NonAssociatesNotAllowed", "una cooperativa con actividad financiera presta sólo a asociados");

        _l.G.ConActividadFinanciera(false);
        var r = await _l.Crear().Handle(LineasDePrueba.Alta("CONLIB", conNoAsociados), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
    }

    [Fact]
    public async Task Origen_comercial_con_tasa_sin_concepto_tributario_es_InterestTaxConceptMissing()
    {
        await _l.NormativaAsync();
        var comercial = LineasDePrueba.Version() with { AllowsCommercialOrigin = true };

        (await Codigo(comercial)).Should().Be("Lending.CreditLine.InterestTaxConceptMissing");
        (await Codigo(comercial with { InterestTaxConcept = new InterestTaxConceptInput(false, null) }))
            .Should().Be("Lending.CreditLine.InterestTaxConceptMissing", "el concepto va con el soporte de la contadora");

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("ROTATIVO",
            comercial with { InterestTaxConcept = new InterestTaxConceptInput(false, Guid.NewGuid()) }), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.CurrentVersion!.InterestTaxTreatment.Should().Be(InterestTaxTreatment.NotTaxed);
    }

    [Fact]
    public async Task Una_tasa_con_mas_de_seis_decimales_es_Lending_Rate_TooPrecise()
    {
        await _l.NormativaAsync();

        (await Codigo(LineasDePrueba.Version(0.2000001m))).Should().Be("Lending.Rate.TooPrecise");
    }

    [Fact]
    public async Task Una_version_que_empieza_antes_de_hoy_es_RetroactiveVersion()
    {
        await _l.NormativaAsync();

        (await Codigo(LineasDePrueba.Version(desde: LineasDePrueba.Hoy.AddDays(-1)))).Should().Be("Lending.CreditLine.RetroactiveVersion");
    }

    [Fact]
    public async Task Bandas_cruzadas_son_RateBandsOverlap()
    {
        await _l.NormativaAsync();
        var version = LineasDePrueba.Version() with
        {
            Rates = LineasDePrueba.Version().Rates with
            {
                Bands = [new RateBandInput(0m, 5_000_000m, EffectiveAnnual: 0.21m), new RateBandInput(4_000_000m, 9_000_000m, EffectiveAnnual: 0.19m)],
            },
        };

        (await Codigo(version)).Should().Be("Lending.CreditLine.RateBandsOverlap");
    }

    [Fact]
    public async Task Minimos_sobre_maximos_defectos_fuera_de_su_lista_o_un_concepto_repetido_son_Validation_Invalid_con_el_campo()
    {
        await _l.NormativaAsync();

        var montos = await _l.Crear().Handle(LineasDePrueba.Alta("A", LineasDePrueba.Version() with { Amounts = new(9_000_000m, 1_000_000m) }), default);
        var defecto = await _l.Crear().Handle(LineasDePrueba.Alta("B", LineasDePrueba.Version() with { DefaultPeriodicity = RepaymentPeriodicity.Biweekly }), default);
        var prelacion = await _l.Crear().Handle(LineasDePrueba.Alta("C",
            LineasDePrueba.Version() with { ConceptOrder = [LoanConcept.Principal, LoanConcept.Principal, LoanConcept.CurrentInterest] }), default);

        montos.Error.Code.Should().Be("Validation.Invalid");
        LineasDePrueba.Datos(montos.Error).GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("minAmount");
        LineasDePrueba.Datos(defecto.Error).GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("defaultPeriodicity");
        LineasDePrueba.Datos(prelacion.Error).GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("conceptPriority");
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_aseguradora_inexistente_es_Core_Person_NotFound()
    {
        await _l.NormativaAsync();
        var version = LineasDePrueba.Version() with { Insurances = [_l.Seguro() with { InsurerPersonPublicId = Guid.NewGuid() }] };

        (await Codigo(version)).Should().Be("Core.Person.NotFound");
    }

    // ------------------------------------------------------------------------------------------------------- usura --

    [Fact]
    public async Task Una_tasa_sobre_la_usura_es_Usury_Exceeded_con_la_usura_la_modalidad_la_fecha_y_el_campo()
    {
        await _l.NormativaAsync(0.25m);

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("CONLIB", LineasDePrueba.Version(0.26m)), default);

        r.Error.Code.Should().Be("Lending.Usury.Exceeded");
        r.Error.Message.Should().Contain("supera la usura").And.Contain("CONSUMO").And.NotContain("Lending.");
        var datos = LineasDePrueba.Datos(r.Error);
        datos.GetProperty("usuryRate").GetDecimal().Should().Be(0.25m);
        datos.GetProperty("effectiveRate").GetDecimal().Should().BeGreaterThan(0.25m);
        datos.GetProperty("modalityKey").GetString().Should().Be(LineasDePrueba.ClaveConsumo);
        datos.GetProperty("date").GetString().Should().Be("2026-12-01");
        datos.GetProperty("nationalValuePublicId").GetGuid().Should().NotBeEmpty();
        datos.GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("annualRate");
        (await _l.Db.CreditLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cada_banda_pasa_la_usura_por_separado()
    {
        await _l.NormativaAsync(0.25m);
        var version = LineasDePrueba.Version(0.20m) with
        {
            Rates = LineasDePrueba.Version().Rates with
            {
                Bands = [new RateBandInput(0m, 5_000_000m, EffectiveAnnual: 0.22m), new RateBandInput(5_000_000.01m, null, EffectiveAnnual: 0.27m)],
            },
        };

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("CONLIB", version), default);

        r.Error.Code.Should().Be("Lending.Usury.Exceeded");
        LineasDePrueba.Datos(r.Error).GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("rateBands[2].annualRate");
    }

    [Fact]
    public async Task El_seguro_que_retiene_parte_de_la_prima_computa_para_la_usura_y_el_que_no_retiene_no()
    {
        await _l.NormativaAsync(0.25m);

        var sinRetener = await _l.Crear().Handle(LineasDePrueba.Alta("SINRET", LineasDePrueba.Version(0.22m) with { Insurances = [_l.Seguro()] }), default);
        var reteniendo = await _l.Crear().Handle(LineasDePrueba.Alta("CONRET",
            LineasDePrueba.Version(0.22m) with { Insurances = [_l.Seguro(retenida: 0.3m)] }), default);

        sinRetener.IsSuccess.Should().BeTrue(sinRetener.IsFailure ? sinRetener.Error.Message : string.Empty);
        reteniendo.Error.Code.Should().Be("Lending.Usury.Exceeded", "la prima retenida es un cargo que llega al deudor");
    }

    [Fact]
    public async Task La_mora_fija_sobre_la_usura_del_dia_es_Usury_Exceeded_en_defaultRate()
    {
        await _l.NormativaAsync(0.25m);
        var version = LineasDePrueba.Version() with
        {
            Rates = LineasDePrueba.Version().Rates with { Default = new DefaultRateInput(DefaultRateKind.Fixed, 0.30m) },
        };

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("CONLIB", version), default);

        r.Error.Code.Should().Be("Lending.Usury.Exceeded");
        LineasDePrueba.Datos(r.Error).GetProperty("errors")[0].GetProperty("Field").GetString().Should().Be("defaultRate");
    }

    [Fact]
    public async Task La_tasa_variable_con_su_margen_piso_y_techo_pasa_contra_la_usura()
    {
        await _l.NormativaAsync(0.25m);
        await _l.IbrAsync(0.09m, LineasDePrueba.Hoy);
        var variable = LineasDePrueba.Version() with
        {
            Rates = new CreditLineRatesInput
            {
                Current = new CurrentRateInput(RateKind.Variable),
                Variable = new VariableRateInput("IBR", "3M", 0.08m, 0.10m, 0.24m, RepaymentPeriodicity.Quarterly),
            },
        };

        var r = await _l.Crear().Handle(LineasDePrueba.Alta("VARIBR", variable), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.CurrentVersion!.IndexCode.Should().Be("IBR");
        r.Value.CurrentVersion.Margin.Should().Be(0.08m);
    }

    [Fact]
    public async Task Sin_usura_del_dia_es_NationalValues_Missing_y_se_alerta()
    {
        await _l.NormativaAsync(conUsura: false);

        var codigo = await Codigo(LineasDePrueba.Version());

        codigo.Should().Be("NationalValues.Missing");
        _l.Levantadas.Should().Contain(a => a.TypeCode == AlertasDeValoresNacionales.ValorNacionalFaltante
            && a.DedupKey == "Cartera.ValorNacionalFaltante:USURA:CONSUMO_Y_ORDINARIO:2026-12-01");
    }

    [Fact]
    public async Task Una_version_futura_se_controla_a_su_fecha_y_a_hoy()
    {
        await _l.NormativaAsync(0.25m);
        await _l.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.21m, new DateOnly(2027, 1, 1));

        var futura = await _l.Crear().Handle(LineasDePrueba.Alta("CONLIB", LineasDePrueba.Version(0.22m, new DateOnly(2027, 1, 1))), default);

        futura.Error.Code.Should().Be("Lending.Usury.Exceeded", "a su fecha la usura ya es 21 %");
        LineasDePrueba.Datos(futura.Error).GetProperty("date").GetString().Should().Be("2027-01-01");
    }

    [Fact]
    public async Task Las_reglas_las_comparten_el_alta_y_la_verificacion_pura()
    {
        await _l.NormativaAsync(0.25m);
        var borrador = LineasDePrueba.Version(0.26m).Borrador(new Dictionary<Guid, int>());

        var r = await _l.Reglas().VerificarAsync(borrador, LineasDePrueba.Hoy, default);

        r.Error.Code.Should().Be("Lending.Usury.Exceeded");
        borrador.Should().BeOfType<BorradorDeVersionDeLinea>();
    }
}
