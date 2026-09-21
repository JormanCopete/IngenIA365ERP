using FluentAssertions;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
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
/// Pendiente de la ola 2 (cuando existan las entidades <c>TerminationReason</c>, <c>Holiday</c> y
/// <c>CompanyPolicy</c> y sus seeders): <c>TerminationReasonsSeeder</c> no cambia
/// <c>GeneratesSeverancePay</c> de un motivo existente; <c>HolidaysSeeder</c> no pisa un festivo
/// <c>Decreed</c>; <c>CompanyPoliciesSeeder</c> no pisa una vigencia existente.
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
}
