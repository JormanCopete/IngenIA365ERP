using FluentAssertions;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Persistence.Seeding;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 010 (T018): las semillas de nómina se reaplican en cada arranque sobre cooperativas
/// que ya las tenían, así que lo que una persona editó no se puede pisar y correrlas dos veces
/// tiene que dar lo mismo que una. Aquí van <see cref="PayrollLegalParametersSeeder"/> y
/// <see cref="PayrollConceptDefinitionsSeeder"/> sobre el contexto InMemory (por eso los seeders
/// exponen <c>AplicarAsync(IApplicationDbContext)</c>).
///
/// <para>
/// Y los tres de la feature 010 (T017): <see cref="TerminationReasonsSeeder"/> no cambia
/// <c>GeneratesSeverancePay</c> de un motivo existente; <see cref="HolidaysSeeder"/> no pisa un
/// festivo <c>Decreed</c>; <see cref="CompanyPoliciesSeeder"/> no pisa una vigencia existente.
/// </para>
/// </summary>
public class SemillasDeNominaIdempotentesTests
{
    private static readonly DateTime Ley2381Desde = new(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------ parámetros legales --

    [Fact]
    public async Task Los_parametros_legales_se_siembran_una_vez_y_la_segunda_pasada_no_inserta_nada()
    {
        using var db = TestDbContextFactory.Create();

        var primera = await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        primera.Should().Be(PayrollLegalParametersSeeder.Catalogo().Count + PayrollLegalParametersSeeder.Revisiones().Count);
        segunda.Should().Be(0);
        (await db.PayrollLegalParameters.CountAsync()).Should().Be(primera);
    }

    [Fact]
    public async Task El_catalogo_de_parametros_trae_norma_exacta_sin_textos_genericos_y_codigos_unicos()
    {
        var catalogo = PayrollLegalParametersSeeder.Catalogo();

        catalogo.Select(p => p.Code).Should().OnlyHaveUniqueItems("una vigencia por código en la base del año; las demás van en Revisiones()");
        catalogo.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Source) && p.Source!.Length <= 200);
        catalogo.Should().OnlyContain(p => !PayrollLegalParametersSeeder.FuentesGenericasAnteriores.Contains(p.Source!),
            "la contadora tiene que poder trazar cada valor a su artículo (SC-001)");
        catalogo.Should().OnlyContain(p => p.Name.Length <= 120 && p.Code.Length <= 40);
        catalogo.Where(p => p.Kind != LegalParameterKind.RangeTable).Should().OnlyContain(p => p.Value != null);
        catalogo.Where(p => p.Kind == LegalParameterKind.RangeTable).Should().OnlyContain(p => p.Value == null && p.Ranges.Count > 0);
        // La nómina ordinaria sigue encontrando todo lo que exige.
        LegalParameterCodes.Required.Except(catalogo.Select(p => p.Code)).Should().BeEmpty();
    }

    [Fact]
    public void Las_fechas_del_anio_son_MMDD_validos()
    {
        var fechas = PayrollLegalParametersSeeder.Catalogo().Where(p => p.Kind == LegalParameterKind.DateInYear).ToList();

        fechas.Select(p => p.Code).Should().BeEquivalentTo(
            ["PRIMA_FECHA_LIMITE_S1", "PRIMA_FECHA_LIMITE_S2", "CESANTIAS_FECHA_LIMITE_CONSIGNACION", "INT_CESANTIAS_FECHA_LIMITE"]);
        foreach (var p in fechas)
        {
            var mmdd = (int)p.Value!.Value;
            var acto = () => new DateOnly(2026, mmdd / 100, mmdd % 100);
            acto.Should().NotThrow($"{p.Code} = {mmdd} debe ser una fecha del año válida");
        }
        fechas.Single(p => p.Code == "PRIMA_FECHA_LIMITE_S2").Value.Should().Be(1220m);
    }

    [Fact]
    public void Las_tablas_por_rangos_son_contiguas_y_la_de_indemnizacion_lleva_dos_valores_por_tramo()
    {
        var tablas = PayrollLegalParametersSeeder.Catalogo().Concat(PayrollLegalParametersSeeder.Revisiones())
            .Where(p => p.Kind == LegalParameterKind.RangeTable).ToList();

        foreach (var t in tablas)
        {
            var tramos = t.Ranges.OrderBy(r => r.Order)
                .Select(r => new LegalParameterRangeInput(r.FromValue, r.ToValue, r.Rate, r.FixedValue)).ToList();
            AddLegalParameterVersionCommandHandler.ValidarTramos(tramos).Should().BeNull($"{t.Code}: la pantalla exige la misma regla al editar la tabla");
        }

        var indemnizacion = tablas.Single(t => t.Code == "INDEMNIZACION_TABLA");
        indemnizacion.RangeUnitParameterCode.Should().Be(LegalParameterCodes.Smmlv);
        indemnizacion.RangeIsMarginal.Should().BeFalse();
        var menorDeDiez = indemnizacion.Ranges.Single(r => r.FromValue == 0m);
        (menorDeDiez.FixedValue, menorDeDiez.Rate).Should().Be((30m, 20m), "D-08: FixedValue = días del 1.er año, Rate = días por año adicional");
        var diezOMas = indemnizacion.Ranges.Single(r => r.FromValue == 10m);
        (diezOMas.FixedValue, diezOMas.Rate, diezOMas.ToValue).Should().Be((20m, 15m, (decimal?)null));

        var noGravado = tablas.Single(t => t.Code == "CESANTIAS_GRAVADA_TABLA_UVT");
        noGravado.Ranges.OrderBy(r => r.Order).Select(r => r.Rate).Should().Equal(100m, 90m, 80m, 60m, 40m, 20m, 0m);
    }

    [Fact]
    public async Task Un_source_editado_a_mano_no_se_pisa_y_uno_generico_si_se_precisa()
    {
        using var db = TestDbContextFactory.Create();
        var base2026 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Una cooperativa sembrada con la semilla de la feature 005: textos genéricos.
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Smmlv, Name = "Salario mínimo", Kind = LegalParameterKind.Amount, Value = 1_750_905m,
            ValidFrom = base2026, Source = "Decreto de salario mínimo y auxilio de transporte 2026", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        // …y una fila que la contadora ya precisó por su cuenta.
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Uvt, Name = "UVT", Kind = LegalParameterKind.Amount, Value = 52_374m,
            ValidFrom = base2026, Source = "Resolución 000238 de 2025, revisada por Rafaela", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var smmlv = await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Smmlv);
        smmlv.Source.Should().Be(PayrollLegalParametersSeeder.Catalogo().Single(p => p.Code == LegalParameterCodes.Smmlv).Source);
        smmlv.Value.Should().Be(1_750_905m, "sólo cambia la norma, nunca el valor");
        smmlv.UpdatedBy.Should().Be(SeedContext.ParametricCreatedBy);

        var uvt = await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt);
        uvt.Source.Should().Be("Resolución 000238 de 2025, revisada por Rafaela");
        uvt.UpdatedBy.Should().BeNull();
        (await db.PayrollLegalParameters.CountAsync(p => p.Code == LegalParameterCodes.Uvt)).Should().Be(1, "la vigencia ya existía: no se duplica");
    }

    [Fact]
    public async Task Revisiones_cierra_la_tabla_del_fondo_de_solidaridad_el_31_de_marzo_de_2027_si_esta_intacta()
    {
        using var db = TestDbContextFactory.Create();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var versiones = await db.PayrollLegalParameters.Include(p => p.Ranges)
            .Where(p => p.Code == LegalParameterCodes.SolidarityFundTable).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateTime(2027, 3, 31, 0, 0, 0, DateTimeKind.Utc), "la Ley 797 rige hasta el día antes de la Ley 2381");
        versiones[1].ValidFrom.Should().Be(Ley2381Desde);
        versiones[1].ValidTo.Should().BeNull();
        versiones[1].Ranges.OrderBy(r => r.Order).Select(r => (r.FromValue, r.Rate)).Should()
            .Equal((4m, 1.5m), (7m, 1.8m), (11m, 2.5m), (19m, 2.8m), (20m, 3.0m));

        // Con las dos vigencias, el motor toma la que aplique a la fecha.
        new ParameterSet(versiones, new DateTime(2027, 3, 31)).Table(LegalParameterCodes.SolidarityFundTable).ValidFrom.Should().Be(versiones[0].ValidFrom);
        new ParameterSet(versiones, new DateTime(2027, 4, 1)).Table(LegalParameterCodes.SolidarityFundTable).ValidFrom.Should().Be(Ley2381Desde);
    }

    [Fact]
    public async Task Revisiones_no_cierra_una_tabla_que_alguien_edito_a_mano_pero_si_inserta_la_nueva()
    {
        using var db = TestDbContextFactory.Create();
        var editada = PayrollLegalParametersSeeder.Catalogo().Single(p => p.Code == LegalParameterCodes.SolidarityFundTable);
        editada.UpdatedBy = "contadora@coop";
        editada.UpdatedAt = DateTime.UtcNow;
        db.PayrollLegalParameters.Add(editada);
        await db.SaveChangesAsync();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var versiones = await db.PayrollLegalParameters
            .Where(p => p.Code == LegalParameterCodes.SolidarityFundTable).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().BeNull("la tocó una persona: su vigencia es suya");
        versiones[0].UpdatedBy.Should().Be("contadora@coop");
        versiones[1].ValidFrom.Should().Be(Ley2381Desde);
    }

    [Fact]
    public async Task Revisiones_respeta_una_vigencia_registrada_a_mano_igual_o_posterior()
    {
        using var db = TestDbContextFactory.Create();
        var propia = PayrollLegalParametersSeeder.Catalogo().Single(p => p.Code == LegalParameterCodes.SolidarityFundTable);
        propia.ValidFrom = new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        propia.Source = "Cargada a mano por la cooperativa";
        propia.CreatedBy = "contadora@coop";
        db.PayrollLegalParameters.Add(propia);
        await db.SaveChangesAsync();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);
        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var versiones = await db.PayrollLegalParameters
            .Where(p => p.Code == LegalParameterCodes.SolidarityFundTable).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2, "la base 2026 y la propia de 2027; la revisión de la semilla no entra");
        versiones[1].Source.Should().Be("Cargada a mano por la cooperativa");
        versiones[0].ValidTo.Should().BeNull("la revisión no entró, así que tampoco cerró nada");
    }

    // ---------------------------------------------------------------- conceptos --

    [Fact]
    public async Task Los_conceptos_se_siembran_una_vez_y_la_segunda_pasada_no_inserta_nada()
    {
        using var db = TestDbContextFactory.Create();

        var primera = await PayrollConceptDefinitionsSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await PayrollConceptDefinitionsSeeder.AplicarAsync(db, CancellationToken.None);

        primera.Should().Be(PayrollConceptDefinitionsSeeder.Catalogo().Count);
        segunda.Should().Be(0);
    }

    [Fact]
    public void Los_conceptos_de_liquidacion_estan_en_la_semilla_y_la_nomina_ordinaria_los_salta()
    {
        var catalogo = PayrollConceptDefinitionsSeeder.Catalogo();
        catalogo.Select(c => c.Code).Should().OnlyHaveUniqueItems();

        foreach (var code in WellKnownConceptCodes.SettlementOnly)
        {
            var def = catalogo.Should().ContainSingle(c => c.Code == code, $"{code} es de la semilla 010").Subject;
            def.IsAutomatic.Should().BeTrue($"{code} lo pone el motor de liquidaciones, no una novedad");
            def.Origin.Should().Be(ConceptOrigin.Seed);
        }
        catalogo.Single(c => c.Code == WellKnownConceptCodes.RetirementBonus).RequiresAmount.Should().BeTrue("la bonificación por retiro la trae la novedad de la definitiva");
        var ausencia = catalogo.Single(c => c.Code == WellKnownConceptCodes.VacationLeave);
        (ausencia.Nature, ausencia.ReducesWorkedDays, ausencia.RequiresDates, ausencia.IsAutomatic)
            .Should().Be((ConceptNature.Informative, true, true, false));
        catalogo.Where(c => c.Code.EndsWith("_AJUSTE_PROV", StringComparison.Ordinal)).Should().HaveCount(4)
            .And.OnlyContain(c => c.Nature == ConceptNature.Provision && c.DianElement == null);
    }

    [Fact]
    public void La_base_de_vacaciones_y_la_ruta_dian_estan_en_su_sitio()
    {
        var porCodigo = PayrollConceptDefinitionsSeeder.Catalogo().ToDictionary(c => c.Code);

        foreach (var si in new[] { "SALARIO", "COMISION", "BONIF_SALARIAL", "RECARGO_NOCTURNO" })
            porCodigo[si].AffectsVacationBase.Should().BeTrue($"{si} es salario ordinario (CST art. 192)");
        foreach (var no in new[] { "AUX_TRANSPORTE", "HEX_DIURNA", "HEX_NOCTURNA", "HEX_DOM_DIURNA", "HEX_DOM_NOCTURNA", "RECARGO_DOMINICAL" })
            porCodigo[no].AffectsVacationBase.Should().BeFalse($"{no} no entra a la base de vacaciones");

        porCodigo["SALARIO"].DianElement.Should().Be("Devengados/Basico");
        porCodigo["SALUD_EMP"].DianElement.Should().Be("Deducciones/Salud");
        porCodigo["PRIMA"].DianElement.Should().Be("Devengados/Primas/Prima");
        // Aportes del empleador y provisiones no van al documento.
        porCodigo.Values.Where(c => c.Nature is ConceptNature.EmployerContribution or ConceptNature.Provision)
            .Should().OnlyContain(c => c.DianElement == null);
        porCodigo.Values.Where(c => c.DianElement != null).Should().OnlyContain(c => c.DianElement!.Length <= 60);
    }

    [Fact]
    public async Task Las_versiones_sembradas_reciben_las_columnas_nuevas_y_un_concepto_propio_no_se_toca()
    {
        using var db = TestDbContextFactory.Create();
        // Una cooperativa con la semilla 005 (sin las columnas de la 010) y un concepto propio.
        var salario = PayrollConceptDefinitionsSeeder.Catalogo().Single(c => c.Code == "SALARIO");
        salario.AffectsVacationBase = false;
        salario.DianElement = null;
        var propio = new PayrollConceptDefinition
        {
            Code = "BONO_COOP", Name = "Bono cooperativo", Nature = ConceptNature.Earning, CalculationKind = CalculationKind.FixedAmount,
            RequiresAmount = true, Origin = ConceptOrigin.Custom, ValidFrom = new DateTime(2026, 1, 1), IsActive = true, CreatedBy = "contadora@coop",
            DianElement = "Devengados/OtrosConceptos/ConceptoS",
        };
        db.PayrollConceptDefinitions.AddRange(salario, propio);
        await db.SaveChangesAsync();

        await PayrollConceptDefinitionsSeeder.AplicarAsync(db, CancellationToken.None);

        var sembrado = await db.PayrollConceptDefinitions.SingleAsync(c => c.Code == "SALARIO");
        sembrado.AffectsVacationBase.Should().BeTrue();
        sembrado.DianElement.Should().Be("Devengados/Basico");
        sembrado.UpdatedBy.Should().Be(SeedContext.ParametricCreatedBy);
        (await db.PayrollConceptDefinitions.CountAsync(c => c.Code == "SALARIO")).Should().Be(1, "se puso en su sitio, no en una versión nueva");

        var custom = await db.PayrollConceptDefinitions.SingleAsync(c => c.Code == "BONO_COOP");
        custom.DianElement.Should().Be("Devengados/OtrosConceptos/ConceptoS");
        custom.UpdatedBy.Should().BeNull();
    }

    /// <summary>Revisión de N1 (D-29): la semilla gobierna las dos columnas sólo en las filas que ella creó.</summary>
    [Fact]
    public async Task Una_version_que_una_persona_registro_al_revisar_un_concepto_sembrado_no_se_pisa()
    {
        using var db = TestDbContextFactory.Create();
        var comision = PayrollConceptDefinitionsSeeder.Catalogo().Single(c => c.Code == "COMISION");
        comision.ValidTo = new DateTime(2026, 9, 30);
        var revisada = new PayrollConceptDefinition
        {
            Code = "COMISION", Name = "Comisiones por ventas", Nature = comision.Nature, CalculationKind = comision.CalculationKind, RequiresAmount = true,
            AffectsSalaryBase = true, AffectsContributionBase = true, AffectsBenefitsBase = true, AffectsWithholdingBase = true,
            AffectsVacationBase = false, DianElement = null, // la contadora decidió que esta comisión no entra a la base de vacaciones
            Origin = ConceptOrigin.Seed, ValidFrom = new DateTime(2026, 10, 1), IsActive = true, CreatedBy = "contadora@coop",
        };
        db.PayrollConceptDefinitions.AddRange(comision, revisada);
        await db.SaveChangesAsync();

        await PayrollConceptDefinitionsSeeder.AplicarAsync(db, CancellationToken.None);
        await PayrollConceptDefinitionsSeeder.AplicarAsync(db, CancellationToken.None);

        var versiones = await db.PayrollConceptDefinitions.Where(c => c.Code == "COMISION").OrderBy(c => c.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].AffectsVacationBase.Should().BeTrue("la que creó la semilla lleva lo del catálogo");
        versiones[1].AffectsVacationBase.Should().BeFalse("la que registró la contadora lleva lo que ella decidió, en todos los arranques");
        versiones[1].DianElement.Should().BeNull();
        versiones[1].UpdatedBy.Should().BeNull();
    }

    // ---------------------------------------------------- motivos de retiro (010) --

    [Fact]
    public async Task Los_motivos_de_retiro_se_siembran_una_vez_con_su_marca_legal()
    {
        using var db = TestDbContextFactory.Create();

        var primera = await TerminationReasonsSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await TerminationReasonsSeeder.AplicarAsync(db, CancellationToken.None);

        primera.Should().Be(9);
        segunda.Should().Be(0);
        var motivos = await db.TerminationReasons.ToListAsync();
        motivos.Select(m => m.Code).Should().BeEquivalentTo(["RENUNCIA", "DESP_SINJC", "DESP_JC", "VENC_TERM", "MUTUO_ACDO", "FIN_OBRA", "PER_PRUEBA", "MUERTE", "PENSION"]);
        motivos.Where(m => m.GeneratesSeverancePay).Select(m => m.Code).Should().ContainSingle("sólo el despido sin justa causa indemniza (CST art. 64)").Which.Should().Be("DESP_SINJC");
        motivos.Should().OnlyContain(m => m.IsSeeded && m.IsActive && !string.IsNullOrWhiteSpace(m.LegalBasis) && m.Code.Length <= 10);
    }

    [Fact]
    public async Task El_seeder_de_motivos_corrige_textos_pero_nunca_la_marca_de_indemnizacion_ni_un_motivo_propio()
    {
        using var db = TestDbContextFactory.Create();
        // Un sembrado con la marca cambiada a mano por error y el nombre viejo; y un motivo propio con código de la semilla.
        db.TerminationReasons.Add(new TerminationReason { Code = "DESP_SINJC", Name = "Despido", GeneratesSeverancePay = false, IsSeeded = true, CreatedBy = SeedContext.ParametricCreatedBy });
        db.TerminationReasons.Add(new TerminationReason { Code = "RENUNCIA", Name = "Renuncia (propio)", GeneratesSeverancePay = true, IsSeeded = false, CreatedBy = "contadora@coop" });
        await db.SaveChangesAsync();

        var insertados = await TerminationReasonsSeeder.AplicarAsync(db, CancellationToken.None);

        insertados.Should().Be(7, "los dos códigos existentes no se insertan");
        var despido = await db.TerminationReasons.SingleAsync(m => m.Code == "DESP_SINJC");
        despido.Name.Should().Be("Despido sin justa causa", "el texto sí se pone al día");
        despido.LegalBasis.Should().NotBeNullOrEmpty();
        despido.GeneratesSeverancePay.Should().BeFalse("la marca nunca la toca la semilla: si alguien la cambió, es decisión suya");
        var propio = await db.TerminationReasons.SingleAsync(m => m.Code == "RENUNCIA");
        propio.Name.Should().Be("Renuncia (propio)");
        propio.GeneratesSeverancePay.Should().BeTrue();
        propio.UpdatedBy.Should().BeNull();
    }

    // ------------------------------------------------------------ festivos (010) --

    [Fact]
    public async Task Los_festivos_de_tres_anios_se_siembran_una_vez_y_no_pisan_un_puente_decretado()
    {
        using var db = TestDbContextFactory.Create();
        // La cooperativa registró un puente decretado justo en una fecha de la Ley 51 y un festivo manual en otra.
        db.Holidays.Add(new Holiday { Date = new DateOnly(2026, 11, 2), Name = "Puente decretado", Origin = HolidayOrigin.Decreed, Year = 2026, CreatedBy = "contadora@coop" });
        db.Holidays.Add(new Holiday { Date = new DateOnly(2026, 12, 24), Name = "Día de la cooperativa", Origin = HolidayOrigin.Manual, Year = 2026, CreatedBy = "contadora@coop" });
        await db.SaveChangesAsync();

        var primera = await HolidaysSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await HolidaysSeeder.AplicarAsync(db, CancellationToken.None);

        primera.Should().Be(HolidaysSeeder.Años.Count * 18 - 1, "18 por año menos la fecha que ya era festivo por decreto");
        segunda.Should().Be(0);
        var todosLosSantos = await db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 11, 2));
        todosLosSantos.Origin.Should().Be(HolidayOrigin.Decreed, "la fila de la cooperativa se respeta");
        todosLosSantos.Name.Should().Be("Puente decretado");
        (await db.Holidays.CountAsync(h => h.Origin == HolidayOrigin.Manual)).Should().Be(1);
        (await db.Holidays.CountAsync(h => h.Year == 2027)).Should().Be(18);
        (await db.Holidays.Where(h => h.Year == 2026).Select(h => h.Date).ToListAsync()).Should().OnlyHaveUniqueItems();
        (await db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 1, 12))).Origin.Should().Be(HolidayOrigin.Ley51MovedToMonday, "Reyes cae martes 6 y se corre al lunes 12");
        (await db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 4, 3))).Origin.Should().Be(HolidayOrigin.Ley51Easter, "Viernes Santo");
        (await db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 12, 25))).Origin.Should().Be(HolidayOrigin.Ley51Fixed);
    }

    // ----------------------------------------------------------- políticas (010) --

    [Fact]
    public async Task Las_politicas_nacen_con_su_defecto_y_una_vigencia_existente_no_se_pisa()
    {
        using var db = TestDbContextFactory.Create();
        db.CompanyPolicies.Add(new CompanyPolicy { Key = CompanyPolicyKeys.SemanaLaboral, Value = CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes, ValidFrom = new DateOnly(2026, 7, 1), CreatedBy = "contadora@coop" });
        db.SystemSettings.Add(new SystemSetting { SettingKey = "Payroll.ApplyEmployerExemption", SettingValue = "true", ValueType = "Bool", ModulePrefix = "PAY", CreatedBy = "test" });
        await db.SaveChangesAsync();

        var primera = await CompanyPoliciesSeeder.AplicarAsync(db, CancellationToken.None);
        var segunda = await CompanyPoliciesSeeder.AplicarAsync(db, CancellationToken.None);

        // Once claves menos SemanaLaboral (ya tenía vigencia) y ArranqueNominaFecha (sin períodos no se siembra).
        primera.Should().Be(CompanyPolicyKeys.Todas.Count - 2);
        segunda.Should().Be(0);
        var semana = await db.CompanyPolicies.Where(p => p.Key == CompanyPolicyKeys.SemanaLaboral).ToListAsync();
        semana.Should().ContainSingle().Which.Value.Should().Be(CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes, "la decisión de la cooperativa manda aunque no cubra todas las fechas");
        (await db.CompanyPolicies.SingleAsync(p => p.Key == CompanyPolicyKeys.Exonerada114_1)).Value.Should().Be("true", "copiada de COR_SystemSettings");
        (await db.CompanyPolicies.SingleAsync(p => p.Key == CompanyPolicyKeys.AllowSameUserApproval)).Value.Should().Be("false", "sin setting heredado, falso");
        (await db.CompanyPolicies.SingleAsync(p => p.Key == CompanyPolicyKeys.VacacionesPagoAnticipado)).ValidFrom.Should().Be(CompanyPoliciesSeeder.VigenciaAbierta);
        (await db.CompanyPolicies.AnyAsync(p => p.Key == CompanyPolicyKeys.ArranqueNominaFecha)).Should().BeFalse();
    }

    [Fact]
    public async Task El_arranque_de_la_nomina_es_la_fecha_del_primer_periodo()
    {
        using var db = TestDbContextFactory.Create();
        db.PayPeriods.Add(new PayPeriod { PlanId = 2, PayrollPlanId = 1, StartDate = new DateTime(2026, 12, 16), EndDate = new DateTime(2026, 12, 31), CreatedBy = "test", StatusMessage = string.Empty, AdvanceLiquidation = "N", AdvanceCrossing = "N" });
        db.PayPeriods.Add(new PayPeriod { PlanId = 1, PayrollPlanId = 1, StartDate = new DateTime(2026, 12, 1), EndDate = new DateTime(2026, 12, 15), CreatedBy = "test", StatusMessage = string.Empty, AdvanceLiquidation = "N", AdvanceCrossing = "N" });
        await db.SaveChangesAsync();

        await CompanyPoliciesSeeder.AplicarAsync(db, CancellationToken.None);

        (await db.CompanyPolicies.SingleAsync(p => p.Key == CompanyPolicyKeys.ArranqueNominaFecha)).Value.Should().Be("2026-12-01");
    }
}
