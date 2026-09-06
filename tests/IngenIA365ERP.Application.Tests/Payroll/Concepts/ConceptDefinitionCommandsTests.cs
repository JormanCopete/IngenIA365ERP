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
}
