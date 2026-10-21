using FluentAssertions;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Housing;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Housing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Housing;

/// <summary>
/// Feature 013, K6b, T1044 (FR-024, D18): las reglas de la Ley 546 de 1999 en la línea, la solicitud y el desembolso de vivienda, sobre el escenario de
/// <see cref="LineasDePrueba"/> con los lectores reales de valores nacionales y los límites y el tope de vivienda sembrados como copias replicadas
/// (valores DE PRUEBA, no de la norma). Antes de K6b la vivienda sigue como en K1 (<c>HousingNotAvailable</c>); con K6b: tasa variable, tasa sobre el
/// tope, plazo fuera de los límites, financiación sobre el porcentaje (general y VIS), cuota sobre la relación con el ingreso e hipoteca que no es de
/// primer grado se rechazan nombrando el límite y su fuente; la simulación sella la versión de reglas de vivienda y, en UVR, valora el plan a la UVR
/// del desembolso (sin ella, <c>NationalValues.Missing</c>). La mora sobre intereses y el cálculo en UVR los fija Domain (ViviendaYUvrTests y los
/// casos dorados 61–65). (nuevo)
/// </summary>
public sealed class ReglasDeViviendaTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CreditLineDto>> CrearAsync(CreditLineVersionInput version, string codigo = "VIV") =>
        await _l.Crear().Handle(LineasDePrueba.Alta(codigo, version), default);

    // ------------------------------------------------------------------------------------------------- sin K6b --

    [Fact]
    public async Task Sin_K6b_la_vivienda_no_se_crea_ni_se_migra_como_en_K1()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();

        var r = await new CreateCreditLineCommandHandler(_l.Db, _l.Reglas(EntregaDeCartera.K6a), _l.Alta(), _l.G.Reloj)
            .Handle(LineasDePrueba.Alta("VIV", LineasDePrueba.Vivienda()), default);

        r.Error.Code.Should().Be(ErroresDeLineasDeCredito.CodigoViviendaNoDisponible);
        LectorDeVivienda.Disponible(EntregaDeCartera.K6a).Should().BeFalse("la migración y el cuadre la dejan fuera antes de K6b");
        LectorDeVivienda.Disponible(EntregaDeCartera.K6b).Should().BeTrue();
        LectorDeVivienda.Disponible().Should().BeTrue("K6b es la entrega vigente (T1058)");
        ErroresDeMigracion.CodigoViviendaNoDisponible.Should().Be("Lending.Migration.HousingNotAvailable");
    }

    // ------------------------------------------------------------------------------------------------- la línea --

    [Fact]
    public async Task Con_K6b_una_linea_de_vivienda_de_tasa_fija_bajo_el_tope_y_en_plazo_se_crea_y_en_UVR_tambien()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync(tope: 0.13m);

        (await CrearAsync(LineasDePrueba.Vivienda())).IsSuccess.Should().BeTrue();
        var uvr = await CrearAsync(LineasDePrueba.Vivienda(unidad: DenominationUnit.Uvr), "VIVUVR");
        uvr.IsSuccess.Should().BeTrue(uvr.IsFailure ? $"{uvr.Error.Code}: {uvr.Error.Message}" : string.Empty);
        uvr.Value.CurrentVersion!.DenominationUnit.Should().Be(DenominationUnit.Uvr);
        (await _l.Db.CreditLineVersions.SingleAsync(v => v.PublicId == uvr.Value.CurrentVersion.PublicId)).DenominationUnit.Should().Be(DenominationUnit.Uvr);
    }

    [Fact]
    public async Task Con_K6b_una_linea_de_vivienda_de_tasa_variable_se_rechaza()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();
        var variable = LineasDePrueba.Vivienda() with
        {
            Rates = new CreditLineRatesInput
            {
                Current = new CurrentRateInput(RateKind.Variable, null),
                Variable = new VariableRateInput("IBR", "3M", 0.05m, null, null, RepaymentPeriodicity.Quarterly),
                Default = new DefaultRateInput(DefaultRateKind.RelativeToCurrent, null, 1.5m),
            },
        };

        (await CrearAsync(variable)).Error.Code.Should().Be(ReglasDeVivienda.CodigoTasaVariable);
    }

    [Fact]
    public async Task Con_K6b_una_tasa_sobre_el_tope_de_vivienda_se_rechaza_con_el_tope_y_su_fuente()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync(tope: 0.13m);

        var r = await CrearAsync(LineasDePrueba.Vivienda(tasa: 0.135m));

        r.Error.Code.Should().Be(ReglasDeVivienda.CodigoTopeExcedido);
        var datos = LineasDePrueba.Datos(r.Error);
        datos.GetProperty("cap").GetDecimal().Should().Be(0.13m);
        datos.GetProperty("rate").GetDecimal().Should().Be(0.135m);
        datos.GetProperty("source").GetString().Should().Be("Norma", "la fuente del valor nacional replicado");
        r.Error.Message.Should().Contain("tope de vivienda");
    }

    [Theory]
    [InlineData(36, 240, CatalogoDeLimitesLegales.ViviendaPlazoMinimoMeses)]
    [InlineData(60, 420, CatalogoDeLimitesLegales.ViviendaPlazoMaximoMeses)]
    public async Task Con_K6b_un_plazo_fuera_de_los_limites_de_vivienda_se_rechaza_nombrando_el_limite(int minimo, int maximo, string limite)
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();

        var r = await CrearAsync(LineasDePrueba.Vivienda(plazoMinimo: minimo, plazoMaximo: maximo));

        r.Error.Code.Should().Be(ReglasDeVivienda.CodigoPlazoFueraDeLimites);
        LineasDePrueba.Datos(r.Error).GetProperty("limitCode").GetString().Should().Be(limite);
        r.Error.Message.Should().Contain(limite);
    }

    [Fact]
    public async Task Sin_los_limites_de_vivienda_replicados_la_linea_responde_NationalValues_Missing()
    {
        await _l.NormativaAsync();

        (await CrearAsync(LineasDePrueba.Vivienda())).Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task La_UVR_solo_va_donde_el_catalogo_la_admite_y_en_un_solo_tramo()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();

        (await CrearAsync(LineasDePrueba.Version() with { DenominationUnit = DenominationUnit.Uvr }, "CONUVR")).Error.Code
            .Should().Be(ReglasDeVivienda.CodigoUvrNoAdmitida, "consumo no admite UVR en el catálogo");
        var conTramos = LineasDePrueba.Vivienda(unidad: DenominationUnit.Uvr) with { Amortization = new AmortizationInput { AllowsTranches = true } };
        var r = await CrearAsync(conTramos);
        r.Error.Code.Should().Be(ErroresDeLineasDeCredito.CodigoInvalido);
        r.Error.Message.Should().Contain("un solo tramo");
    }

    // ------------------------------------------------------------------------------------------- la simulación --

    [Fact]
    public async Task La_simulacion_de_vivienda_sella_la_version_de_reglas_de_vivienda_y_en_UVR_valora_el_plan_a_la_UVR_del_desembolso()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();
        var pesos = await _l.LineaAsync("VIV", LineasDePrueba.Vivienda());
        var uvr = await _l.LineaAsync("VIVUVR", LineasDePrueba.Vivienda(unidad: DenominationUnit.Uvr));
        var simulador = new SimuladorDeCredito(_l.Db, _l.Reglas());

        var enPesos = await simulador.SimularAsync(await LineaAsync(pesos.PublicId), new ScheduleSimulationInput(pesos.PublicId, 100_000_000m, 120),
            PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default);
        enPesos.IsSuccess.Should().BeTrue(enPesos.IsFailure ? enPesos.Error.Message : string.Empty);
        enPesos.Value.Plan.RulesVersion.Should().Be(VersionesDeReglas.Vivienda, "el crédito de vivienda sella las reglas de la Ley 546");
        enPesos.Value.PlanEnUvr.Should().BeNull();

        // En UVR sin la UVR del día: NationalValues.Missing; con ella, el plan en UVR y su valoración en pesos al centavo del monto.
        var sinUvr = await simulador.SimularAsync(await LineaAsync(uvr.PublicId), new ScheduleSimulationInput(uvr.PublicId, 100_000_000m, 120),
            PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default);
        sinUvr.Error.Code.Should().Be("NationalValues.Missing");

        await _l.UvrAsync((LineasDePrueba.Hoy, 407.7518m));
        var enUvr = await new SimuladorDeCredito(_l.Db, _l.Reglas()).SimularAsync(await LineaAsync(uvr.PublicId),
            new ScheduleSimulationInput(uvr.PublicId, 100_000_000m, 120), PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default);
        enUvr.IsSuccess.Should().BeTrue(enUvr.IsFailure ? enUvr.Error.Message : string.Empty);
        var s = enUvr.Value;
        s.ValorUvr.Should().Be(407.7518m);
        s.PlanEnUvr!.Entrada.Monto.Should().Be(ConversionUvr.AUvr(100_000_000m, 407.7518m, RoundingMode.AwayFromZero));
        s.PlanEnUvr.TotalCapital.Should().Be(s.PlanEnUvr.Entrada.Monto);
        s.Plan.TotalCapital.Should().Be(100_000_000m, "el capital en pesos se reparte por acumulado contra el monto (SC-017)");
        s.Plan.RulesVersion.Should().Be(VersionesDeReglas.Vivienda);
        s.Plan.Cuotas[0].Interes.Should().Be(ConversionUvr.APesos(s.PlanEnUvr.Cuotas[0].Interes, 407.7518m, RoundingMode.AwayFromZero));
    }

    // ---------------------------------------------------------------------------------------------- la solicitud --

    [Fact]
    public async Task La_solicitud_de_vivienda_cumple_el_porcentaje_financiable_general_y_VIS_y_la_relacion_cuota_ingreso()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();
        var linea = await _l.LineaAsync("VIV", LineasDePrueba.Vivienda());
        var simulacion = (await new SimuladorDeCredito(_l.Db, _l.Reglas()).SimularAsync(await LineaAsync(linea.PublicId),
            new ScheduleSimulationInput(linea.PublicId, 100_000_000m, 120), PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default)).Value;
        var lector = new LectorDeVivienda(_l.Valores());

        async Task<IngenIA365ERP.Application.Common.Models.Result> Evaluar(decimal inmueble, bool vis, decimal ingreso, byte rango = 1)
        {
            var s = await SolicitudAsync(linea.PublicId, vis, inmueble, ingreso, rango);
            return await ValidacionDeVivienda.SolicitudAsync(_l.Db, lector, s, simulacion, 100_000_000m, LineasDePrueba.Hoy, default);
        }

        (await Evaluar(150_000_000m, vis: false, ingreso: 6_000_000m)).IsSuccess.Should().BeTrue("financia el 66,7 % y la cuota es menos del 30 % del ingreso");

        var general = await Evaluar(120_000_000m, vis: false, ingreso: 6_000_000m);
        general.Error.Code.Should().Be(ReglasDeVivienda.CodigoFinanciacionExcedida);
        LineasDePrueba.Datos(general.Error).GetProperty("limitCode").GetString().Should().Be(CatalogoDeLimitesLegales.ViviendaFinanciable);

        var vis = await Evaluar(120_000_000m, vis: true, ingreso: 6_000_000m);
        vis.Error.Code.Should().Be(ReglasDeVivienda.CodigoFinanciacionExcedida);
        LineasDePrueba.Datos(vis.Error).GetProperty("limitCode").GetString().Should().Be(CatalogoDeLimitesLegales.ViviendaFinanciableVis);
        (await Evaluar(125_000_000m, vis: true, ingreso: 6_000_000m)).IsSuccess.Should().BeTrue("la VIS financia hasta el 80 %");

        var cuota = await Evaluar(150_000_000m, vis: false, ingreso: 3_000_000m);
        cuota.Error.Code.Should().Be(ReglasDeVivienda.CodigoCuotaIngresoExcedida);
        LineasDePrueba.Datos(cuota.Error).GetProperty("limitCode").GetString().Should().Be(CatalogoDeLimitesLegales.ViviendaCuotaIngreso);
    }

    [Fact]
    public async Task Una_hipoteca_que_no_es_de_primer_grado_no_deja_desembolsar_vivienda()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();
        var linea = await _l.LineaAsync("VIV", LineasDePrueba.Vivienda());
        var simulacion = (await new SimuladorDeCredito(_l.Db, _l.Reglas()).SimularAsync(await LineaAsync(linea.PublicId),
            new ScheduleSimulationInput(linea.PublicId, 100_000_000m, 120), PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default)).Value;

        var segundoGrado = await SolicitudAsync(linea.PublicId, false, 150_000_000m, 6_000_000m, rango: 2);
        (await ValidacionDeVivienda.GarantiasAsync(_l.Db, segundoGrado, simulacion, default))!.Code.Should().Be(ReglasDeVivienda.CodigoHipotecaDePrimerGrado);
        var primerGrado = await SolicitudAsync(linea.PublicId, false, 150_000_000m, 6_000_000m, rango: 1);
        (await ValidacionDeVivienda.GarantiasAsync(_l.Db, primerGrado, simulacion, default)).Should().BeNull();
    }

    // ----------------------------------------------------------------------------------------------- la serie UVR --

    [Fact]
    public async Task La_serie_UVR_trae_solo_lo_publicado_para_cada_dia()
    {
        await _l.UvrAsync((new DateOnly(2026, 12, 1), 407.7518m), (new DateOnly(2026, 12, 2), 407.8040m), (new DateOnly(2026, 12, 4), 407.9085m));
        var series = new SeriesRegulatoriasDeCartera(_l.Valores(), _l.Normativa(), Substitute.For<ICalendarioHabil>(),
            new ReglasDeParametrosDeCartera(_l.Valores(), Substitute.For<IngenIA365ERP.Application.Common.Parameters.ILectorDeParametros>()));

        var serie = await series.LeerUvrAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), null, default);

        serie.Valores.Select(v => (v.Fecha, v.Valor)).Should().Equal(
            (new DateOnly(2026, 12, 1), 407.7518m), (new DateOnly(2026, 12, 2), 407.8040m), (new DateOnly(2026, 12, 4), 407.9085m));
        serie.Publicada(new DateOnly(2026, 12, 3)).Should().BeNull("un día sin publicar no trae valor: el crédito en UVR no se causa ese día");
        serie.Del(new DateOnly(2026, 12, 3)).Proyectada.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------ piezas --

    private Task<Domain.Entities.Lending.Configuration.CreditLine> LineaAsync(Guid publicId) =>
        _l.Db.CreditLines.AsNoTracking().SingleAsync(l => l.PublicId == publicId);

    private async Task<CreditApplication> SolicitudAsync(Guid linea, bool vis, decimal inmueble, decimal ingreso, byte rango)
    {
        var entidad = await LineaAsync(linea);
        var version = await _l.Db.CreditLineVersions.AsNoTracking().SingleAsync(v => v.CreditLineId == entidad.Id);
        var solicitante = _l.G.Persona("Vera", "Vivienda", Guid.NewGuid().ToString("N")[..8]);
        var s = new CreditApplication
        {
            ApplicantPersonId = solicitante.Id, DebtorKind = DebtorKind.Associate, BranchId = 1, CreditLineId = entidad.Id, CreditLineVersionId = version.Id,
            RequestedAmount = 100_000_000m, RepaymentChannel = RepaymentChannel.Cashier, IsSocialHousing = vis, CreatedBy = "test",
        };
        _l.Db.CreditApplications.Add(s);
        await _l.Db.SaveChangesAsync();
        var parte = CreditApplicationParty.Nueva(s.Id, solicitante.Id, CreditPartyRole.Applicant, 1);
        parte.MonthlyIncome = ingreso;
        _l.Db.CreditApplicationParties.Add(parte);
        _l.Db.Collaterals.Add(new Collateral
        {
            Kind = CollateralKind.Mortgage, OwnerPersonId = solicitante.Id, ApplicationId = s.Id, Description = "Apartamento", Value = inmueble,
            AdmissibleValue = inmueble, MortgageRank = rango, Status = CollateralStatus.Constituted, ConstitutedOn = LineasDePrueba.Hoy,
        });
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        return s;
    }
}
