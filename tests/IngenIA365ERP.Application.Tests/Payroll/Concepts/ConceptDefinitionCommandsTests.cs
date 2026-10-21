using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Concepts;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Concepts;

/// <summary>T080 — FR-027..FR-029: definiciones por forma de cálculo, referencias, ciclos, versiones.</summary>
public class ConceptDefinitionCommandsTests
{
    private static ConceptDefinitionInput Bonificacion(string code = "BONIF_ANTIGUEDAD") => new()
    {
        Code = code, Name = "Bonificación por antigüedad", Nature = ConceptNature.Earning, CalculationKind = CalculationKind.PercentOfBase,
        BaseKind = CalculationBase.BasicSalary, Percent = 2m, AffectsWithholdingBase = true, IsAutomatic = true, ValidFrom = new DateTime(2026, 4, 1),
    };

    [Fact]
    public async Task Crea_un_concepto_propio_y_desactualiza_los_borradores()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);
        var h = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await h.Handle(new CreateConceptDefinitionCommand(Bonificacion()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var c = await d.Db.PayrollConceptDefinitions.SingleAsync(x => x.PublicId == r.Value);
        c.Origin.Should().Be(ConceptOrigin.Custom);
        c.Code.Should().Be("BONIF_ANTIGUEDAD");
        c.IsActive.Should().BeTrue();
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task Un_codigo_repetido_se_rechaza()
    {
        var d = new NominaTestData();
        var h = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await h.Handle(new CreateConceptDefinitionCommand(Bonificacion("SALARIO")), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptCodeDuplicate");
    }

    [Fact]
    public async Task Cada_forma_exige_sus_campos()
    {
        var d = new NominaTestData();
        var h = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var sinBase = Bonificacion() with { BaseKind = null };
        var r1 = await h.Handle(new CreateConceptDefinitionCommand(sinBase), CancellationToken.None);
        r1.Error.Code.Should().Be("Payroll.ConceptFormIncomplete");
        r1.Error.Message.Should().Contain("base");

        var tablaSinParametro = Bonificacion() with { CalculationKind = CalculationKind.RangeTable, TableParameterCode = null };
        var r2 = await h.Handle(new CreateConceptDefinitionCommand(tablaSinParametro), CancellationToken.None);
        r2.Error.Code.Should().Be("Payroll.ConceptFormIncomplete");
    }

    [Fact]
    public async Task Una_referencia_a_un_parametro_inexistente_se_rechaza()
    {
        var d = new NominaTestData();
        var h = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await h.Handle(new CreateConceptDefinitionCommand(Bonificacion() with { Percent = null, PercentParameterCode = "NO_EXISTE_PCT" }), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptReferenceNotFound");
        r.Error.Message.Should().Contain("NO_EXISTE_PCT");
    }

    [Fact]
    public async Task Un_ciclo_entre_compuestos_se_rechaza_con_el_camino()
    {
        var d = new NominaTestData();
        var h = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);
        var a = new ConceptDefinitionInput { Code = "COMP_A", Name = "A", Nature = ConceptNature.Earning, CalculationKind = CalculationKind.CompositeOfConcepts, ComponentConceptCodes = "+HEX_DIURNA", ValidFrom = new DateTime(2026, 1, 1) };
        (await h.Handle(new CreateConceptDefinitionCommand(a), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var b = a with { Code = "COMP_B", Name = "B", ComponentConceptCodes = "+COMP_A" };
        (await h.Handle(new CreateConceptDefinitionCommand(b), CancellationToken.None)).IsSuccess.Should().BeTrue();

        // A pasa a depender de B: A → B → A.
        var revisar = new ReviseConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);
        var r = await revisar.Handle(new ReviseConceptDefinitionCommand("COMP_A", a with { ComponentConceptCodes = "+COMP_B", ValidFrom = new DateTime(2026, 6, 1) }), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptCycle");
        r.Error.Message.Should().Contain("COMP_A").And.Contain("COMP_B");
    }

    [Fact]
    public async Task Revisar_crea_version_nueva_y_cierra_la_anterior_sin_tocar_las_liquidaciones()
    {
        var d = new NominaTestData();
        var revisar = new ReviseConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);
        var vigente = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.Code == "HEX_NOCTURNA");
        var idVigente = vigente.Id;

        var r = await revisar.Handle(new ReviseConceptDefinitionCommand("HEX_NOCTURNA", new ConceptDefinitionInput
        {
            Code = "HEX_NOCTURNA", Name = vigente.Name, Nature = vigente.Nature, CalculationKind = vigente.CalculationKind,
            UnitKind = vigente.UnitKind, UnitFactor = 2m, RequiresQuantity = true, AllowsRepeatInPeriod = true,
            AffectsSalaryBase = true, AffectsContributionBase = true, AffectsBenefitsBase = true, AffectsWithholdingBase = true,
            ValidFrom = new DateTime(2026, 7, 1),
        }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var versiones = await d.Db.PayrollConceptDefinitions.Where(c => c.Code == "HEX_NOCTURNA").OrderBy(c => c.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].Id.Should().Be(idVigente, "la versión anterior conserva su Id: las liquidaciones aprobadas la siguen referenciando");
        versiones[0].ValidTo.Should().Be(new DateTime(2026, 6, 30));
        versiones[0].UnitFactor.Should().Be(1.75m);
        versiones[1].UnitFactor.Should().Be(2m);
        versiones[1].Origin.Should().Be(ConceptOrigin.Seed, "la versión hereda el origen");
    }

    /// <summary>Revisión de N1 (D-37): las dos columnas de la 010 van en el contrato, y al revisar se heredan si no se mandan.</summary>
    [Fact]
    public async Task La_base_de_vacaciones_y_la_ruta_dian_se_fijan_en_un_concepto_propio_y_se_heredan_al_revisar()
    {
        var d = new NominaTestData();
        var crear = new CreateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);
        var revisar = new ReviseConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var propio = await crear.Handle(new CreateConceptDefinitionCommand(Bonificacion("BONO_SAL_COOP") with
        {
            AffectsSalaryBase = true, AffectsBenefitsBase = true, AffectsVacationBase = true, DianElement = "Devengados/Bonificaciones/BonificacionS",
        }), CancellationToken.None);
        propio.IsSuccess.Should().BeTrue(propio.Error.Message);
        var creado = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.PublicId == propio.Value);
        creado.AffectsVacationBase.Should().BeTrue("un concepto salarial propio entra a la base de vacaciones si la cooperativa lo dice");
        creado.DianElement.Should().Be("Devengados/Bonificaciones/BonificacionS");

        // COMISION sembrada: base de vacaciones y ruta DIAN puestas por la semilla.
        var comision = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.Code == "COMISION");
        comision.AffectsVacationBase = true;
        comision.DianElement = "Devengados/Comisiones/Comision";
        await d.Db.SaveChangesAsync();

        // Revisar sólo el nombre, sin mandar las dos columnas: se heredan.
        var r1 = await revisar.Handle(new ReviseConceptDefinitionCommand("COMISION", new ConceptDefinitionInput
        {
            Code = "COMISION", Name = "Comisiones por ventas", Nature = comision.Nature, CalculationKind = comision.CalculationKind, RequiresAmount = true,
            AffectsSalaryBase = true, AffectsContributionBase = true, AffectsBenefitsBase = true, AffectsWithholdingBase = true, ValidFrom = new DateTime(2026, 10, 1),
        }), CancellationToken.None);
        r1.IsSuccess.Should().BeTrue(r1.Error.Message);
        var v2 = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.PublicId == r1.Value);
        v2.AffectsVacationBase.Should().BeTrue("la revisión hereda la base de vacaciones: la comisión de octubre entra al promedio de vacaciones");
        v2.DianElement.Should().Be("Devengados/Comisiones/Comision");

        // Revisar mandando las dos: lo que se manda vale; la cadena vacía quita la ruta.
        var r2 = await revisar.Handle(new ReviseConceptDefinitionCommand("COMISION", new ConceptDefinitionInput
        {
            Code = "COMISION", Name = "Comisiones", Nature = comision.Nature, CalculationKind = comision.CalculationKind, RequiresAmount = true,
            AffectsSalaryBase = true, AffectsVacationBase = false, DianElement = string.Empty, ValidFrom = new DateTime(2026, 11, 1),
        }), CancellationToken.None);
        r2.IsSuccess.Should().BeTrue(r2.Error.Message);
        var v3 = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.PublicId == r2.Value);
        v3.AffectsVacationBase.Should().BeFalse();
        v3.DianElement.Should().BeNull();
    }

    [Fact]
    public void La_ruta_dian_tiene_forma_de_ruta_del_xml()
    {
        var validador = new ConceptDefinitionInputValidator();
        validador.Validate(Bonificacion() with { DianElement = "Devengados/Basico" }).IsValid.Should().BeTrue();
        validador.Validate(Bonificacion() with { DianElement = "Devengados/Cesantias/@PagoIntereses" }).IsValid.Should().BeTrue();
        validador.Validate(Bonificacion() with { DianElement = "salario básico" }).IsValid.Should().BeFalse();
        validador.Validate(Bonificacion() with { DianElement = new string('A', 61) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Revisar_con_vigencia_anterior_a_la_actual_se_rechaza()
    {
        var d = new NominaTestData();
        var revisar = new ReviseConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await revisar.Handle(new ReviseConceptDefinitionCommand("COMISION", new ConceptDefinitionInput
        {
            Code = "COMISION", Name = "Comisiones", Nature = ConceptNature.Earning, CalculationKind = CalculationKind.FixedAmount, RequiresAmount = true, ValidFrom = new DateTime(2025, 12, 1),
        }), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptVersionOverlap");
    }

    [Fact]
    public async Task Desactivar_cierra_la_vigencia_y_el_salario_basico_esta_protegido()
    {
        var d = new NominaTestData();
        var h = new DeactivateConceptDefinitionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var ok = await h.Handle(new DeactivateConceptDefinitionCommand("VIATICOS", new DateTime(2026, 12, 31)), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        (await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.Code == "VIATICOS")).ValidTo.Should().Be(new DateTime(2026, 12, 31));

        var salario = await h.Handle(new DeactivateConceptDefinitionCommand("SALARIO", new DateTime(2026, 12, 31)), CancellationToken.None);
        salario.Error.Code.Should().Be("Payroll.ConceptSeedProtected");
    }

    [Fact]
    public async Task Poner_quitar_y_volver_a_poner_las_cuentas_de_un_concepto_revive_la_fila_por_defecto_en_vez_de_duplicarla()
    {
        // Feature 013, T0801: la e2e de Nómina con Cartera puso, quitó y volvió a poner las cuentas de DESC_CARTERA y la API respondió 500
        // (UX_PAY_ConceptDefinitionAccounts_Default): la fila retirada queda marcada (Principio VII), el filtro global la escondía y el comando
        // agregaba otra con el mismo concepto y sin centro de costo.
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var debito = d.Cuenta("51059901", "Gasto de prueba", Domain.Enums.Accounting.AccountNature.Debit);
        var credito = d.Cuenta("25059901", "Pasivo de prueba", Domain.Enums.Accounting.AccountNature.Credit);
        var h = new SetConceptAccountsCommandHandler(d.Db, d.Clock, d.User);

        (await h.Handle(new SetConceptAccountsCommand("SALARIO", []), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var r = await h.Handle(new SetConceptAccountsCommand("SALARIO", [new ConceptAccountRow(null, debito.Code, credito.Code)]), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var todas = await d.Db.PayrollConceptDefinitionAccounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.ConceptCode == "SALARIO" && a.CostCenterId == null).ToListAsync();
        todas.Should().ContainSingle("la fila por defecto es una sola, viva o retirada: el índice único no mira el borrado lógico")
            .Which.Should().BeEquivalentTo(new { IsDeleted = false, DebitAccountId = debito.Id, CreditAccountId = credito.Id }, o => o.ExcludingMissingMembers());
    }
}
